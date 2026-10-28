using System;

namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// A <see cref="StateMachine"/>'s runtime state. Plain, non-recursive data, so any
  /// serializer can store it. It loads into a machine built with the same topology.
  /// <see cref="Graphs"/> and <see cref="States"/> list every graph and state at every
  /// depth in the library's save order: a graph, then each of its states followed by
  /// that state's hosted graphs, then the graph's own hosted graphs.
  /// </summary>
  [Serializable]
  public sealed class StateMachineSnapshot
  {
    public bool         Started;
    public bool         TurnedOn;
    public int          RootGraphCount;
    public SavedGraph[] Graphs;
    public SavedState[] States;
  }
}
