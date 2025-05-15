namespace Nopnag.StateMachineLib.Transition
{
  public class DirectTransition : IStateTransition
  {
    public StateUnit TargetUnit { get; private set; }
    public string TargetUnitName { get; private set; }
    public string SourceUnitName { get; private set; }

    internal DirectTransition(StateUnit sourceUnit, StateUnit targetUnit, bool isAnySource = false)
    {
        TargetUnit = targetUnit ?? throw new System.ArgumentNullException(nameof(targetUnit));
        TargetUnitName = targetUnit.Name ?? "[Unnamed Target]";
        SourceUnitName = isAnySource ? "[Any]" : (sourceUnit?.Name ?? "[Unnamed Source]");
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextState) // elapsedTime is not used but required by interface
    {
      nextState = TargetUnit;
      return true;
    }

    // Connects from a specific state to a target state
    public static void Connect(StateUnit sourceUnit, StateUnit targetUnit)
    {
      if (sourceUnit == null) throw new System.ArgumentNullException(nameof(sourceUnit));
      var transition = new DirectTransition(sourceUnit, targetUnit);
      sourceUnit.Transitions.Add(transition);
    }

    // Connects from Any State to a target state
    public static void Connect(StateGraph graphContext, StateUnit targetUnit)
    {
      if (graphContext == null) throw new System.ArgumentNullException(nameof(graphContext));
      var transition = new DirectTransition(null, targetUnit, isAnySource: true);
      graphContext.AddAnyStateTransition(transition);
    }
  }
}