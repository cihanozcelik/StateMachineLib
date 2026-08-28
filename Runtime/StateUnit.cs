using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using Nopnag.StateMachineLib.Util;
using UnityEngine;

namespace Nopnag.StateMachineLib
{
  public class StateUnit : IGraphHost, IPoweredNode
  {
    struct PeriodicCallback
    {
      public float  IntervalTime;
      public Action Callback;
      public float  NextInvocationTime;

      public PeriodicCallback(float intervalTime, Action callback)
      {
        IntervalTime       = intervalTime;
        Callback           = callback;
        NextInvocationTime = intervalTime;
      }
    }

    struct ScheduledCallback
    {
      public float  TargetTime;
      public Action Callback;
      public bool   HasBeenInvoked;

      public ScheduledCallback(float targetTime, Action callback)
      {
        TargetTime     = targetTime;
        Callback       = callback;
        HasBeenInvoked = false;
      }
    }

    public readonly StateGraph BaseGraph;

    [Obsolete("Use OnEnter instead.", false)]
    public Action EnterStateFunction;

    [Obsolete("Use OnExit instead.", false)]
    public Action ExitStateFunction;

    [Obsolete("Use OnFixedUpdate instead.", false)]
    public Action<float> FixedUpdateStateFunction;

    [Obsolete("Use OnLateUpdate instead.", false)]
    public Action<float> LateUpdateStateFunction;

    public readonly string                 Name;
    public readonly List<IStateTransition> Transitions      = new();
    public readonly List<IStateTransition> FixedTransitions = new();

    [Obsolete("Use OnUpdateBeforeTransitionCheck instead.", false)]
    public Action<float> UpdateStateBeforeTransitionCheckFunction;

    [Obsolete("Use OnUpdate instead.", false)]
    public Action<float> UpdateStateFunction;

    // IGraphHost implementation
    readonly GraphHost _graphHost;

    // New multi-graph support via composition
    readonly List<PeriodicCallback> _periodicCallbacks      = new();
    readonly List<PeriodicCallback> _fixedPeriodicCallbacks = new();

    // IPoweredNode implementation  
    readonly PoweredNode _poweredNode;
    float                _previousTime;
    float                _localTimeScale = 1f;

    readonly List<ScheduledCallback> _scheduledCallbacks         = new();
    readonly List<ScheduledCallback> _fixedScheduledCallbacks    = new();
    List<IIListener>                 _stateUnitEventBusListeners = new();

    internal StateUnit(string name, StateGraph graph)
    {
      Name         = name;
      BaseGraph    = graph;
      _graphHost   = new GraphHost();
      _poweredNode = new PoweredNode(false);
      _poweredNode.SetTurnedOn(true); // StateUnits are always turned on
    }

    internal StateUnit(StateGraph graph)
    {
      BaseGraph    = graph;
      _graphHost   = new GraphHost();
      _poweredNode = new PoweredNode(false);
      _poweredNode.SetTurnedOn(true); // StateUnits are always turned on
    }

    /// <summary>
    /// Accumulated scaled Update time since this state was entered. This is the
    /// existing Update-time clock retained for backwards compatibility.
    /// </summary>
    public float DeltaTimeSinceStart { get; private set; }

    /// <summary>
    /// Scaled delta consumed by this state during its latest Update tick.
    /// </summary>
    public float DeltaTime { get; private set; }

    /// <summary>
    /// Scaled fixed delta consumed by this state during its latest FixedUpdate tick.
    /// </summary>
    public float FixedDeltaTime { get; private set; }

    /// <summary>
    /// Accumulated scaled FixedUpdate time since this state was entered.
    /// </summary>
    public float FixedElapsed { get; private set; }

    internal LocalEventBus LocalEventBus       => _graphHost.LocalEventBus;

    /// <summary>
    /// Scales this state's Update and FixedUpdate clocks and all hosted graphs.
    /// A value of 1 is normal speed, 0.5 is half speed, and 0 freezes time-driven
    /// callbacks, polling transitions, timers, and hosted graph ticks while leaving
    /// the state active for event handling.
    /// </summary>
    public float LocalTimeScale
    {
      get => _localTimeScale;
      set
      {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
          throw new ArgumentOutOfRangeException(nameof(value), value,
            "StateUnit.LocalTimeScale must be finite and greater than or equal to zero.");

        var wasFrozen = _localTimeScale == 0f;
        _localTimeScale = value;
        if (wasFrozen && value > 0f) RebaseUpdateClock();
      }
    }

