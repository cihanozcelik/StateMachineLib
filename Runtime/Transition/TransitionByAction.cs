using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class TransitionByAction
  {
    public static void Connect(StateUnit baseUnit, StateUnit targetUnit, ref Action signal)
    {
      signal += () =>
      {
        if (baseUnit.IsActive()) baseUnit.BaseGraph.StartState(targetUnit);
      };
    }

    public static void Connect<TActionParam>(StateUnit baseUnit, StateUnit targetUnit, ref Action<TActionParam> signal)
    {
        signal += (param) =>
        {
            if (baseUnit.IsActive()) baseUnit.BaseGraph.StartState(targetUnit);
        };
    }
  }
}