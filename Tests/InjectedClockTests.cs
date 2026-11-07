using System;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nopnag.StateMachineLib.Tests
{
  public sealed class InjectedClockTests
  {
    sealed class Clock : IStateMachineClock
    {
      public double UpdateTime { get; set; }
      public float FixedDeltaTime { get; set; } = 0.125f;
    }

    sealed class Advance : BusEvent { }

    readonly List<StateMachine> _machines = new();
    IStateMachineClock _previousDefault;
    GameObject _object;

    [SetUp]
    public void SetUp()
    {
      _previousDefault = StateMachineClock.Default;
      EventBus.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
      if (_object != null) Object.DestroyImmediate(_object);
      foreach (var machine in _machines) machine.Dispose();
      _machines.Clear();
      StateMachineClock.Default = _previousDefault;
      EventBus.ClearAll();
    }

    [Test]
    public void ExplicitRootsKeepIndependentClocksAtLargeEpochs()
    {
      var a = new Clock { UpdateTime = 1_000_000_000d, FixedDeltaTime = 0.125f };
      var b = new Clock { UpdateTime = 2_000_000_000d, FixedDeltaTime = 0.25f };
      var ma = Machine(a);
      var mb = Machine(b);
      var sa = ma.CreateGraph().CreateState();
      var sb = mb.CreateGraph().CreateState();
      ma.Start();
      mb.Start();

      a.UpdateTime += 0.125d;
      b.UpdateTime += 0.5d;
      ma.UpdateMachine();
      mb.UpdateMachine();
      ma.FixedUpdateMachine();
      mb.FixedUpdateMachine();
      ma.LateUpdateMachine();

      Assert.AreEqual(0.125f, sa.DeltaTimeSinceStart);
      Assert.AreEqual(0.5f, sb.DeltaTimeSinceStart);
      Assert.AreEqual(0.125f, sa.FixedElapsed);
      Assert.AreEqual(0.25f, sb.FixedElapsed);
    }

    [Test]
    public void DefaultIsCapturedAtConstructionAndExplicitClockTakesPrecedence()
    {
      var first = new Clock();
      var second = new Clock();
      StateMachineClock.Default = first;
      var machine = new StateMachine();
      _machines.Add(machine);
      var standalone = new StateGraph();
      var explicitMachine = Machine(second);
      StateMachineClock.Default = second;

      Assert.AreSame(first, machine.Clock);
      Assert.AreSame(first, machine.CreateGraph().Clock);
      Assert.AreSame(first, standalone.Clock);
      Assert.AreSame(second, explicitMachine.Clock);
      StateMachineClock.Reset();
      Assert.AreSame(UnityStateMachineClock.Instance, StateMachineClock.Default);
      Assert.AreSame(first, machine.Clock, "Reset selects the next session's default only.");
    }

    [Test]
    public void UnityDefaultRetainsOriginalTimeInputs()
    {
      Assert.AreEqual((double)Time.time, UnityStateMachineClock.Instance.UpdateTime);
      Assert.AreEqual(Time.fixedDeltaTime, UnityStateMachineClock.Instance.FixedDeltaTime);
    }

    [Test]
    public void EveryGraphDepthInheritsClockAndLocalScale()
    {
      var clock = new Clock { UpdateTime = 20d, FixedDeltaTime = 0.5f };
      var machine = Machine(clock);
      var root = machine.CreateGraph();
      var state = root.CreateState();
      state.LocalTimeScale = 0.5f;
      var stateChild = state.CreateGraph();
      var child = stateChild.CreateState();
      child.LocalTimeScale = 0.5f;
      var graphChild = root.CreateGraph();
      var sibling = graphChild.CreateState();
      machine.Start();
      graphChild.EnterGraph();
      clock.UpdateTime += 1d;
      machine.UpdateMachine();
      machine.FixedUpdateMachine();
      // Graph-hosted graphs have explicit traversal in the existing API.
      root.UpdateAllGraphs();
      root.FixedUpdateAllGraphs();

      Assert.AreSame(clock, stateChild.Clock);
      Assert.AreSame(clock, graphChild.Clock);
      Assert.AreEqual(0.5f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.25f, child.DeltaTimeSinceStart);
      Assert.AreEqual(0.125f, child.FixedElapsed);
      Assert.AreEqual(1f, sibling.DeltaTimeSinceStart);
      Assert.AreEqual(0.5f, sibling.FixedElapsed);
    }

    [Test]
    public void ExplicitGraphTickUsesSelectedClock()
    {
      var clock = new Clock { UpdateTime = 10d };
      var graph = new StateGraph(clock);
      var state = graph.CreateState();
      // Standalone graphs receive power when attached to a powered parent.
      var machine = Machine(clock);
      machine.AttachGraph(graph);
      machine.Start();
      clock.UpdateTime += 0.25d;
      graph.UpdateGraph();
      graph.FixedUpdateGraph();
      Assert.AreEqual(0.25f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.125f, state.FixedElapsed);
    }

    [Test]
    public void ReattachmentRebasesActiveAndInactiveDescendantsWithoutCallbacks()
    {
      var a = new Clock { UpdateTime = 10d };
      var b = new Clock { UpdateTime = 1000d, FixedDeltaTime = 0.25f };
      var ma = Machine(a);
      var mb = Machine(b);
      var graph = ma.CreateGraph();
      var state = graph.CreateState();
      var next = graph.CreateState();
      var childGraph = state.CreateGraph();
      var child = childGraph.CreateState();
      var inactiveGraph = next.CreateGraph();
      var inactiveChild = inactiveGraph.CreateState();
      var enters = 0;
      state.OnEnter = () => enters++;
      ma.Start();
      mb.Start();
      a.UpdateTime += 0.5d;
      ma.UpdateMachine();
      ma.DetachGraph(graph);
      Assert.AreSame(a, graph.Clock);
      mb.AttachGraph(graph);

      Assert.AreSame(b, graph.Clock);
      Assert.AreSame(b, childGraph.Clock);
      Assert.AreSame(b, inactiveGraph.Clock);
      Assert.AreEqual(1, enters);
      b.UpdateTime += 0.125d;
      mb.UpdateMachine();
      Assert.AreEqual(0.625f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.625f, child.DeltaTimeSinceStart);
      graph.StartState(next);
      b.UpdateTime += 0.25d;
      mb.UpdateMachine();
      Assert.AreEqual(0.25f, inactiveChild.DeltaTimeSinceStart);
    }

    [TestCase("machine")]
    [TestCase("graph")]
    [TestCase("state")]
    [TestCase("localScale")]
    public void ResumingRebasesAgainstInjectedTime(string boundary)
    {
      var clock = new Clock { UpdateTime = 10d };
      var machine = Machine(clock);
      var graph = machine.CreateGraph();
      var state = graph.CreateState();
      var child = state.CreateGraph().CreateState();
      machine.Start();
      clock.UpdateTime += 0.25d;
      machine.UpdateMachine();
      Power(false);
      clock.UpdateTime += 100d;
      Power(true);
      clock.UpdateTime += 0.125d;
      machine.UpdateMachine();

      Assert.AreEqual(0.375f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.375f, child.DeltaTimeSinceStart);

      void Power(bool on)
      {
        switch (boundary)
        {
          case "machine": machine.SetTurnedOn(on); break;
          case "graph": graph.SetTurnedOn(on); break;
          case "state": state.SetTurnedOn(on); break;
          case "localScale": state.LocalTimeScale = on ? 1f : 0f; break;
        }
      }
    }

    [Test]
    public void ExplicitTicksRebaseTheNextParameterlessTick()
    {
      var clock = new Clock { UpdateTime = 10d };
      var machine = Machine(clock);
      var state = machine.CreateGraph().CreateState();
      machine.Start();
      clock.UpdateTime = 100d;
      machine.UpdateMachine(0.25f);
      machine.FixedUpdateMachine(0.5f);
      clock.UpdateTime += 0.125d;
      machine.UpdateMachine();
      machine.FixedUpdateMachine();
      Assert.AreEqual(0.375f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.625f, state.FixedElapsed);
    }

    [Test]
    public void TransitionEntryDoesNotConsumeEarlierTime()
    {
      var clock = new Clock();
      var machine = Machine(clock);
      var graph = machine.CreateGraph();
      var first = graph.CreateState();
      var next = graph.CreateState();
      (first > next).On<Advance>();
      machine.Start();
      clock.UpdateTime = 100d;
      machine.LocalRaise(new Advance());
      clock.UpdateTime += 0.125d;
      machine.UpdateMachine();
      Assert.AreSame(next, graph.CurrentUnit);
      Assert.AreEqual(0.125f, next.DeltaTimeSinceStart);
    }

    [Test]
    public void SavedMachineContinuesIdenticallyAtADifferentClockEpoch()
    {
      var a = new Clock { UpdateTime = 10d };
      var b = new Clock { UpdateTime = 100_000_000d };
      var ma = Machine(a);
      var mb = Machine(b);
      var logA = new List<string>();
      var logB = new List<string>();
      Configure(ma, logA);
      Configure(mb, logB);
      ma.Start();
      a.UpdateTime += 0.25d;
      ma.UpdateMachine();
      ma.FixedUpdateMachine();
      var saved = ma.Save();
      mb.Load(saved);
      CollectionAssert.IsEmpty(logB, "Loading must not invoke entry or timer callbacks.");
      logA.Clear();

      for (var i = 0; i < 12; i++)
      {
        a.UpdateTime += 0.125d;
        b.UpdateTime += 0.125d;
        ma.UpdateMachine();
        mb.UpdateMachine();
        ma.FixedUpdateMachine();
        mb.FixedUpdateMachine();
        ma.LateUpdateMachine();
        mb.LateUpdateMachine();
      }

      Assert.IsNotEmpty(logA);
      CollectionAssert.AreEqual(logA, logB);
      Assert.AreEqual(JsonUtility.ToJson(ma.Save()), JsonUtility.ToJson(mb.Save()));

      void Configure(StateMachine machine, List<string> log)
      {
        var graph = machine.CreateGraph();
        var idle = graph.CreateState();
        var active = graph.CreateState();
        idle.AtEvery(0.25f, () => log.Add("idle timer"));
        idle.AtFixed(0.375f, () => log.Add("fixed timer"));
        (idle > active).After(0.75f);
        active.OnEnter = () => log.Add("enter active");
        active.LocalTimeScale = 0.5f;
        active.AtEvery(0.25f, () => log.Add("active timer"));
        var child = active.CreateGraph().CreateState();
        child.AtEveryFixed(0.125f, () => log.Add("child fixed timer"));
        child.OnLateUpdate = elapsed => log.Add("late " + elapsed);
      }
    }

    [Test]
    public void ManagedCreationInstallsClockBeforeSetupAndStartsSynchronously()
    {
      var clock = new Clock { UpdateTime = 500d };
      _object = new GameObject("Injected clock owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      StateUnit state = null;
      var entered = false;
      var machine = owner.CreateManagedStateMachine(clock, sm =>
      {
        Assert.AreSame(clock, sm.Clock);
        state = sm.CreateGraph().CreateState();
        state.OnEnter = () => entered = true;
      });
      Assert.IsTrue(entered);
      var wrapper = _object.GetComponent<StateMachineWrapper>();
      clock.UpdateTime += 0.25d;
      wrapper.UpdateManagedStateMachinesForTesting();
      wrapper.FixedUpdateManagedStateMachinesForTesting();
      Assert.AreEqual(0.25f, state.DeltaTimeSinceStart);
      Assert.AreEqual(0.125f, state.FixedElapsed);
      Assert.AreSame(clock, machine.Clock);
    }

    [Test]
    public void ManagedDefaultAndDeferredStartRetainSelectedClock()
    {
      var clock = new Clock { UpdateTime = 10d };
      StateMachineClock.Default = clock;
      _object = new GameObject("Deferred clock owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      owner.enabled = false;
      StateUnit state = null;
      var entries = 0;
      owner.CreateManagedStateMachine(sm =>
      {
        state = sm.CreateGraph().CreateState();
        state.OnEnter = () => entries++;
      });
      StateMachineClock.Default = new Clock { UpdateTime = 900d };
      var wrapper = _object.GetComponent<StateMachineWrapper>();
      clock.UpdateTime += 100d;
      wrapper.UpdateManagedStateMachinesForTesting();
      Assert.AreEqual(0, entries);
      owner.enabled = true;
      wrapper.UpdateManagedStateMachinesForTesting();
      Assert.AreEqual(1, entries);
      Assert.AreEqual(0f, state.DeltaTimeSinceStart);
      clock.UpdateTime += 0.125d;
      wrapper.UpdateManagedStateMachinesForTesting();
      Assert.AreEqual(0.125f, state.DeltaTimeSinceStart);
      owner.enabled = false;
      wrapper.UpdateManagedStateMachinesForTesting();
      clock.UpdateTime += 100d;
      owner.enabled = true;
      wrapper.UpdateManagedStateMachinesForTesting();
      Assert.AreEqual(0.125f, state.DeltaTimeSinceStart);
      Assert.AreEqual(1, entries);
    }

    [Test]
    public void InjectedManagedClockDoesNotAllocateDuringTicks()
    {
      var clock = new Clock();
      _object = new GameObject("Clock allocation owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      owner.CreateManagedStateMachine(clock, sm => sm.CreateGraph().CreateState());
      var wrapper = _object.GetComponent<StateMachineWrapper>();
      wrapper.UpdateManagedStateMachinesForTesting();
      wrapper.FixedUpdateManagedStateMachinesForTesting();
      GC.GetAllocatedBytesForCurrentThread();
      var before = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 0; i < 100; i++)
      {
        clock.UpdateTime += 0.125d;
        wrapper.UpdateManagedStateMachinesForTesting();
        wrapper.FixedUpdateManagedStateMachinesForTesting();
      }
      var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
      Assert.AreEqual(0, allocated);
    }

    StateMachine Machine(IStateMachineClock clock)
    {
      var machine = new StateMachine(clock);
      _machines.Add(machine);
      return machine;
    }
  }
}
