using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using UnityEngine;

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
    public Action EnterStateFunction;

    public Action ExitStateFunction;
    public Action<float> FixedUpdateStateFunction;

    public Action<float> LateUpdateStateFunction;
    public readonly string Name;
    public readonly List<IStateTransition> Transitions = new();
    public Action<float> UpdateStateBeforeTransitionCheckFunction;
    public Action<float> UpdateStateFunction;

    readonly List<ScheduledCallback> _scheduledCallbacks = new();
    readonly List<PeriodicCallback> _periodicCallbacks = new();
    float _previousTime;
    StateGraph _subGraph;

    public StateUnit(string name, StateGraph graph)
    {
      Name = name;
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

    public bool CheckTransitions()
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

    public void Exit()
    {
      _subGraph?.ExitGraph();
      ExitStateFunction?.Invoke();
    }

    public void FixedUpdate()
    {
      FixedUpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.FixedUpdateGraph();
    }

    public StateGraph GetSubStateGraph()
    {
      _subGraph = new StateGraph();
      return _subGraph;
    }

    public bool IsActive()
    {
      return BaseGraph.IsUnitActive(this);
    }

    public void LateUpdate()
    {
      LateUpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.LateUpdateGraph();
    }

    /// <summary>
    /// Subscribes to events of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="listener">The callback to invoke when the event is raised and this state is active.</param>
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
    /// Subscribes to filtered events (via EventQuery) of type T from the EventBus, but only invokes the listener while this state is active.
    /// </summary>
    /// <typeparam name="T">The event type to listen for.</typeparam>
    /// <param name="query">The EventQuery to filter which events to listen for.</param>
    /// <param name="listener">The callback to invoke when the filtered event is raised and this state is active.</param>
    public void Listen<T>(EventQuery<T> query, ListenerDelegate<T> listener) where T : BusEvent
    {
      query.Listen(
        @event =>
        {
          if (IsActive()) listener.Invoke(@event);
        }
      );
    }

    public void ListenSignal<T>(ref Action<T> signal, Action<T> sensor)
    {
      signal += parameter =>
      {
        if (IsActive()) sensor(parameter);
      };
    }

    public void SetSubStateGraph(StateGraph subGraph)
    {
      _subGraph = subGraph;
    }

    public void Start()
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

    public bool Update()
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
  }
}