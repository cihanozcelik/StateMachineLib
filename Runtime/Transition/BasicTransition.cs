using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class BasicTransition : IStateTransition
  {
    public Func<float, bool> Predicate;
    public StateUnit TargetStateInfo;

    BasicTransition()
    {
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextTransition)
    {
      if (Predicate(elapsedTime))
      {
        nextTransition = TargetStateInfo;
        return true;
      }

      nextTransition = null;
      return false;
    }

    public static void Connect(
      StateUnit stateUnit,
      StateUnit targetStateInfo,
      Func<float, bool> predicate
    )
    {
      stateUnit.Transitions.Add(
        new BasicTransition { Predicate = predicate, TargetStateInfo = targetStateInfo }
      );
    }
  }
}