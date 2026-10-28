using System;

namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// One graph in a snapshot. States are identified by their index in
  /// <see cref="StateGraph.States"/>; -1 is none. The counts record the topology the
  /// snapshot was saved from.
  /// </summary>
  [Serializable]
  public sealed class SavedGraph
  {
    public bool TurnedOn;
    public int  Initial;
    public int  Current;
    public int  StateCount;
    public int  HostedGraphCount;
  }
}
