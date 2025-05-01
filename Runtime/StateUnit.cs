using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using UnityEngine;

namespace Nopnag.StateMachineLib
{
  public class StateUnit
  {
    public readonly StateGraph BaseGraph;
    public Action EnterStateFunction;

    public Action ExitStateFunction;
    public Action<float> FixedUpdateStateFunction;

    public Action<float> LateUpdateStateFunction;
    public readonly string Name;
    public readonly List<IStateTransition> Transitions = new();
    public Action<float> UpdateStateBeforeTransitionCheckFunction;
    public Action<float> UpdateStateFunction;

    float _previousTime;
    StateGraph _subGraph;

    public StateUnit(string name, StateGraph graph)
    {
      Name = name;
      BaseGraph = graph;
    }

    public float DeltaTimeSinceStart { get; private set; }

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

      CheckTransitions();
    }

    public bool Update()
    {
      DeltaTimeSinceStart += (Time.time - _previousTime) * 1; // timescale here
      _previousTime = Time.time;
      UpdateStateBeforeTransitionCheckFunction?.Invoke(DeltaTimeSinceStart);
      if (CheckTransitions()) return false;

      UpdateStateFunction?.Invoke(DeltaTimeSinceStart);
      _subGraph?.UpdateGraph();
      return true;
    }
  }
}