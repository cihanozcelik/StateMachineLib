using System;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib.Transition
{
  public class TransitionByEvent
  {
    public static void Connect<T>(StateUnit baseUnit, StateUnit targetUnit) where T : BusEvent
    {
      EventBus<T>.Listen(
        @event =>
        {
          if (baseUnit.IsActive()) baseUnit.BaseGraph.StartState(targetUnit);
        }
      );
    }

    public static void Connect<T>(StateUnit baseUnit, StateUnit targetUnit, Func<T, bool> predicate)
      where T : BusEvent
    {
      EventBus<T>.Listen(
        @event =>
        {
          if (baseUnit.IsActive() && predicate(@event)) baseUnit.BaseGraph.StartState(targetUnit);
        }
      );
    }
  }
}