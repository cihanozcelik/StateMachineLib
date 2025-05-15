using System;

namespace Nopnag.StateMachineLib.Transition
{
  /// <summary>
  /// Provides static methods to connect state transitions to C# Actions (signals).
  /// These transitions are push-based and occur immediately when the signal is invoked,
  /// bypassing the typical polling CheckTransition loop.
  /// </summary>
  public static class TransitionByAction // Kept static as per original design
  {
    /// <summary>
    /// Connects a transition from a specific source state to a target state, triggered by a parameterless Action.
    /// </summary>
    public static void Connect(StateUnit sourceUnit, StateUnit targetUnit, ref Action signal)
    {
      if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));
      
      signal += () =>
      {
        // Check if the source unit is currently active in its graph when the signal fires.
        // Accessing sourceUnit.BaseGraph safely.
        if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit)) 
        {
            sourceUnit.BaseGraph.StartState(targetUnit);
        }
      };
    }

    /// <summary>
    /// Connects a transition from any state within the graph to a target state, triggered by a parameterless Action.
    /// </summary>
    public static void Connect(StateGraph graphContext, StateUnit targetUnit, ref Action signal)
    {
      if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
      if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));

      signal += () =>
      {
        if (graphContext.IsGraphActive) // Check if the graph itself is active
        {
            graphContext.StartState(targetUnit);
        }
      };
    }

    /// <summary>
    /// Connects a transition from a specific source state to a target state, triggered by an Action with one parameter.
    /// </summary>
    public static void Connect<TActionParam>(StateUnit sourceUnit, StateUnit targetUnit, ref Action<TActionParam> signal)
    {
        if (sourceUnit == null) throw new ArgumentNullException(nameof(sourceUnit));
        if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));

        signal += (param) =>
        {
            if (sourceUnit.BaseGraph != null && sourceUnit.BaseGraph.IsUnitActive(sourceUnit))
            {
                sourceUnit.BaseGraph.StartState(targetUnit);
            }
        };
    }

    /// <summary>
    /// Connects a transition from any state within the graph to a target state, triggered by an Action with one parameter.
    /// </summary>
    public static void Connect<TActionParam>(StateGraph graphContext, StateUnit targetUnit, ref Action<TActionParam> signal)
    {
        if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
        if (targetUnit == null) throw new ArgumentNullException(nameof(targetUnit));

        signal += (param) =>
        {
            if (graphContext.IsGraphActive)
            {
                graphContext.StartState(targetUnit);
            }
        };
    }
  }
}