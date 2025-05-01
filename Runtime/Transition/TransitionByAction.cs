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
  }
}