using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class ConditionalTransition : IStateTransition
  {
    public Func<float, StateUnit> Predicate;

    ConditionalTransition()
    {
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextTransition)
    {
      nextTransition = Predicate(elapsedTime);
      return nextTransition != null;
    }

    public static void Connect(StateUnit stateUnit, Func<float, StateUnit> predicate)
    {
      stateUnit.Transitions.Add(new ConditionalTransition { Predicate = predicate });
    }
  }
}