using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
  public class StateMachine
  {
    public readonly List<StateGraph> GraphList = new();
    private bool _isStarted = false;

    public StateGraph CreateGraph()
    {
      var stateGraph = new StateGraph();
      GraphList.Add(stateGraph);
      return stateGraph;
    }

    public void Exit()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].ExitGraph();
      _isStarted = false;
    }

    public void FixedUpdateMachine()
    {
      if (!_isStarted)
      {
        Start();
      }
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].FixedUpdateGraph();
    }

    public void LateUpdateMachine()
    {
      if (!_isStarted)
      {
        Start();
      }
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].LateUpdateGraph();
    }

    public void RemoveGraph(StateGraph graph)
    {
      GraphList.Remove(graph);
    }

    public void Start()
    {
      if (!_isStarted)
      {
        for (var i = 0; i < GraphList.Count; i++) GraphList[i].EnterGraph();
        _isStarted = true;
      }
    }

    public void UpdateMachine()
    {
      if (!_isStarted)
      {
        Start();
      }
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].UpdateGraph();
    }
  }
}