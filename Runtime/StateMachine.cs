using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib
{
  public class StateMachine : IGraphHost, IDisposable, IPoweredNode
  {
    readonly GraphHost _graphHost;

    bool                 _isDisposed = false;
    bool                 _isStarted  = false;
    readonly PoweredNode _poweredNode;

    public StateMachine()
    {
      _graphHost   = new GraphHost();
      _poweredNode = new PoweredNode(true);
    }

    internal LocalEventBus LocalEventBus => _graphHost.LocalEventBus;

    void IPoweredNode.AttachChild(IPoweredNode child)
    {
      _poweredNode.AttachChild(child);
    }

    public void AttachGraph(StateGraph graph)
    {
      _graphHost.AttachGraph(graph);
      // Connect power relationship
      ((IPoweredNode)this).AttachChild(graph);
    }

    public StateGraph CreateGraph()
    {
      var graph = _graphHost.CreateGraph();
      // Connect power relationship for newly created graph
      ((IPoweredNode)this).AttachChild(graph);
      return graph;
    }

    void IPoweredNode.DetachChild(IPoweredNode child)
    {
      _poweredNode.DetachChild(child);
    }

    public void DetachGraph(StateGraph graph)
    {
      _graphHost.DetachGraph(graph);
      // Disconnect power relationship
      ((IPoweredNode)this).DetachChild(graph);
    }

    public void Dispose()
    {
      if (_isDisposed) return;

      _graphHost.Dispose();
      _isStarted  = false;
      _isDisposed = true;
    }

    public void FixedUpdateAllGraphs()
    {
      _graphHost.FixedUpdateAllGraphs();
    }

    // IPoweredNode implementation
    public bool HasPower => _poweredNode.HasPower;

    // IGraphHost implementation via composition
    public IReadOnlyList<StateGraph> HostedGraphs => _graphHost.HostedGraphs;
    public bool                      IsActive     => _poweredNode.IsActive;
    public bool                      IsTurnedOn   => _poweredNode.IsTurnedOn;

    public void LateUpdateAllGraphs()
    {
      _graphHost.LateUpdateAllGraphs();
    }

    LocalEventBus IGraphHost.LocalEventBus => LocalEventBus;

    public void LocalRaise<T>(T busEvent) where T : BusEvent
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      _graphHost.LocalRaise(busEvent);
    }

    void IPoweredNode.RefreshPowerState()
    {
      _poweredNode.RefreshPowerState();
    }

    void IPoweredNode.SetParent(IPoweredNode? parent)
    {
      _poweredNode.SetParent(parent);
    }

    public void SetTurnedOn(bool on)
    {
      _poweredNode.SetTurnedOn(on);
    }

    public void UpdateAllGraphs()
    {
      _graphHost.UpdateAllGraphs();
    }

    public void Exit()
    {
      if (_isDisposed) return;

      _graphHost.ExitAllGraphs();
      _poweredNode.SetTurnedOn(false); // Turn off power
      _isStarted = false;
    }

    public void FixedUpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      FixedUpdateAllGraphs();
    }

    public void LateUpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      LateUpdateAllGraphs();
    }

    public void RemoveGraph(StateGraph graph)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      DetachGraph(graph);
    }

    public void Reset()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      _isStarted = false;
      _poweredNode.SetTurnedOn(false); // Turn off power
      _graphHost.ExitAllGraphs();
    }

    public void Start()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted)
      {
        _isStarted = true;
        _poweredNode.SetTurnedOn(true); // Turn on power
        _graphHost.StartAllGraphs();
      }
    }

    public void UpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      UpdateAllGraphs();
    }
  }
}