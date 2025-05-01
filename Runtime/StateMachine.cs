using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
  public class StateMachine
  {
    public readonly List<StateGraph> GraphList = new();

    public StateGraph CreateGraph()
    {
      var stateGraph = new StateGraph();
      GraphList.Add(stateGraph);
      return stateGraph;
    }

    public void Exit()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].ExitGraph();
    }

    public void FixedUpdateMachine()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].FixedUpdateGraph();
    }

    public void LateUpdateMachine()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].LateUpdateGraph();
    }

    public void RemoveGraph(StateGraph graph)
    {
      GraphList.Remove(graph);
    }

    public void Start()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].EnterGraph();
    }

    public void UpdateMachine()
    {
      for (var i = 0; i < GraphList.Count; i++) GraphList[i].UpdateGraph();
    }
  }
}