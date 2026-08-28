using System;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib.Transition
{
  /// <summary>
  /// Provides static methods to connect state transitions to EventBus events.
  /// These transitions are push-based and occur immediately when a relevant event is raised,
  /// bypassing the typical polling CheckTransition loop.
  /// Each StateGraph accepts at most one event-driven transition for a RaiseUniqueId.
  /// The guard runs before predicates and is local to the graph; it does not stop event propagation.
  /// </summary>
  public static class TransitionByEvent // Kept static as per original design
  {
    static void TryTransition<T>(
      StateUnit sourceUnit,
      StateUnit targetUnit,
      T @event,
      Func<T, bool> predicate = null) where T : BusEvent
    {
      var graph = sourceUnit.BaseGraph;
      if (graph == null
          || !graph.IsUnitActive(sourceUnit)
          || !graph.CanProcessEventRaise(@event.RaiseUniqueId))
        return;

      if (predicate != null && !predicate(@event)) return;
      graph.StartState(targetUnit, @event.RaiseUniqueId);
    }

    static void TryTransition<T>(
      StateGraph graphContext,
      StateUnit targetUnit,
      T @event,
      Func<T, bool> predicate = null) where T : BusEvent
    {
      if (!graphContext.IsGraphActive
          || !graphContext.CanProcessEventRaise(@event.RaiseUniqueId))
        return;

      if (predicate != null && !predicate(@event)) return;
      graphContext.StartState(targetUnit, @event.RaiseUniqueId);
    }

    // --- Transitions from a specific StateUnit ---
    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit) where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      // Subscribe to Global EventBus
      IIListener globalHandle = EventBus<T>.Listen(
        @event => TryTransition(sourceUnit, targetUnit, @event)
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (sourceUnit.BaseGraph.LocalEventBus != null)
      {
        IIListener localHandle = sourceUnit.BaseGraph.LocalEventBus.On<T>().Listen(
          @event => TryTransition(sourceUnit, targetUnit, @event)
        );
        sourceUnit.BaseGraph.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      // Subscribe to Global EventBus
      IIListener globalHandle = EventBus<T>.Listen(
        @event => TryTransition(sourceUnit, targetUnit, @event, predicate)
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (sourceUnit.BaseGraph.LocalEventBus != null)
      {
        IIListener localHandle = sourceUnit.BaseGraph.LocalEventBus.On<T>().Listen(
          @event => TryTransition(sourceUnit, targetUnit, @event, predicate)
        );
        sourceUnit.BaseGraph.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, EventQuery<T> query)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      // Subscribe to Global EventBus with query
      IIListener globalHandle = query.Listen(
        @event => TryTransition(sourceUnit, targetUnit, @event)
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (sourceUnit.BaseGraph.LocalEventBus != null)
      {
        IIListener localHandle = sourceUnit.BaseGraph.LocalEventBus.On<T>().Listen(
          @event => TryTransition(sourceUnit, targetUnit, @event)
        );
        sourceUnit.BaseGraph.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateUnit sourceUnit, StateUnit targetUnit, EventQuery<T> query, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));
      if (sourceUnit.BaseGraph == null) throw new InvalidOperationException("SourceUnit must be associated with a StateGraph.");

      // Subscribe to Global EventBus with query and predicate
      IIListener globalHandle = query.Listen(
        @event => TryTransition(sourceUnit, targetUnit, @event, predicate)
      );
      sourceUnit.BaseGraph.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (sourceUnit.BaseGraph.LocalEventBus != null)
      {
        IIListener localHandle = sourceUnit.BaseGraph.LocalEventBus.On<T>().Listen(
          @event => TryTransition(sourceUnit, targetUnit, @event, predicate)
        );
        sourceUnit.BaseGraph.RegisterEventTransitionListener(localHandle);
      }
    }

    // --- Transitions from Any State within a StateGraph ---
    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit) where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));

      // Subscribe to Global EventBus
      IIListener globalHandle = EventBus<T>.Listen(
        @event => TryTransition(graphContext, targetUnit, @event)
      );
      graphContext.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (graphContext.LocalEventBus != null)
      {
        IIListener localHandle = graphContext.LocalEventBus.On<T>().Listen(
          @event => TryTransition(graphContext, targetUnit, @event)
        );
        graphContext.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));

      // Subscribe to Global EventBus
      IIListener globalHandle = EventBus<T>.Listen(
        @event => TryTransition(graphContext, targetUnit, @event, predicate)
      );
      graphContext.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (graphContext.LocalEventBus != null)
      {
        IIListener localHandle = graphContext.LocalEventBus.On<T>().Listen(
          @event => TryTransition(graphContext, targetUnit, @event, predicate)
        );
        graphContext.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, EventQuery<T> query)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));

      // Subscribe to Global EventBus with query
      IIListener globalHandle = query.Listen(
        @event => TryTransition(graphContext, targetUnit, @event)
      );
      graphContext.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (graphContext.LocalEventBus != null)
      {
        IIListener localHandle = graphContext.LocalEventBus.On<T>().Listen(
          @event => TryTransition(graphContext, targetUnit, @event)
        );
        graphContext.RegisterEventTransitionListener(localHandle);
      }
    }

    public static void Connect<T>(StateGraph graphContext, StateUnit targetUnit, EventQuery<T> query, Func<T, bool> predicate)
      where T : BusEvent
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      if (query == null) throw new ArgumentNullException(nameof(query));
      if (predicate == null) throw new ArgumentNullException(nameof(predicate));

      // Subscribe to Global EventBus with query and predicate
      IIListener globalHandle = query.Listen(
        @event => TryTransition(graphContext, targetUnit, @event, predicate)
      );
      graphContext.RegisterEventTransitionListener(globalHandle);

      // Subscribe to Local EventBus if available
      if (graphContext.LocalEventBus != null)
      {
        IIListener localHandle = graphContext.LocalEventBus.On<T>().Listen(
          @event => TryTransition(graphContext, targetUnit, @event, predicate)
        );
        graphContext.RegisterEventTransitionListener(localHandle);
      }
    }
  }
}