    internal bool IsTimeFrozen => _localTimeScale == 0f;
    public Action OnEnter
    {
      get => EnterStateFunction;
      set => EnterStateFunction = value;
    }
    public Action OnExit
    {
      get => ExitStateFunction;
      set => ExitStateFunction = value;
    }
    /// <summary>
    /// Legacy FixedUpdate callback. Its argument remains DeltaTimeSinceStart, the
    /// accumulated Update-time clock. New physics-time code should use OnFixedTick.
    /// </summary>
    public Action<float> OnFixedUpdate
    {
      get => FixedUpdateStateFunction;
      set => FixedUpdateStateFunction = value;
    }
    /// <summary>
    /// Receives the scaled fixed delta and elapsed fixed time for this state.
    /// Unlike the legacy OnFixedUpdate callback, both values are driven by FixedUpdate.
    /// </summary>
    public Action<float, float> OnFixedTick { get; set; }
    public Action<float> OnLateUpdate
    {
      get => LateUpdateStateFunction;
      set => LateUpdateStateFunction = value;
    }
    public Action<float> OnUpdate
    {
      get => UpdateStateFunction;
      set => UpdateStateFunction = value;
    }
    public Action<float> OnUpdateBeforeTransitionCheck
    {
      get => UpdateStateBeforeTransitionCheckFunction;
      set => UpdateStateBeforeTransitionCheckFunction = value;
    }

    // IPoweredNode explicit implementations
    public void AttachChild(IPoweredNode child)
    {
      _poweredNode.AttachChild(child);
    }

    public void AttachGraph(StateGraph graph)
    {
      AttachChild(graph);
      _graphHost.AttachGraph(graph);
    }

    public StateGraph CreateGraph()
    {
      var graph = _graphHost.CreateGraph();
      AttachChild(graph);
      return graph;
    }

    public void DetachChild(IPoweredNode child)
    {
      _poweredNode.DetachChild(child);
    }

    public void DetachGraph(StateGraph graph)
    {
      DetachChild(graph);
      _graphHost.DetachGraph(graph);
    }

    public void FixedUpdateAllGraphs()
    {
      _graphHost.FixedUpdateAllGraphs();
    }

    internal void FixedUpdateAllGraphs(float fixedDeltaTime)
    {
      _graphHost.FixedUpdateAllGraphs(fixedDeltaTime);
    }

    // IPoweredNode implementation
    public bool HasPower => _poweredNode.HasPower;

    public IReadOnlyList<StateGraph> HostedGraphs => _graphHost.HostedGraphs;

    public bool IsActive   => _poweredNode.IsActive && BaseGraph?.CurrentUnit == this;
    public bool IsTurnedOn => _poweredNode.IsTurnedOn;

    public void LateUpdateAllGraphs()
    {
      _graphHost.LateUpdateAllGraphs();
    }

    LocalEventBus IGraphHost.LocalEventBus => LocalEventBus;

    // IGraphHost implementation
    public void LocalRaise<T>(T busEvent) where T : BusEvent
    {
      _graphHost.LocalRaise(busEvent);
    }

    void IPoweredNode.RefreshPowerState()
    {
      _poweredNode.RefreshPowerState();
    }

    public void SetParent(IPoweredNode? parent)
    {
      var wasActive = IsActive;
      _poweredNode.SetParent(parent);
      if (!wasActive && IsActive) RebaseUpdateClock();
    }

    public void SetTurnedOn(bool on)
    {
      var wasActive = IsActive;
      _poweredNode.SetTurnedOn(on);
      if (!wasActive && IsActive) RebaseUpdateClock();
    }

    public void UpdateAllGraphs()
    {
      _graphHost.UpdateAllGraphs();
    }

    internal void UpdateAllGraphs(float deltaTime)
    {
      _graphHost.UpdateAllGraphs(deltaTime);
    }

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

    /// <summary>
    /// Schedules a callback using this state's FixedElapsed clock.
    /// </summary>
    public void AtFixed(float targetTime, Action callback)
    {
      _fixedScheduledCallbacks.Add(new ScheduledCallback(targetTime, callback));
    }

    /// <summary>
    /// Repeats a callback using this state's FixedElapsed clock.
    /// </summary>
    public void AtEveryFixed(float intervalTime, Action callback)
    {
      if (intervalTime <= 0f)
      {
        Debug.LogWarning("StateUnit.AtEveryFixed: intervalTime must be positive.");
        return;
      }

      _fixedPeriodicCallbacks.Add(new PeriodicCallback(intervalTime, callback));
    }

