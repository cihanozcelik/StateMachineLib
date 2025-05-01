namespace Nopnag.StateMachineLib.Transition
{
  public interface IStateTransition
  {
    bool CheckTransition(float elapsedTime, out StateUnit nextTransition);
  }
}