using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class ConditionalTransitionByIndex : IStateTransition
  {
    public Func<float, int> Predicate { get; private set; }
    public StateUnit[] TargetStateInfos { get; private set; }

    // IStateTransition implementation
    public StateUnit TargetUnit => null; // No single target unit
    public string TargetUnitName => "[Indexed]";
    public string SourceUnitName { get; private set; }

    internal ConditionalTransitionByIndex(StateUnit sourceUnit, StateUnit[] targetStateInfos, Func<float, int> predicate)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        TargetStateInfos = targetStateInfos ?? throw new ArgumentNullException(nameof(targetStateInfos));
        if (targetStateInfos.Length == 0) throw new ArgumentException("TargetStateInfos array cannot be empty.", nameof(targetStateInfos));
        SourceUnitName = sourceUnit?.Name ?? "[Unnamed Source]"; // sourceUnit can be null if we add an AnyState variant later
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextState) // Renamed parameter
    {
      var index = Predicate(elapsedTime);
      if (index > -1 && index < TargetStateInfos.Length) // Added bounds check for safety
      {
        nextState = TargetStateInfos[index];
        return true;
      }

      nextState = null;
      return false;
    }

    // This Connect method is for transitions from a specific StateUnit.
    // If "Any State to Indexed" is needed later, a new overload taking StateGraph can be added.
    public static void Connect(
      StateUnit sourceUnit,
      StateUnit[] targetStateInfos,
      Func<float, int> predicate
    )
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      var transition = new ConditionalTransitionByIndex(sourceUnit, targetStateInfos, predicate);
      sourceUnit.Transitions.Add(transition);
    }
  }
}