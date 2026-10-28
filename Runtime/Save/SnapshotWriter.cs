using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
  // Collects graphs and states in save order while a save walks the topology.
  internal sealed class SnapshotWriter
  {
    public readonly List<SavedGraph> Graphs = new();
    public readonly List<SavedState> States = new();
  }

  // Walks a snapshot in save order while a load walks the same topology.
  internal struct SnapshotReader
  {
    public SavedGraph[] Graphs;
    public SavedState[] States;
    public int          NextGraph;
    public int          NextState;

    public SnapshotReader(SavedGraph[] graphs, SavedState[] states)
    {
      Graphs    = graphs;
      States    = states;
      NextGraph = 0;
      NextState = 0;
    }

    // The snapshot must hold exactly the topology that was walked.
    public void RequireFullyRead()
    {
      GraphHost.RequireCount(Graphs.Length, NextGraph, "graphs");
      GraphHost.RequireCount(States.Length, NextState, "states");
    }
  }
}
