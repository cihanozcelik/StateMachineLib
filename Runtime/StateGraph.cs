using System;
using System.Collections.Generic;
using Nopnag.StateMachineLib.Transition;
using Nopnag.StateMachineLib.Util; // Added for marker and configurator types
// using Nopnag.StateMachine.Runtime; // Removed incorrect using
// No specific using needed if AnyStateMarker is in Nopnag.StateMachineLib

namespace Nopnag.StateMachineLib
{
  public class StateGraph
  {
    /// <summary>
    /// A marker used with the fluent API (e.g., <code>sourceState > StateGraph.DynamicTarget</code>)
    /// to indicate that a transition's target state will be determined dynamically at runtime
    /// by a predicate function.
    /// </summary>
    public static readonly DynamicTargetMarker DynamicTarget = new DynamicTargetMarker();
    public static readonly AnyStateMarker Any = new AnyStateMarker(); // Using without explicit namespace as it's now in Nopnag.StateMachineLib

    private const int MAX_STATE_CHANGES_PER_UPDATE = 10; // Safety break for chained transitions

    public StateUnit InitialUnit;
    public bool IsGraphActive { get; private set; } // Made setter private
    StateUnit _currentUnit;
    readonly List<StateUnit> _units = new();
    private readonly List<IStateTransition> _anyStateTransitions = new List<IStateTransition>();

