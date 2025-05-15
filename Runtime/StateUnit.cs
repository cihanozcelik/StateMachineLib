using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using Nopnag.StateMachineLib.Util;
using UnityEngine;
using System.Linq;

namespace Nopnag.StateMachineLib
{
  public class StateUnit
  {
    struct ScheduledCallback
    {
      public float TargetTime;
      public Action Callback;
      public bool HasBeenInvoked;

      public ScheduledCallback(float targetTime, Action callback)
      {
        TargetTime = targetTime;
        Callback = callback;
        HasBeenInvoked = false;
      }
    }

    struct PeriodicCallback
    {
      public float IntervalTime;
      public Action Callback;
      public float NextInvocationTime;

      public PeriodicCallback(float intervalTime, Action callback)
      {
        IntervalTime = intervalTime;
        Callback = callback;
        NextInvocationTime = intervalTime;
      }
    }

    public readonly StateGraph BaseGraph;

    [Obsolete("Use OnEnter instead.", false)]
    public Action EnterStateFunction;
    public Action OnEnter
    {
      get => EnterStateFunction;
      set => EnterStateFunction = value;
    }

    [Obsolete("Use OnExit instead.", false)]
    public Action ExitStateFunction;
    public Action OnExit
    {
      get => ExitStateFunction;
      set => ExitStateFunction = value;
    }

    [Obsolete("Use OnFixedUpdate instead.", false)]
    public Action<float> FixedUpdateStateFunction;
    public Action<float> OnFixedUpdate
    {
      get => FixedUpdateStateFunction;
      set => FixedUpdateStateFunction = value;
    }

    [Obsolete("Use OnLateUpdate instead.", false)]
    public Action<float> LateUpdateStateFunction;
    public Action<float> OnLateUpdate
    {
      get => LateUpdateStateFunction;
      set => LateUpdateStateFunction = value;
    }

    public readonly string Name;
    public readonly List<IStateTransition> Transitions = new();

    [Obsolete("Use OnUpdateBeforeTransitionCheck instead.", false)]
    public Action<float> UpdateStateBeforeTransitionCheckFunction;
    public Action<float> OnUpdateBeforeTransitionCheck
    {
      get => UpdateStateBeforeTransitionCheckFunction;
      set => UpdateStateBeforeTransitionCheckFunction = value;
    }

    [Obsolete("Use OnUpdate instead.", false)]
    public Action<float> UpdateStateFunction;
    public Action<float> OnUpdate
    {
      get => UpdateStateFunction;
      set => UpdateStateFunction = value;
    }

    readonly List<ScheduledCallback> _scheduledCallbacks = new();
    readonly List<PeriodicCallback> _periodicCallbacks = new();
    float _previousTime;
    StateGraph _subGraph;

    public StateUnit(string name, StateGraph graph)
    {
      Name = name;
      BaseGraph = graph;
    }
    
    public StateUnit(StateGraph graph)
    {
      BaseGraph = graph;
    }

    public float DeltaTimeSinceStart { get; private set; }

    public void At(float targetTime, Action callback)
    {
      _scheduledCallbacks.Add(new ScheduledCallback(targetTime, callback));
    }

    public void AtEvery(float intervalTime, Action callback)
    {
      if (intervalTime <= 0f)
      {
        Debug.LogWarning("StateUnit.AtEvery: intervalTime must be positive.");
        return;
      }
      _periodicCallbacks.Add(new PeriodicCallback(intervalTime, callback));
    }

    internal bool CheckTransitions()
    {
      // Debug.Log("Check transitions");
      StateUnit targetState;
      for (var i = 0; i < Transitions.Count; i++)
        if (Transitions[i].CheckTransition(DeltaTimeSinceStart, out targetState))
        {
          if (ExitStateFunction != null) ExitStateFunction();

          BaseGraph.StartState(targetState);
          return true;
        }

      return false;
    }

    internal void Exit()
    {
      _subGraph?.ExitGraph();
      ExitStateFunction?.Invoke();
    }

    internal void FixedUpdate()
    {
      FixedUpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.FixedUpdateGraph();
    }

    public StateGraph GetSubStateGraph()
    {
      _subGraph = new StateGraph();
      return _subGraph;
    }

    internal bool IsActive()
    {
      return BaseGraph.IsUnitActive(this);
    }

    internal void LateUpdate()
    {
      LateUpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.LateUpdateGraph();
    }

