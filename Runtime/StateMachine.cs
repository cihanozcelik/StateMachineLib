using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib
{
  public class StateMachine : IGraphHost, IDisposable, IPoweredNode,
    ISaveable<StateMachineSnapshot>
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

    /// <summary>
    /// Fixed-updates all hosted graphs with an explicit delta without starting the
    /// state machine. Prefer FixedUpdateMachine(float) for normal driving code.
    /// </summary>
    public void FixedUpdateAllGraphs(float fixedDeltaTime)
    {
      _graphHost.FixedUpdateAllGraphs(fixedDeltaTime);
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
      var wasActive = IsActive;
      _poweredNode.SetTurnedOn(on);
      if (!wasActive && IsActive) _graphHost.RebaseUpdateClocks();
    }

    public void UpdateAllGraphs()
    {
      _graphHost.UpdateAllGraphs();
    }

    /// <summary>
    /// Updates all hosted graphs with an explicit delta without starting the state
    /// machine. Prefer UpdateMachine(float) for normal driving code.
    /// </summary>
    public void UpdateAllGraphs(float deltaTime)
    {
      _graphHost.UpdateAllGraphs(deltaTime);
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

    /// <summary>
    /// Fixed-updates all graphs with an explicit delta for deterministic simulation.
    /// Existing parameterless callers remain supported.
    /// </summary>
    public void FixedUpdateMachine(float fixedDeltaTime)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      FixedUpdateAllGraphs(fixedDeltaTime);
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

    /// <summary>
    /// Saves the machine's runtime state: started flag, power switch and every hosted
    /// graph at every depth. The owner's own variables are not included; the owner
    /// saves them beside this snapshot.
    /// </summary>
    public StateMachineSnapshot Save()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      var writer = new SnapshotWriter();
      _graphHost.Save(writer);
      return new StateMachineSnapshot
      {
        Started        = _isStarted,
        TurnedOn       = IsTurnedOn,
        RootGraphCount = _graphHost.HostedGraphCount,
        Graphs         = writer.Graphs.ToArray(),
        States         = writer.States.ToArray()
      };
    }

    /// <summary>
    /// Loads a snapshot saved from a machine with the same topology, started or not.
    /// The whole snapshot is compared with the topology first: a mismatch throws
    /// InvalidOperationException and changes nothing. No OnEnter or OnExit runs and
    /// nothing is evaluated. A loaded started machine is
    /// not entered again by Start or a lazy-starting tick.
    /// </summary>
    public void Load(StateMachineSnapshot snapshot)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      GraphHost.RequireCount(snapshot.RootGraphCount, _graphHost.HostedGraphCount, "graphs");
      var check = new SnapshotReader(snapshot.Graphs, snapshot.States);
      _graphHost.Check(ref check);
      check.RequireFullyRead();

      _isStarted = snapshot.Started;
      _poweredNode.SetTurnedOn(snapshot.TurnedOn);
      var reader = new SnapshotReader(snapshot.Graphs, snapshot.States);
      _graphHost.Load(ref reader);
    }

    public void UpdateMachine()
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      UpdateAllGraphs();
    }

    /// <summary>
    /// Updates all graphs with an explicit delta for deterministic simulation.
    /// Existing parameterless callers retain their wall-clock behavior.
    /// </summary>
    public void UpdateMachine(float deltaTime)
    {
      if (_isDisposed) throw new ObjectDisposedException(nameof(StateMachine));
      if (!_isStarted) Start();
      UpdateAllGraphs(deltaTime);
    }
  }
}
