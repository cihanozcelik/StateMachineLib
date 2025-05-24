using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using Nopnag.StateMachineLib.Util;
using UnityEngine;

// Added for marker and configurator types

// Added for IIListener
// using Nopnag.StateMachine.Runtime; // Removed incorrect using
// No specific using needed if AnyStateMarker is in Nopnag.StateMachineLib

namespace Nopnag.StateMachineLib
{
  public class StateGraph : IGraphHost
  {
    public static readonly AnyStateMarker
      Any = new(); // Using without explicit namespace as it's now in Nopnag.StateMachineLib
    /// <summary>
    /// A marker used with the fluent API (e.g., <code>sourceState > StateGraph.DynamicTarget</code>)
    /// to indicate that a transition's target state will be determined dynamically at runtime
    /// by a predicate function.
    /// </summary>
    public static readonly DynamicTargetMarker DynamicTarget = new();

    public StateUnit InitialUnit;
    
    // IGraphHost implementation
    private readonly GraphHost _graphHost;
    public IReadOnlyList<StateGraph> HostedGraphs => _graphHost.HostedGraphs;
    internal LocalEventBus LocalEventBus => _graphHost.LocalEventBus;
    LocalEventBus IGraphHost.LocalEventBus => LocalEventBus;

    const    int MAX_STATE_CHANGES_PER_UPDATE = 10; // Safety break for chained transitions
    readonly List<IStateTransition> _anyStateTransitions = new();
    List<IIListener> _graphEventTransitionListeners = new();
    bool _isDisposedByParent = false;
    readonly List<StateUnit> _units = new();
    StateUnit _currentUnit;
    public StateUnit CurrentUnit 
    { 
      get 
      {
        if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
        return _currentUnit;
      }
      internal set 
      {
        _currentUnit = value;
      }
    }
    public   bool IsGraphActive { get; private set; }

    public StateGraph()
    {
      _graphHost = new GraphHost();
    }

    public StateUnit CreateState()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      var stateUnit = new StateUnit(this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
    }

    [Obsolete(
      "Use CreateState() instead. If a name is needed, it should be managed by the user externally or via StateUnit properties if available.",
      false)]
    public StateUnit CreateUnit(string name)
    {
      var stateUnit = new StateUnit(name, this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
    }

    public void EnterGraph()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      IsGraphActive = true;
      StartState(InitialUnit);
    }

    public void ExitGraph()
    {
      CurrentUnit?.Exit();
      IsGraphActive = false;
      CurrentUnit   = null; // Clear current unit on exit
    }

    public void FixedUpdateGraph()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      CurrentUnit?.FixedUpdate();
    }

    // --- Fluent API for Any State Transitions ---