    /// <summary>
    /// Subscribes to events of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="listener">The callback to invoke when the event is raised and this state is active.</param>
    [Obsolete("Use On<T>(listener) instead.", false)]
    public void Listen<T>(ListenerDelegate<T> listener) where T : BusEvent
    {
      EventBus<T>.Listen(
        @event =>
        {
          if (IsActive()) listener.Invoke(@event);
        }
      );
    }

    /// <summary>
    /// A synonym for Listen. Subscribes to events of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="listener">The callback to invoke when the event is raised and this state is active.</param>
    public void On<T>(ListenerDelegate<T> listener) where T : BusEvent
    {
        Listen(listener);
    }

    /// <summary>
    /// Subscribes to filtered events (via EventQuery) of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="query">The EventQuery to filter which events to listen for.</param>
    /// <param name="listener">The callback to invoke when the filtered event is raised and this state is active.</param>
    [Obsolete("Use On<T>(query, listener) instead.", false)]
    public void Listen<T>(EventQuery<T> query, ListenerDelegate<T> listener) where T : BusEvent
    {
      query.Listen(
        @event =>
        {
          if (IsActive()) listener.Invoke(@event);
        }
      );
    }

    /// <summary>
    /// A synonym for Listen. Subscribes to filtered events (via EventQuery) of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="query">The EventQuery to filter which events to listen for.</param>
    /// <param name="listener">The callback to invoke when the filtered event is raised and this state is active.</param>
    public void On<T>(EventQuery<T> query, ListenerDelegate<T> listener) where T : BusEvent
    {
        Listen(query, listener);
    }

    [Obsolete("Use On<T>() instead.", false)]
    public void ListenSignal<T>(ref Action<T> signal, Action<T> sensor)
    {
      signal += parameter =>
      {
        if (IsActive()) sensor(parameter);
      };
    }

    /// <summary>
    /// A synonym for ListenSignal. Subscribes a sensor Action to an external signal Action.
    /// The sensor is only invoked if this StateUnit is active when the signal is raised.
    /// </summary>
    /// <typeparam name="T">The type of the parameter for the signal and sensor actions.</typeparam>
    /// <param name="signal">The external signal Action to listen to (passed by reference).</param>
    /// <param name="sensor">The Action to execute when the signal is raised and this state is active.</param>
    public void On<T>(ref Action<T> signal, Action<T> sensor)
    {
      ListenSignal(ref signal, sensor);
    }

    public void SetSubStateGraph(StateGraph subGraph)
    {
      _subGraph = subGraph;
    }

    internal void Start()
    {
      _previousTime = Time.time;
      DeltaTimeSinceStart = 0;
      EnterStateFunction?.Invoke();
      _subGraph?.EnterGraph();

      for (int i = 0; i < _scheduledCallbacks.Count; i++)
      {
        var sc = _scheduledCallbacks[i];
        sc.HasBeenInvoked = false;
        _scheduledCallbacks[i] = sc;
      }

      for (int i = 0; i < _periodicCallbacks.Count; i++)
      {
        var pc = _periodicCallbacks[i];
        pc.NextInvocationTime = pc.IntervalTime;
        _periodicCallbacks[i] = pc;
      }

      CheckScheduledCallbacks();
      CheckPeriodicCallbacks();
      CheckTransitions();
    }

    internal bool Update()
    {
      DeltaTimeSinceStart += (Time.time - _previousTime) * 1; // timescale here
      _previousTime = Time.time;
      UpdateStateBeforeTransitionCheckFunction?.Invoke(DeltaTimeSinceStart);

      CheckScheduledCallbacks();
      CheckPeriodicCallbacks();

      if (CheckTransitions()) return false;

      UpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.UpdateGraph();
      return true;
    }
    
    void CheckScheduledCallbacks()
    {
      for (int i = 0; i < _scheduledCallbacks.Count; i++)
      {
        var scheduledCallback = _scheduledCallbacks[i]; // Get a copy of the struct
        if (!scheduledCallback.HasBeenInvoked && DeltaTimeSinceStart >= scheduledCallback.TargetTime)
        {
          scheduledCallback.Callback?.Invoke();
          scheduledCallback.HasBeenInvoked = true;
          _scheduledCallbacks[i] = scheduledCallback; // Assign the modified copy back
        }
      }
    }

