namespace Nopnag.StateMachineLib.Transition
{
  public class DirectTransition : IStateTransition
  {
    public StateUnit TargetStateInfo;

    DirectTransition()
    {
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextTransition)
    {
      nextTransition = TargetStateInfo;
      return true;
    }

    public static void Connect(StateUnit stateUnit, StateUnit targetStateInfo)
    {
      stateUnit.Transitions.Add(new DirectTransition { TargetStateInfo = targetStateInfo });
    }
  }
}