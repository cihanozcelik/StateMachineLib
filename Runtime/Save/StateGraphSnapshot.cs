using System;

namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// A <see cref="StateGraph"/>'s runtime state with every hosted graph at every
  /// depth, in the order <see cref="StateMachineSnapshot"/> describes. The first
  /// graph is the saved graph itself.
  /// </summary>
  [Serializable]
  public sealed class StateGraphSnapshot
  {
    public SavedGraph[] Graphs;
    public SavedState[] States;
  }
}
