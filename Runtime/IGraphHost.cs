using System.Collections.Generic;
using Nopnag.EventBusLib;

namespace Nopnag.StateMachineLib
{
    /// <summary>
    /// Interface for objects that can host and manage multiple StateGraphs.
    /// Each IGraphHost owns its LocalEventBus and can propagate events to child graphs.
    /// Implemented by StateMachine, StateGraph, and StateUnit.
    /// </summary>
    public interface IGraphHost
    {
        /// <summary>
        /// Read-only collection of all hosted StateGraphs.
        /// </summary>
        IReadOnlyList<StateGraph> HostedGraphs { get; }
        
        /// <summary>
        /// The LocalEventBus instance owned by this host.
        /// Used internally for event subscription and propagation.
        /// </summary>
        LocalEventBus LocalEventBus { get; }
        
        /// <summary>
        /// Raises an event on this host's LocalEventBus and propagates it to all child graphs.
        /// This creates a hierarchical event system where events flow from parent to children.
        /// </summary>
        /// <typeparam name="T">The event type to raise.</typeparam>
        /// <param name="busEvent">The event to raise and propagate.</param>
        void LocalRaise<T>(T busEvent) where T : BusEvent;
        
        /// <summary>
        /// Adds a StateGraph to be hosted and managed by this host.
        /// The graph will be included in event propagation.
        /// </summary>
        /// <param name="graph">The StateGraph to attach.</param>
        void AttachGraph(StateGraph graph);
        
        /// <summary>
        /// Removes a StateGraph from being hosted by this host.
        /// The graph will be cleaned up and excluded from event propagation.
        /// </summary>
        /// <param name="graph">The StateGraph to detach.</param>
        void DetachGraph(StateGraph graph);
        
        /// <summary>
        /// Updates all hosted graphs by calling their UpdateGraph method.
        /// </summary>
        void UpdateAllGraphs();
        
        /// <summary>
        /// Fixed updates all hosted graphs by calling their FixedUpdateGraph method.
        /// </summary>
        void FixedUpdateAllGraphs();
        
        /// <summary>
        /// Late updates all hosted graphs by calling their LateUpdateGraph method.
        /// </summary>
        void LateUpdateAllGraphs();
        
        /// <summary>
        /// Creates a new StateGraph and attaches it to this host.
        /// The graph will receive its own LocalEventBus and be included in event propagation.
        /// </summary>
        /// <returns>A new StateGraph attached to this host.</returns>
        StateGraph CreateGraph();
    }
}