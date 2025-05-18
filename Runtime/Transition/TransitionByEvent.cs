using System;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib.Transition
{
  /// <summary>
  /// Provides static methods to connect state transitions to EventBus events.
  /// These transitions are push-based and occur immediately when a relevant event is raised,
  /// bypassing the typical polling CheckTransition loop.
  /// </summary>
  public static class TransitionByEvent // Kept static as per original design
  {
    // --- Transitions from a specific StateUnit ---
    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit) where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      IIListener handle = EventBus<T>.Listen(
        @event =>
        {
          if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit)) 
          {
            sourceUnit.BaseGraph.StartState(targetUnit);
          }
        }
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      IIListener handle = EventBus<T>.Listen(
        @event =>
        {
          if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit) && predicate(@event)) 
          {
            sourceUnit.BaseGraph.StartState(targetUnit);
          }
        }
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, EventQuery<T> query)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      IIListener handle = query.Listen(
        @event =>
        {
          if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit)) 
          {
            sourceUnit.BaseGraph.StartState(targetUnit);
          }
        }
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, EventQuery<T> query, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      IIListener handle = query.Listen(
        @event =>
        {
          if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit) && predicate(@event)) 
          {
            sourceUnit.BaseGraph.StartState(targetUnit);
          }
        }
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(handle);
    }

    // --- Transitions from Any State within a StateGraph ---
    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit) where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));

      IIListener handle = EventBus<T>.Listen(
        @event =>
        {
          if (graphContext.IsGraphActive) 
          {
            graphContext.StartState(targetUnit);
          }
        }
      );
      graphContext.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));

      IIListener handle = EventBus<T>.Listen(
        @event =>
        {
          if (graphContext.IsGraphActive && predicate(@event)) 
          {
            graphContext.StartState(targetUnit);
          }
        }
      );
      graphContext.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, EventQuery<T> query)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));

      IIListener handle = query.Listen(
        @event =>
        {
          if (graphContext.IsGraphActive) 
          {
            graphContext.StartState(targetUnit);
          }
        }
      );
      graphContext.RegisterEventTransitionListener(handle);
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, EventQuery<T> query, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));

      IIListener handle = query.Listen(
        @event =>
        {
          if (graphContext.IsGraphActive && predicate(@event)) 
          {
            graphContext.StartState(targetUnit);
          }
        }
      );
      graphContext.RegisterEventTransitionListener(handle);
    }
  }
}