    /// <summary>
    /// Subscribes to events of type T from both Global and Local EventBus, but only invokes the listener while this state is active.
    /// A state entered by an event transition does not receive that same event raise.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="listener">The callback to invoke when the event is raised and this state is active.</param>
    [Obsolete("Use On<T>(listener) instead.", false)]
    public void Listen<T>(ListenerDelegate<T> listener) where T : BusEvent
    {
      // Subscribe to Global EventBus
      var globalHandle = EventBus<T>.Listen(
        @event =>
        {
          if (CanProcessEvent(@event)) listener.Invoke(@event);
        }
      );
      _stateUnitEventBusListeners.Add(globalHandle);

      // Subscribe to BaseGraph's LocalEventBus (parent graph events)
      if (BaseGraph != null)
      {
        var parentHandle = BaseGraph.LocalEventBus.On<T>().Listen(
          @event =>
          {
            if (CanProcessEvent(@event)) listener.Invoke(@event);
          }
        );
        _stateUnitEventBusListeners.Add(parentHandle);
      }

      // Subscribe to own LocalEventBus (subgraph events)
      var localHandle = LocalEventBus.On<T>().Listen(
        @event =>
        {
          if (CanProcessEvent(@event)) listener.Invoke(@event);
        }
      );
      _stateUnitEventBusListeners.Add(localHandle);
    }

    /// <summary>
    /// Subscribes to filtered events (via EventQuery) of type T from both Global and Local EventBus, but only invokes the listener while this state is active.
    /// A state entered by an event transition does not receive that same event raise.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="query">The EventQuery to filter which events to listen for.</param>
    /// <param name="listener">The callback to invoke when the filtered event is raised and this state is active.</param>
    [Obsolete("Use On<T>(query, listener) instead.", false)]
    public void Listen<T>(EventQuery<T> query, ListenerDelegate<T> listener) where T : BusEvent
    {
      // Subscribe to Global EventBus with query
      var globalHandle = query.Listen(
        @event =>
        {
          if (CanProcessEvent(@event)) listener.Invoke(@event);
        }
      );
      _stateUnitEventBusListeners.Add(globalHandle);

      // Subscribe to BaseGraph's LocalEventBus (parent graph events)
      if (BaseGraph != null)
      {
        var parentHandle = BaseGraph.LocalEventBus.On<T>().Listen(
          @event =>
          {
            if (CanProcessEvent(@event)) listener.Invoke(@event);
          }
        );
        _stateUnitEventBusListeners.Add(parentHandle);
      }

      // Subscribe to own LocalEventBus (subgraph events)
      var localHandle = LocalEventBus.On<T>().Listen(
        @event =>
        {
          if (CanProcessEvent(@event)) listener.Invoke(@event);
        }
      );
      _stateUnitEventBusListeners.Add(localHandle);
    }

    bool CanProcessEvent(BusEvent @event)
    {
      return IsActive
             && BaseGraph != null
             && BaseGraph.CanProcessEventRaise(@event.RaiseUniqueId);
    }

    /// <summary>
    /// Subscribes to events of type T from both Global and Local EventBus, but only invokes the listener while this state is active.
    /// A state entered by an event transition does not receive that same event raise.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="listener">The callback to invoke when the event is raised and this state is active.</param>
    public void On<T>(ListenerDelegate<T> listener) where T : BusEvent
    {
      Listen(listener);
    }