    /// <summary>
    /// Begins configuration for a transition from any state to the specified target state.
    /// Example: <code>graph.FromAny(targetState).When(condition);</code>
    /// </summary>
    /// <param name="targetState">The target state for the transition.</param>
    /// <returns>A TransitionConfigurator to define the transition's properties.</returns>
    public TransitionConfigurator FromAny(StateUnit targetState)
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      if (targetState == null) throw new ArgumentNullException(nameof(targetState));
      if (targetState.BaseGraph != this)
        throw new ArgumentException("Target state must belong to this graph.", nameof(targetState));
      return new TransitionConfigurator(this, targetState);
    }

    /// <summary>
    /// Begins configuration for a transition from any state to a dynamically determined target state.
    /// Example: <code>graph.FromAnyToDynamic().When(dynamicPredicate);</code>
    /// </summary>
    /// <returns>A DynamicTargetTransitionConfigurator to define the transition's properties.</returns>
    public DynamicTargetTransitionConfigurator FromAnyToDynamic()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      // The DynamicTarget marker is static and implicitly used by ConditionalTransition
      return new DynamicTargetTransitionConfigurator(this, DynamicTarget);
    }

    public string GetCurrentStateName()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      return CurrentUnit?.Name;
    }

    public bool IsUnitActive(StateUnit unit)
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      return IsGraphActive && CurrentUnit == unit;
    }

    public void LateUpdateGraph()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      CurrentUnit?.LateUpdate();
    }

    public void StartState(StateUnit unit)
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      if (unit == null)
      {
        Debug.LogWarning("Attempted to start a null state.");
        return;
      }

      CurrentUnit?.Exit();
      CurrentUnit = unit;
      CurrentUnit.Start();
    }

    public void UpdateGraph()
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      if (!IsGraphActive || CurrentUnit == null) return;

      var  safetyBreak = 0;
      bool stateChangedInCycle;

      do
      {
        if (CurrentUnit == null || safetyBreak++ >= MAX_STATE_CHANGES_PER_UPDATE)
        {
          if (safetyBreak >= MAX_STATE_CHANGES_PER_UPDATE)
            Debug.LogWarning(
              "StateGraph: Exceeded max state changes per update cycle. Breaking loop.");
          break;
        }

        stateChangedInCycle = false;
        var       deltaTimeInCurrentState   = CurrentUnit.DeltaTimeSinceStart;
        StateUnit targetStateFromTransition = null;

        // 1. Check Any-State Transitions
        foreach (var transition in _anyStateTransitions)
        {
          var transitionShouldFire =
            transition.CheckTransition(deltaTimeInCurrentState, out targetStateFromTransition);
          // The IStateTransition.CheckTransition is expected to provide the targetState if it's dynamic

          if (transitionShouldFire)
          {
            // Use the targetStateFromTransition if valid (e.g. from ConditionalTransition), otherwise fallback to transition.TargetUnit
            var actualTarget = targetStateFromTransition ?? transition.TargetUnit;

            if (actualTarget == null)
            {
              Debug.LogError(
                $"Any-state transition ({transition.GetType().Name}, to '{transition.TargetUnitName}') fired but resolved target is null. Current state: '{CurrentUnit.Name}'. Skipping this transition.");
              continue;
            }

            // Avoid immediate self-loop from Any to current for DirectTransition to prevent infinite state re-entry in one frame if not careful
            if (transition is DirectTransition && actualTarget == CurrentUnit) continue;

            // UnityEngine.Debug.Log($"Any-State Transition from {CurrentUnit.Name} to {actualTarget.Name}");
            StartState(
              actualTarget); // This calls Exit on old CurrentUnit, sets new CurrentUnit, calls Start on new CurrentUnit
            stateChangedInCycle = true;
            break;
          }
        }

        if (stateChangedInCycle)
          continue; // Restart loop to process new state (including its Any-State transitions again)

        // 2. If no any-state transition occurred, let the current unit process its update and local transitions.
        // StateUnit.Update() returns false if a local transition occurred (state changed), true otherwise.
        // The existing `StateUnit.Update()` calls `CheckTransitions()` which calls `BaseGraph.StartState()`.
        if (CurrentUnit.Update()) // True if NO local transition occurred
          // No local transition, and no any-state transition in this iteration.
          // The update cycle for this CurrentUnit for this specific UpdateGraph() call is done.
          break; // Exit the do-while loop.
        else
          // A local transition occurred within CurrentUnit.Update(). CurrentUnit has changed.
          stateChangedInCycle = true; // Ensure the loop continues to process the new state.
      } while (stateChangedInCycle && safetyBreak < MAX_STATE_CHANGES_PER_UPDATE);
    }

    void ClearSubscriptions()
    {
      foreach (var listener in _graphEventTransitionListeners) listener.Unsubscribe();
      _graphEventTransitionListeners.Clear();
      foreach (var state in _units) state.ClearSubscriptions();
    }

    /// <summary>
    /// Adds a transition that can occur from any state in this graph.
    /// Internal use: Called by TransitionConfigurator.
    /// </summary>
    internal void AddAnyStateTransition(IStateTransition transition)
    {
      if (transition == null) throw new ArgumentNullException(nameof(transition));
      _anyStateTransitions.Add(transition);
    }

    internal void RegisterEventTransitionListener(IIListener listener)
    {
      if (listener == null) throw new ArgumentNullException(nameof(listener));
      _graphEventTransitionListeners.Add(listener);
    }

    internal void MarkAsDisposed() // New method to be called by StateMachine
    {
      _isDisposedByParent = true;
      // Optionally, also ensure IsGraphActive is false and CurrentUnit is null if not already handled by ExitGraph
      IsGraphActive = false;
      _currentUnit = null; // Set directly to avoid dispose check
      ClearSubscriptions();
    }

    // IGraphHost implementation
    public void LocalRaise<T>(T busEvent) where T : BusEvent
    {
      if (_isDisposedByParent) throw new ObjectDisposedException(nameof(StateGraph));
      _graphHost.LocalRaise(busEvent);
    }
    
    public void AttachGraph(StateGraph graph) => _graphHost.AttachGraph(graph);
    public void DetachGraph(StateGraph graph) => _graphHost.DetachGraph(graph);
    public void UpdateAllGraphs() => _graphHost.UpdateAllGraphs();
    public void FixedUpdateAllGraphs() => _graphHost.FixedUpdateAllGraphs();
    public void LateUpdateAllGraphs() => _graphHost.LateUpdateAllGraphs();
    public StateGraph CreateGraph() => _graphHost.CreateGraph();
  }
}