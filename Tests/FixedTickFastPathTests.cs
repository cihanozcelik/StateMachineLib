using System;
using Nopnag.StateMachineLib.Transition;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nopnag.StateMachineLib.Tests
{
  public sealed class ManagedAllocationTestOwner : MonoBehaviour
  {
  }

  public class FixedTickFastPathTests
  {
    sealed class NeverTransition : IStateTransition
    {
      public StateUnit TargetUnit => null;
      public string TargetUnitName => "[Never]";
      public string SourceUnitName => "[Test]";

      public bool CheckTransition(float elapsedTime, out StateUnit nextState)
      {
        nextState = null;
        return false;
      }
    }

    [Test]
    public void EmptyFixedTopologyAdvancesClockWithoutDispatchCapability()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var state = graph.CreateState();
        machine.Start();

        Assert.IsFalse(state.RequiresFixedTickDispatch);

        machine.FixedUpdateMachine(0.02f);

        Assert.AreEqual(0.02f, state.FixedDeltaTime, 0.0001f);
        Assert.AreEqual(0.02f, state.FixedElapsed, 0.0001f);
        Assert.IsFalse(state.RequiresFixedTickDispatch);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void FixedCapabilitiesTrackCallbacksTimersAndTransitions()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var state = graph.CreateState();
        var target = graph.CreateState();

        Assert.IsFalse(state.RequiresFixedTickDispatch);

        state.OnFixedTick = (_, __) => { };
        Assert.IsTrue(state.RequiresFixedTickDispatch);
        state.OnFixedTick = null;
        Assert.IsFalse(state.RequiresFixedTickDispatch);

#pragma warning disable CS0618
        var legacyCalls = 0;
        state.FixedUpdateStateFunction = _ => legacyCalls++;
        Assert.IsTrue(state.RequiresFixedTickDispatch);
        machine.Start();
        machine.FixedUpdateMachine(0.02f);
        Assert.AreEqual(1, legacyCalls);
        state.FixedUpdateStateFunction = null;
        Assert.IsFalse(state.RequiresFixedTickDispatch);
#pragma warning restore CS0618

        state.AtFixed(0f, () => { });
        Assert.IsTrue(state.RequiresFixedTickDispatch);
        machine.FixedUpdateMachine(0.02f);
        Assert.IsFalse(state.RequiresFixedTickDispatch,
          "A completed one-shot fixed timer should leave the fast path eligible.");

        state.AtEveryFixed(1f, () => { });
        Assert.IsTrue(state.RequiresFixedTickDispatch);

        var transitionState = target;
        BasicTransition.ConnectFixed(transitionState, state, _ => false);
        Assert.IsTrue(transitionState.RequiresFixedTickDispatch);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void HostedGraphCapabilityOnlyRequiresDispatchWhileAChildIsActive()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var state = graph.CreateState();
        machine.Start();

        var child = state.CreateGraph();
        child.CreateState();
        child.EnterGraph();
        Assert.IsTrue(state.RequiresFixedTickDispatch);

        child.SetTurnedOn(false);
        Assert.IsFalse(state.RequiresFixedTickDispatch);

        child.SetTurnedOn(true);
        Assert.IsTrue(state.RequiresFixedTickDispatch);

        state.DetachGraph(child);
        Assert.IsFalse(state.RequiresFixedTickDispatch);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void DirectFixedTransitionListMutationRemainsSupported()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var state = graph.CreateState();
        state.FixedTransitions.Add(new NeverTransition());

        machine.Start();
        machine.FixedUpdateMachine(0.02f);

        Assert.AreEqual(0.02f, state.FixedElapsed, 0.0001f);
        Assert.IsTrue(state.RequiresFixedTickDispatch);

        state.FixedTransitions.Clear();
        Assert.IsFalse(state.RequiresFixedTickDispatch);

        BasicTransition.ConnectFixed(state, graph.CreateState(), _ => false);
        Assert.IsTrue(state.RequiresFixedTickDispatch);
        state.FixedTransitions.Clear();
        Assert.IsFalse(state.RequiresFixedTickDispatch,
          "Clearing the public list should repair a cached fluent-transition capability.");
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void ManagedWrapperMaximumOwnerSteadyStateDoesNotAllocate()
    {
      const int maximumOwnerCount = 64;
      const int measuredTicks = 100;
      var go = new GameObject("ManagedWrapperAllocationTest");

      try
      {
        var wrapper = StateMachineWrapper.GetOrCreate(go);
        for (var i = 0; i < maximumOwnerCount; i++)
        {
          var owner = go.AddComponent<ManagedAllocationTestOwner>();
          wrapper.CreateStateMachineFor(owner, machine =>
          {
            var graph = machine.CreateGraph();
            graph.CreateState();
          });
        }

        // Warm collection traversal, Unity object checks, Time access, and both tick paths.
        wrapper.UpdateManagedStateMachinesForTesting();
        wrapper.FixedUpdateManagedStateMachinesForTesting();
        GC.GetAllocatedBytesForCurrentThread();

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measuredTicks; i++)
        {
          wrapper.UpdateManagedStateMachinesForTesting();
          wrapper.FixedUpdateManagedStateMachinesForTesting();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        Assert.AreEqual(0, allocated,
          $"Warmed managed wrapper ticks allocated {allocated} bytes for " +
          $"{maximumOwnerCount} owners.");
      }
      finally
      {
        Object.DestroyImmediate(go);
      }
    }
  }
}