    void CheckPeriodicCallbacks()
    {
      for (int i = 0; i < _periodicCallbacks.Count; i++)
      {
        var pc = _periodicCallbacks[i]; // Get a copy of the struct
        bool invokedInLoop = false;

        while (DeltaTimeSinceStart >= pc.NextInvocationTime && pc.IntervalTime > 0)
        {
          pc.Callback?.Invoke();
          pc.NextInvocationTime += pc.IntervalTime;
          invokedInLoop = true;
        }

        if (invokedInLoop)
        {
          _periodicCallbacks[i] = pc; // Assign the modified copy back
        }
      }
    }

    // --- Operator Overloads for Fluent API ---

    /// <summary>
    /// Initiates a fluent transition definition from a source state to a target state.
    /// Example: <code>(stateA > stateB).When(...);</code>
    /// </summary>
    public static TransitionConfigurator operator >(StateUnit fromState, StateUnit toState)
    {
      return new TransitionConfigurator(fromState, toState);
    }

    /// <summary>
    /// Initiates a fluent transition definition from a source state to a target state (reversed operands).
    /// Satisfies CS0216 (operator < requires matching operator >).
    /// Conceptually, <code>(targetState < fromState)</code> is equivalent to <code>(fromState > targetState)</code>.
    /// </summary>
    public static TransitionConfigurator operator <(StateUnit toState, StateUnit fromState)
    {
      // We ensure that FromState is always the left operand of > or right operand of < conceptually
      return new TransitionConfigurator(fromState, toState);
    }

    /// <summary>
    /// Initiates a fluent transition definition from a source state to one of several target states (indexed).
    /// Example: <code>(stateA > new[] { stateB, stateC }).When(...);</code>
    /// </summary>
    public static MultiTargetTransitionConfigurator operator >(StateUnit fromState, StateUnit[] toStates)
    {
        return new MultiTargetTransitionConfigurator(fromState, toStates);
    }

    /// <summary>
    /// Matching operator for <code>StateUnit > StateUnit[]</code> to satisfy CS0216.
    /// This syntax (sourceState < targetStatesArray) is not supported for fluent transitions.
    /// </summary>
    public static MultiTargetTransitionConfigurator operator <(StateUnit fromState, StateUnit[] toStates)
    {
        throw new NotSupportedException("The syntax 'StateUnit < StateUnit[]' is not supported for defining multi-target transitions. Use 'StateUnit > StateUnit[]'.");
    }

    /// <summary>
    /// Initiates a fluent transition definition from a source state to a dynamically resolved target state.
    /// Example: <code>(stateA > StateGraph.DynamicTarget).When(...);</code>
    /// </summary>
    public static DynamicTargetTransitionConfigurator operator >(StateUnit fromState, DynamicTargetMarker dynamicTargetMarker)
    {
        return new DynamicTargetTransitionConfigurator(fromState);
    }

    /// <summary>
    /// Matching operator for <code>StateUnit > DynamicTargetMarker</code> to satisfy CS0216.
    /// This syntax (sourceState < dynamicTargetMarker) is not supported for fluent transitions.
    /// </summary>
    public static DynamicTargetTransitionConfigurator operator <(StateUnit fromState, DynamicTargetMarker dynamicTargetMarker)
    {
        throw new NotSupportedException("The syntax 'StateUnit < DynamicTargetMarker' is not supported. Use 'StateUnit > StateGraph.DynamicTarget'.");
    }
    
    /// <summary>
    /// Initiates a fluent transition definition from any state within the targetUnit's graph to the targetUnit.
    /// This is syntactic sugar for <code>targetUnit.BaseGraph.FromAny(targetUnit)</code>.
    /// Example: <code>(StateGraph.Any > stateB).When(...);</code>
    /// </summary>
    public static TransitionConfigurator operator >(AnyStateMarker anyMarker, StateUnit targetUnit)
    {
        if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
        if (targetUnit.BaseGraph == null) 
            throw new InvalidOperationException("Target state must be associated with a graph to create an AnyState transition using StateGraph.Any syntax.");
        return targetUnit.BaseGraph.FromAny(targetUnit);
    }

    /// <summary>
    /// Matching operator for <code>AnyStateMarker > StateUnit</code> to satisfy C# operator pairing rules (CS0216).
    /// This syntax (AnyStateMarker < StateUnit) is not supported for defining transitions.
    /// </summary>
    public static TransitionConfigurator operator <(AnyStateMarker anyMarker, StateUnit targetUnit)
    {
        throw new NotSupportedException("The '<' operator is not supported for AnyState transitions. Use '(StateGraph.Any > yourStateUnit)' to define transitions.");
    }
  }
}