    [Obsolete("Use CreateState() instead. If a name is needed, it should be managed by the user externally or via StateUnit properties if available.", false)]
    public StateUnit CreateUnit(string name)
    {
      var stateUnit = new StateUnit(name, this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
    }

    public StateUnit CreateState()
    {
      var stateUnit = new StateUnit(this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
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

    // --- Fluent API for Any State Transitions ---

    /// <summary>
    /// Begins configuration for a transition from any state to the specified target state.
    /// Example: <code>graph.FromAny(targetState).When(condition);</code>
    /// </summary>
    /// <param name="targetState">The target state for the transition.</param>
    /// <returns>A TransitionConfigurator to define the transition's properties.</returns>
    public TransitionConfigurator FromAny(StateUnit targetState)
    {
        if (targetState == null) throw new ArgumentNullException(nameof(targetState));
        if (targetState.BaseGraph != this) throw new ArgumentException("Target state must belong to this graph.", nameof(targetState));
        return new TransitionConfigurator(this, targetState);
    }

    /// <summary>
    /// Begins configuration for a transition from any state to a dynamically determined target state.
    /// Example: <code>graph.FromAnyToDynamic().When(dynamicPredicate);</code>
    /// </summary>
    /// <returns>A DynamicTargetTransitionConfigurator to define the transition's properties.</returns>
    public DynamicTargetTransitionConfigurator FromAnyToDynamic()
    {
        // The DynamicTarget marker is static and implicitly used by ConditionalTransition
        return new DynamicTargetTransitionConfigurator(this, DynamicTarget);
    }

    public void EnterGraph()
    {
      IsGraphActive = true;
      StartState(InitialUnit);
    }

    public void ExitGraph()
    {
      _currentUnit?.Exit();
      IsGraphActive = false;
      _currentUnit = null; // Clear current unit on exit
    }

    public void FixedUpdateGraph()
    {
      _currentUnit?.FixedUpdate();
    }

    public string GetCurrentStateName()
    {
      return _currentUnit?.Name;
    }

    public bool IsUnitActive(StateUnit unit)
    {
      return IsGraphActive && _currentUnit == unit;
    }

    public void LateUpdateGraph()
    {
      _currentUnit?.LateUpdate();
    }

    public void StartState(StateUnit unit)
    {
      if (unit == null)
      {
          UnityEngine.Debug.LogWarning("Attempted to start a null state.");
          return;
      }
      if (!_units.Contains(unit) && unit != InitialUnit) // InitialUnit might not be in _units if graph is empty then InitialUnit set externally
      {
          // This check might be too strict if states can be added/removed dynamically in complex ways
          // For now, assume states are created via CreateState/CreateUnit or InitialUnit is valid.
          // UnityEngine.Debug.LogWarning($"Attempted to start state '{unit.Name}' which is not part of this graph's known units.");
          // To be safer, we could add it: if(!_units.Contains(unit)) _units.Add(unit);
          // However, a state should know its graph.
      }

      _currentUnit?.Exit();
      _currentUnit = unit;
      _currentUnit.Start();
    }

    public void UpdateGraph()
    {
        if (!IsGraphActive || _currentUnit == null) return;

        int safetyBreak = 0;
        bool stateChangedInCycle;

        do
        {
            if (_currentUnit == null || safetyBreak++ >= MAX_STATE_CHANGES_PER_UPDATE)
            {
                if (safetyBreak >= MAX_STATE_CHANGES_PER_UPDATE) UnityEngine.Debug.LogWarning("StateGraph: Exceeded max state changes per update cycle. Breaking loop.");
                break;
            }

            stateChangedInCycle = false;
            float deltaTimeInCurrentState = _currentUnit.DeltaTimeSinceStart;
            StateUnit targetStateFromTransition = null;

            // 1. Check Any-State Transitions
            foreach (var transition in _anyStateTransitions)
            {
                bool transitionShouldFire = false;
                // The IStateTransition.CheckTransition is expected to provide the targetState if it's dynamic
                if (transition.CheckTransition(deltaTimeInCurrentState, out targetStateFromTransition)) 
                {
                    transitionShouldFire = true;
                }
                
                if (transitionShouldFire)
                {
                    // Use the targetStateFromTransition if valid (e.g. from ConditionalTransition), otherwise fallback to transition.TargetUnit
                    StateUnit actualTarget = targetStateFromTransition ?? transition.TargetUnit;

                    if (actualTarget == null)
                    {
                         UnityEngine.Debug.LogError($"Any-state transition ({transition.GetType().Name}, to '{transition.TargetUnitName}') fired but resolved target is null. Current state: '{_currentUnit.Name}'. Skipping this transition.");
                         continue;
                    }
                    
                    // Avoid immediate self-loop from Any to current for DirectTransition to prevent infinite state re-entry in one frame if not careful
                    if (transition is DirectTransition && actualTarget == _currentUnit)
                    {
                        continue; 
                    }
                    
                    // UnityEngine.Debug.Log($"Any-State Transition from {_currentUnit.Name} to {actualTarget.Name}");
                    StartState(actualTarget); // This calls Exit on old _currentUnit, sets new _currentUnit, calls Start on new _currentUnit
                    stateChangedInCycle = true;
                    break; 
                }
            }

            if (stateChangedInCycle)
            {
                continue; // Restart loop to process new state (including its Any-State transitions again)
            }

            // 2. If no any-state transition occurred, let the current unit process its update and local transitions.
            // StateUnit.Update() returns false if a local transition occurred (state changed), true otherwise.
            // The existing `StateUnit.Update()` calls `CheckTransitions()` which calls `BaseGraph.StartState()`.
            if (_currentUnit.Update()) // True if NO local transition occurred
            {
                // No local transition, and no any-state transition in this iteration.
                // The update cycle for this _currentUnit for this specific UpdateGraph() call is done.
                break; // Exit the do-while loop.
            }
            else
            {
                // A local transition occurred within _currentUnit.Update(). _currentUnit has changed.
                stateChangedInCycle = true; // Ensure the loop continues to process the new state.
            }

        } while (stateChangedInCycle && safetyBreak < MAX_STATE_CHANGES_PER_UPDATE);
    }

    // public void CheckTransitions()
    // {
    //   StateUnit transitionedUnit = currentUnit?.CheckTransitions();
    //   if(transitionedUnit != null)
    //   {
    //     StartState(transitionedUnit);
    //   }
    // }
  }
}