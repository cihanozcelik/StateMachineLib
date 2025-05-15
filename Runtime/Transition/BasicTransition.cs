using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class BasicTransition : IStateTransition
  {
    public Func<float, bool> Predicate { get; private set; }
    public StateUnit TargetUnit { get; private set; }
    public string TargetUnitName { get; private set; }
    public string SourceUnitName { get; private set; }

    internal BasicTransition(StateUnit sourceUnit, StateUnit targetUnit, Func<float, bool> predicate, bool isAnySource = false)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        TargetUnit = targetUnit ?? throw new ArgumentNullException(nameof(targetUnit));
        TargetUnitName = targetUnit.Name ?? "[Unnamed Target]"; // Assuming StateUnit has a Name property
        SourceUnitName = isAnySource ? "[Any]" : (sourceUnit?.Name ?? "[Unnamed Source]");
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextState)
    {
      if (Predicate(elapsedTime))
      {
        nextState = TargetUnit;
        return true;
      }

      nextState = null;
      return false;
    }

    // Connects from a specific state to a target state
    public static void Connect(
      StateUnit sourceUnit,
      StateUnit targetUnit,
      Func<float, bool> predicate
    )
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      var transition = new BasicTransition(sourceUnit, targetUnit, predicate);
      sourceUnit.Transitions.Add(transition); // Changed AddTransition to Transitions.Add
    }

    // Connects from Any State to a target state
    public static void Connect(
      StateGraph graphContext,
      StateUnit targetUnit,
      Func<float, bool> predicate
    )
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      var transition = new BasicTransition(null, targetUnit, predicate, isAnySource: true);
      graphContext.AddAnyStateTransition(transition);
    }
  }
}