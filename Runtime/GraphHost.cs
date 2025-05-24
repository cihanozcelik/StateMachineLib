using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// Internal implementation of IGraphHost used by StateMachine and StateUnit through composition.
  /// Manages multiple StateGraphs and provides LocalEventBus support.
  /// </summary>
  internal class GraphHost : IGraphHost
  {
    readonly List<StateGraph> _hostedGraphs = new();
    bool                      _isDisposed   = false;
    readonly LocalEventBus    _localEventBus;

    /// <summary>
    /// Creates a new GraphHost with its own LocalEventBus.
    /// </summary>
    public GraphHost()
    {
      _localEventBus = new LocalEventBus();
    }

    public void AttachGraph(StateGraph graph)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));
      if (graph == null) throw new ArgumentNullException(nameof(graph));
      if (_hostedGraphs.Contains(graph)) return; // Already hosted

      ((IPoweredNode)graph).SetTurnedOn(true); // Re-activate the graph
      graph.ClearDisposedByParentFlagInternal(); 
      _hostedGraphs.Add(graph);
      // Note: Each StateGraph now owns its own LocalEventBus, no need to set it
    }

    /// <summary>
    /// Creates a new StateGraph and attaches it to this host.
    /// The graph will receive the host's LocalEventBus reference.
    /// </summary>
    /// <returns>A new StateGraph attached to this host.</returns>
    public StateGraph CreateGraph()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      var stateGraph = new StateGraph();
      AttachGraph(stateGraph);
      return stateGraph;
    }

    public void DetachGraph(StateGraph graph)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));
      if (graph == null) return;

      if (_hostedGraphs.Remove(graph))
      {
        // Clean up the graph
        ((IPoweredNode)graph).SetTurnedOn(false); // Effectively deactivates the graph
        // We don't call ExitGraph or MarkAsDisposed, as Detach is temporary.
      }
    }

    public void FixedUpdateAllGraphs()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      for (var i = 0; i < _hostedGraphs.Count; i++) _hostedGraphs[i].FixedUpdateGraph();
    }

    public IReadOnlyList<StateGraph> HostedGraphs => _hostedGraphs.AsReadOnly();

    /// <summary>
    /// Late updates all hosted graphs by calling their LateUpdateGraph method.
    /// </summary>
    public void LateUpdateAllGraphs()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      for (var i = 0; i < _hostedGraphs.Count; i++) _hostedGraphs[i].LateUpdateGraph();
    }

    public LocalEventBus LocalEventBus => _localEventBus;

    /// <summary>
    /// Raises an event on the LocalEventBus and propagates it to all hosted graphs.
    /// </summary>
    /// <typeparam name="T">The event type to raise.</typeparam>
    /// <param name="busEvent">The event to raise.</param>
    public void LocalRaise<T>(T busEvent) where T : BusEvent
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      // Raise on own LocalEventBus
      _localEventBus.Raise(busEvent);

      // Propagate to all hosted subgraphs
      for (var i = 0; i < _hostedGraphs.Count; i++) _hostedGraphs[i].LocalRaise(busEvent);
    }

    public void UpdateAllGraphs()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      for (var i = 0; i < _hostedGraphs.Count; i++) _hostedGraphs[i].UpdateGraph();
    }

    /// <summary>
    /// Disposes the GraphHost and all its hosted graphs.
    /// </summary>
    public void Dispose()
    {
      if (_isDisposed) return;

      DisposeAllGraphs();
      _isDisposed = true;
    }

    /// <summary>
    /// Exits all hosted graphs without disposing them.
    /// Graphs remain attached and can be restarted.
    /// </summary>
    public void ExitAllGraphs()
    {
      if (_isDisposed) return;

      for (var i = 0; i < _hostedGraphs.Count; i++)
      {
        _hostedGraphs[i].ExitGraph();
      }
    }

    /// <summary>
    /// Disposes all hosted graphs and clears the list.
    /// This is called during disposal only.
    /// </summary>
    void DisposeAllGraphs()
    {
      for (var i = 0; i < _hostedGraphs.Count; i++)
      {
        _hostedGraphs[i].ExitGraph();      
        _hostedGraphs[i].MarkAsDisposed(); 
      }

      _hostedGraphs.Clear();
    }

    /// <summary>
    /// Starts all hosted graphs by calling their EnterGraph method.
    /// </summary>
    public void StartAllGraphs()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(GraphHost));

      for (var i = 0; i < _hostedGraphs.Count; i++) _hostedGraphs[i].EnterGraph();
    }
  }
}