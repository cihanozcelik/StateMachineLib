namespace Nopnag.StateMachineLib.Transition
{
  public interface IStateTransition
  {
    StateUnit TargetUnit { get; }
    string TargetUnitName { get; }
    string SourceUnitName { get; }
    bool CheckTransition(float elapsedTime, out StateUnit nextState);
  }
}