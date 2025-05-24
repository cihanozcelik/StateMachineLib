using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib
{
  public class StateMachine : IGraphHost, IDisposable
  {
    private readonly GraphHost _graphHost;
    
    bool _isDisposed = false;
    bool _isStarted  = false;

    public StateMachine()
    {
      _graphHost = new GraphHost();
    }

    // IGraphHost implementation via composition
    public IReadOnlyList<StateGraph> HostedGraphs => _graphHost.HostedGraphs;
    internal LocalEventBus LocalEventBus => _graphHost.LocalEventBus;
    LocalEventBus IGraphHost.LocalEventBus => LocalEventBus;
    
    public void LocalRaise<T>(T busEvent) where T : BusEvent
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      _graphHost.LocalRaise(busEvent);
    }
    
    public void AttachGraph(StateGraph graph) => _graphHost.AttachGraph(graph);
    public void DetachGraph(StateGraph graph) => _graphHost.DetachGraph(graph);
    public void UpdateAllGraphs() => _graphHost.UpdateAllGraphs();
    public void FixedUpdateAllGraphs() => _graphHost.FixedUpdateAllGraphs();
    public void LateUpdateAllGraphs() => _graphHost.LateUpdateAllGraphs();
    public StateGraph CreateGraph() => _graphHost.CreateGraph();

    public void Dispose()
    {
      if (_isDisposed) return;

      _graphHost.Dispose();
      _isStarted  = false;
      _isDisposed = true;
    }

    public void Exit()
    {
      if (_isDisposed) return;

      _graphHost.ExitAllGraphs();
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

    public void Start()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted)
      {
        _graphHost.StartAllGraphs();
        _isStarted = true;
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