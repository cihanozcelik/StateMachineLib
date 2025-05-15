using System;

namespace Nopnag.StateMachineLib.Transition
{
  public class ConditionalTransition : IStateTransition
  {
    public Func<float, StateUnit> Predicate { get; private set; }

    // For IStateTransition interface. Target is resolved dynamically.
    public StateUnit TargetUnit => null; 
    public string TargetUnitName => "[Dynamic]"; 
    public string SourceUnitName { get; private set; }

    internal ConditionalTransition(StateUnit sourceUnit, Func<float, StateUnit> predicate, bool isAnySource = false)
    {
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        SourceUnitName = isAnySource ? "[Any]" : (sourceUnit?.Name ?? "[Unnamed Source]");
    }

    public bool CheckTransition(float elapsedTime, out StateUnit nextState)
    {
      nextState = Predicate(elapsedTime);
      return nextState != null;
    }

    // Connects from a specific state to a dynamically resolved target state
    public static ConditionalTransition Connect(StateUnit sourceUnit, Func<float, StateUnit> predicate)
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      var transition = new ConditionalTransition(sourceUnit, predicate);
      sourceUnit.Transitions.Add(transition);
      return transition;
    }

    // Connects from Any State to a dynamically resolved target state
    public static ConditionalTransition Connect(StateGraph graphContext, Func<float, StateUnit> predicate)
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      var transition = new ConditionalTransition(null, predicate, isAnySource: true);
      graphContext.AddAnyStateTransition(transition);
      return transition;
    }
  }
}