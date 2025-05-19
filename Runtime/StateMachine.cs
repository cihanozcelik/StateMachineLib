using System;
using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
  public class StateMachine : IDisposable
  {
    public readonly List<StateGraph> GraphList   = new();
    bool                             _isDisposed = false;
    bool                             _isStarted  = false;

    public void Dispose()
    {
      if (_isDisposed) return;

      // Exit all graphs and mark them as disposed internally first
      for (var i = 0; i < GraphList.Count; i++)
      {
        GraphList[i].ExitGraph();      // Ensure listeners are unsubscribed etc.
        GraphList[i].MarkAsDisposed(); // Mark the graph itself as unusable
      }
      // GraphList.Clear(); // Optionally clear the list after disposing graphs

      _isStarted  = false; // Ensure StateMachine itself is also marked as not started
      _isDisposed = true;
    }

    public StateGraph CreateGraph()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      var stateGraph = new StateGraph();
      GraphList.Add(stateGraph);
      return stateGraph;
    }

    public void Exit()
    {
      if (_isDisposed) return;

      for (var i = 0; i < GraphList.Count; i++) GraphList[i].ExitGraph();
      _isStarted = false;
    }

    public void FixedUpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].FixedUpdateGraph();
    }

    public void LateUpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].LateUpdateGraph();
    }

    public void RemoveGraph(StateGraph graph)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      graph.MarkAsDisposed();
      GraphList.Remove(graph);
    }

    public void Start()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted)
      {
        for (var i = 0; i < GraphList.Count; i++) GraphList[i].EnterGraph();
        _isStarted = true;
      }
    }

    public void UpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].UpdateGraph();
    }
  }
}