    /// <summary>
    /// Subscribes to filtered events (via EventQuery) of type T from both Global and Local EventBus, but only invokes the listener while this state is active.
    /// A state entered by an event transition does not receive that same event raise.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="query">The EventQuery to filter which events to listen for.</param>
    /// <param name="listener">The callback to invoke when the filtered event is raised and this state is active.</param>
    public void On<T>(EventQuery<T> query, ListenerDelegate<T> listener) where T : BusEvent
    {
      Listen(query, listener);
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
    /// Initiates a fluent transition definition from a source state to one of several target states (indexed).
    /// Example: <code>(stateA > new[] { stateB, stateC }).When(...);</code>
    /// </summary>
    public static MultiTargetTransitionConfigurator operator >(
      StateUnit   fromState,
      StateUnit[] toStates
    )
    {
      return new MultiTargetTransitionConfigurator(fromState, toStates);
    }

    /// <summary>
    /// Initiates a fluent transition definition from a source state to a dynamically resolved target state.
    /// Example: <code>(stateA > StateGraph.DynamicTarget).When(...);</code>
    /// </summary>
    public static DynamicTargetTransitionConfigurator operator >(
      StateUnit           fromState,
      DynamicTargetMarker dynamicTargetMarker
    )
    {
      return new DynamicTargetTransitionConfigurator(fromState);
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
        throw new InvalidOperationException(
          "Target state must be associated with a graph to create an AnyState transition using StateGraph.Any syntax.");
      return targetUnit.BaseGraph.FromAny(targetUnit);
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
    /// Matching operator for <code>StateUnit > StateUnit[]</code> to satisfy CS0216.
    /// This syntax (sourceState < targetStatesArray) is not supported for fluent transitions.
    /// </summary>
    public static MultiTargetTransitionConfigurator operator <(
      StateUnit   fromState,
      StateUnit[] toStates
    )
    {
      throw new NotSupportedException(
        "The syntax 'StateUnit < StateUnit[]' is not supported for defining multi-target transitions. Use 'StateUnit > StateUnit[]'.");
    }

    /// <summary>
    /// Matching operator for <code>StateUnit > DynamicTargetMarker</code> to satisfy CS0216.
    /// This syntax (sourceState < dynamicTargetMarker) is not supported for fluent transitions.
    /// </summary>
    public static DynamicTargetTransitionConfigurator operator <(
      StateUnit           fromState,
      DynamicTargetMarker dynamicTargetMarker
    )
    {
      throw new NotSupportedException(
        "The syntax 'StateUnit < DynamicTargetMarker' is not supported. Use 'StateUnit > StateGraph.DynamicTarget'.");
    }

    /// <summary>
    /// Matching operator for <code>AnyStateMarker > StateUnit</code> to satisfy C# operator pairing rules (CS0216).
    /// This syntax (AnyStateMarker < StateUnit) is not supported for defining transitions.
    /// </summary>
    public static TransitionConfigurator operator <(AnyStateMarker anyMarker, StateUnit targetUnit)
    {
      throw new NotSupportedException(
        "The '<' operator is not supported for AnyState transitions. Use '(StateGraph.Any > yourStateUnit)' to define transitions.");
    }

    internal void Start()
    {
      _previousTime       = Time.time;
      DeltaTime           = 0f;
      DeltaTimeSinceStart = 0;
      FixedDeltaTime      = 0f;
      FixedElapsed        = 0f;

      EnterStateFunction?.Invoke();

      // Start all hosted graphs via GraphHost
      _graphHost.StartAllGraphs();

      for (var i = 0; i < _scheduledCallbacks.Count; i++)
      {
        var sc = _scheduledCallbacks[i];
        sc.HasBeenInvoked      = false;
        _scheduledCallbacks[i] = sc;
      }

      for (var i = 0; i < _periodicCallbacks.Count; i++)
      {
        var pc = _periodicCallbacks[i];
        pc.NextInvocationTime = pc.IntervalTime;
        _periodicCallbacks[i] = pc;
      }

      for (var i = 0; i < _fixedScheduledCallbacks.Count; i++)
      {
        var sc = _fixedScheduledCallbacks[i];
        sc.HasBeenInvoked           = false;
        _fixedScheduledCallbacks[i] = sc;
      }

      for (var i = 0; i < _fixedPeriodicCallbacks.Count; i++)
      {
        var pc = _fixedPeriodicCallbacks[i];
        pc.NextInvocationTime      = pc.IntervalTime;
        _fixedPeriodicCallbacks[i] = pc;
      }

      if (!IsTimeFrozen)
      {
        CheckScheduledCallbacks();
        CheckPeriodicCallbacks();
        CheckTransitions();
      }
    }

    internal bool Update()
    {
      var now = Time.time;
      var deltaTime = Mathf.Max(0f, now - _previousTime);
      return Update(deltaTime, now);
    }

    internal bool Update(float deltaTime)
    {
      return Update(deltaTime, Time.time);
    }

    bool Update(float deltaTime, float now)
    {
      _previousTime = now;
      if (IsTimeFrozen)
      {
        DeltaTime = 0f;
        return true;
      }

      DeltaTime = deltaTime * _localTimeScale;
      DeltaTimeSinceStart += DeltaTime;
      UpdateStateBeforeTransitionCheckFunction?.Invoke(DeltaTimeSinceStart);

      CheckScheduledCallbacks();
      CheckPeriodicCallbacks();

      if (CheckTransitions()) return false;

      UpdateStateFunction?.Invoke(DeltaTimeSinceStart);

      // Update all hosted graphs via GraphHost
      UpdateAllGraphs(DeltaTime);

      return true;
    }

    internal void AdvanceFixedTime(float fixedDeltaTime)
    {
      FixedDeltaTime = fixedDeltaTime * _localTimeScale;
      FixedElapsed += FixedDeltaTime;
    }

    internal bool FixedUpdateAfterTimeAdvance()
    {
      if (IsTimeFrozen) return true;

      CheckFixedScheduledCallbacks();
      CheckFixedPeriodicCallbacks();

      if (CheckFixedTransitions()) return false;

      // Preserve the legacy callback and its elapsed-Update argument.
      FixedUpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      OnFixedTick?.Invoke(FixedDeltaTime, FixedElapsed);

      // FixedUpdate all hosted graphs via GraphHost
      FixedUpdateAllGraphs(FixedDeltaTime);
      return true;
    }

    internal void LateUpdate()
    {
      if (IsTimeFrozen) return;
      LateUpdateStateFunction?.Invoke(DeltaTimeSinceStart);

      // LateUpdate all hosted graphs via GraphHost
      LateUpdateAllGraphs();
    }

    void CheckPeriodicCallbacks()
    {
      for (var i = 0; i < _periodicCallbacks.Count; i++)
      {
        var pc            = _periodicCallbacks[i]; // Get a copy of the struct
        var invokedInLoop = false;

        while (DeltaTimeSinceStart >= pc.NextInvocationTime && pc.IntervalTime > 0)
        {
          pc.Callback?.Invoke();
          pc.NextInvocationTime += pc.IntervalTime;
          invokedInLoop         =  true;
        }

        if (invokedInLoop) _periodicCallbacks[i] = pc; // Assign the modified copy back
      }
    }

    void CheckScheduledCallbacks()
    {
      for (var i = 0; i < _scheduledCallbacks.Count; i++)
      {
        var scheduledCallback = _scheduledCallbacks[i]; // Get a copy of the struct
        if (!scheduledCallback.HasBeenInvoked &&
            DeltaTimeSinceStart >= scheduledCallback.TargetTime)
        {
          scheduledCallback.Callback?.Invoke();
          scheduledCallback.HasBeenInvoked = true;
          _scheduledCallbacks[i]           = scheduledCallback; // Assign the modified copy back
        }
      }
    }

    void CheckFixedPeriodicCallbacks()
    {
      for (var i = 0; i < _fixedPeriodicCallbacks.Count; i++)
      {
        var callback = _fixedPeriodicCallbacks[i];
        var invoked = false;
        while (HasReached(FixedElapsed, callback.NextInvocationTime) &&
               callback.IntervalTime > 0f)
        {
          callback.Callback?.Invoke();
          callback.NextInvocationTime += callback.IntervalTime;
          invoked = true;
        }

        if (invoked) _fixedPeriodicCallbacks[i] = callback;
      }
    }

    void CheckFixedScheduledCallbacks()
    {
      for (var i = 0; i < _fixedScheduledCallbacks.Count; i++)
      {
        var callback = _fixedScheduledCallbacks[i];
        if (!callback.HasBeenInvoked && HasReached(FixedElapsed, callback.TargetTime))
        {
          callback.Callback?.Invoke();
          callback.HasBeenInvoked = true;
          _fixedScheduledCallbacks[i] = callback;
        }
      }
    }

    static bool HasReached(float elapsed, float target)
    {
      return elapsed >= target || Mathf.Approximately(elapsed, target);
    }

    internal bool CheckTransitions()
    {
      // Debug.Log("Check transitions");
      StateUnit targetState;
      for (var i = 0; i < Transitions.Count; i++)
        if (Transitions[i].CheckTransition(DeltaTimeSinceStart, out targetState))
        {
          BaseGraph.StartState(targetState);
          return true;
        }

      return false;
    }

    internal bool CheckFixedTransitions()
    {
      StateUnit targetState;
      for (var i = 0; i < FixedTransitions.Count; i++)
        if (FixedTransitions[i].CheckTransition(FixedElapsed, out targetState))
        {
          BaseGraph.StartState(targetState);
          return true;
        }

      return false;
    }

    internal void RebaseUpdateClock()
    {
      _previousTime = Time.time;
      _graphHost.RebaseUpdateClocks();
    }

    internal void Exit()
    {
      // Exit all hosted graphs via GraphHost
      _graphHost.ExitAllGraphs();

      ExitStateFunction?.Invoke();
    }

    internal void ClearSubscriptions()
    {
      // Unsubscribe EventBus listeners
      foreach (var listener in _stateUnitEventBusListeners) listener.Unsubscribe();
      _stateUnitEventBusListeners.Clear();

      // Clear callback lists to prevent memory leaks
      _scheduledCallbacks.Clear();
      _periodicCallbacks.Clear();
      _fixedScheduledCallbacks.Clear();
      _fixedPeriodicCallbacks.Clear();

      // Dispose GraphHost to clean up all hosted graphs
      _graphHost.Dispose();
    }
  }
}
