using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class ConditionalTransitionByIndex : IStateTransition
  {
    public Func<float, int> Predicate;
    public StateUnit[] TargetStateInfos;

    ConditionalTransitionByIndex()
    {
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextTransition)
    {
      var index = Predicate(elapsedTime);
      if (index > -1)
      {
        nextTransition = TargetStateInfos[index];
        return true;
      }

      nextTransition = null;
      return false;
    }

    public static void Connect(
      StateUnit stateUnit,
      StateUnit[] targetStateInfos,
      Func<float, int> predicate
    )
    {
      stateUnit.Transitions.Add(
        new ConditionalTransitionByIndex
        {
          Predicate = predicate, TargetStateInfos = targetStateInfos
        }
      );
    }
  }
}