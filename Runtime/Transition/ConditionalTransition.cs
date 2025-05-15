using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class ConditionalTransition : IStateTransition
  {
    public Func<float, StateUnit> Predicate;

    private ConditionalTransition()
    {
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextTransition)
    {
      nextTransition = Predicate(elapsedTime);
      return nextTransition != null;
    }

    public static ConditionalTransition Connect(StateUnit stateUnit, Func<float, StateUnit> predicate)
    {
      var transition = new ConditionalTransition { Predicate = predicate };
      stateUnit.Transitions.Add(transition);
      return transition;
    }
  }
}