using System;
using System.Collections;
using System.Collections.Generic;
using Nopnag.EventBusLib;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nopnag.StateMachineLib.Tests
{
  // ISaveable on StateMachine and StateGraph: a snapshot copies every runtime variable
  // of the library, and a machine that loads it continues exactly like the machine
  // that saved it.
  public sealed class SaveAndLoadTests
  {
    public enum TimerKind
    {
      At,
      AtEvery,
      AtFixed,
      AtEveryFixed
    }

    sealed class Go : BusEvent { }
    sealed class Ping : BusEvent { }

    // One topology, built the same way for the saving and the loading machine.
    sealed class Rig
    {
      public readonly StateMachine Machine;
      public readonly List<string> Log = new();
      public readonly StateGraph   Graph;
      public readonly StateGraph   Child;
      public readonly StateUnit    Idle;
      public readonly StateUnit    Charge;
      public readonly StateUnit    Strike;
      public readonly StateUnit    ChildA;
      public readonly StateUnit    ChildB;
      public          int          Tick;

      public Rig() : this(new StateMachine()) { }

      public Rig(StateMachine machine)
      {
        Machine = machine;
        Graph  = Machine.CreateGraph();
        Idle   = Graph.CreateState();
        Charge = Graph.CreateState();
        Strike = Graph.CreateState();

        Idle.OnEnter = () => Add("idle enter");
        Idle.AtEvery(0.3f, () => Add("idle every"));
        (Idle > Charge).On<Go>();

        Charge.OnEnter = () => Add("charge enter");
        Charge.OnExit = () => Add("charge exit");
        Charge.At(0.25f, () =>
        {
          Add("charge at");
          Charge.LocalTimeScale = 0.5f;
        });
        Charge.AtFixed(0.35f, () => Add("charge at fixed"));
        Charge.AtEveryFixed(0.2f, () => Add("charge every fixed"));
        Charge.OnLateUpdate = _ => Add($"charge late {Charge.DeltaTime}");
        (Charge > Strike).After(1f);

        Child  = Charge.CreateGraph();
        ChildA = Child.CreateState();
        ChildB = Child.CreateState();
        ChildA.AtEvery(0.15f, () => Add("child a every"));
        (ChildA > ChildB).AfterFixed(0.4f);
        ChildB.OnEnter = () => Add("child b enter");
        ChildB.At(0.1f, () => Add("child b at"));

        Strike.OnEnter = () =>
        {
          Add("strike enter");
          Charge.LocalTimeScale = 1f;
        };
        (Strike > Idle).After(0.5f);
      }

      public void Step()
      {
        Tick++;
        Machine.UpdateMachine(0.125f);
        Machine.FixedUpdateMachine(0.125f);
        Machine.LateUpdateMachine();
      }

      void Add(string entry)
      {
        Log.Add($"{Tick}: {entry}");
      }
    }

    sealed class OwnerSnapshot
    {
      public StateMachineSnapshot Machine;
      public int                  Charges;
    }

    // How a developer saves what the library cannot see: the owner implements the same
    // interface and keeps its own variables beside the machine's snapshot.
    sealed class Owner : ISaveable<OwnerSnapshot>
    {
      public readonly Rig Rig = new();
      public          int Charges;

      public Owner()
      {
        Rig.Charge.At(0.05f, () => Charges++);
      }

      public OwnerSnapshot Save()
      {
        return new OwnerSnapshot { Machine = Rig.Machine.Save(), Charges = Charges };
      }

      public void Load(OwnerSnapshot snapshot)
      {
        Rig.Machine.Load(snapshot.Machine);
        Charges = snapshot.Charges;
      }
    }

    readonly List<StateMachine> _machines = new();
    GameObject _object;

    [SetUp]
    public void SetUp()
    {
      EventBus.ClearAll();
    }

    [TearDown]
    public void TearDown()
    {
      if (_object != null) Object.DestroyImmediate(_object);
      foreach (var machine in _machines) machine.Dispose();
      _machines.Clear();
      EventBus.ClearAll();
    }

    [Test]
    public void AFreshMachineLoadedFromASaveContinuesExactlyLikeTheSavedOne()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      Assert.AreSame(saved.Charge, saved.Graph.CurrentUnit);
      Assert.AreEqual(0.5f, saved.Charge.LocalTimeScale, "Saved after a callback changed state.");
      Assert.AreSame(saved.ChildB, saved.Child.CurrentUnit);

      var snapshot = saved.Machine.Save();
      loaded.Machine.Load(snapshot);
      loaded.Tick = saved.Tick;
      CollectionAssert.IsEmpty(loaded.Log, "Load runs no callback and does not start the machine.");
      saved.Log.Clear();

      for (var i = 0; i < 25; i++)
      {
        if (i == 15)
        {
          saved.Machine.LocalRaise(new Go());
          loaded.Machine.LocalRaise(new Go());
        }

        saved.Step();
        loaded.Step();
      }

      CollectionAssert.AreEqual(saved.Log, loaded.Log);
      var future = string.Join("\n", saved.Log);
      foreach (var expected in new[]
                 { "charge exit", "strike enter", "idle every", "charge enter", "child b at" })
        StringAssert.Contains(expected, future, "The compared future covers every path.");
    }

    [Test]
    public void LoadingIntoAStartedMachineRunsNoLifecycleCallback()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      loaded.Machine.Start();
      loaded.Log.Clear();

      loaded.Machine.Load(saved.Machine.Save());

      CollectionAssert.IsEmpty(loaded.Log);
      Assert.AreSame(loaded.Charge, loaded.Graph.CurrentUnit);
      Assert.AreSame(loaded.ChildB, loaded.Child.CurrentUnit);
      Assert.AreEqual(saved.Charge.DeltaTimeSinceStart, loaded.Charge.DeltaTimeSinceStart);
      Assert.AreEqual(saved.Charge.FixedElapsed, loaded.Charge.FixedElapsed);
      Assert.AreEqual(saved.Charge.DeltaTime, loaded.Charge.DeltaTime);
      Assert.AreEqual(saved.Charge.FixedDeltaTime, loaded.Charge.FixedDeltaTime);
    }

    [Test]
    public void ATimerThatHasNotFiredAtItsTimeLoadsUnfired()
    {
      var frozen = new StateMachine();
      var copy = new StateMachine();
      _machines.Add(frozen);
      _machines.Add(copy);
      var frozenState = frozen.CreateGraph().CreateState();
      var copyState = copy.CreateGraph().CreateState();
      var frozenCalls = 0;
      var copyCalls = 0;
      frozenState.At(0f, () => frozenCalls++);
      copyState.At(0f, () => copyCalls++);
      frozenState.LocalTimeScale = 0f;
      frozen.Start();
      Assert.AreEqual(0, frozenCalls, "A frozen entry does not run its zero-time timer.");

      copy.Load(frozen.Save());
      Assert.AreEqual(0f, copyState.LocalTimeScale);
      frozenState.LocalTimeScale = 1f;
      copyState.LocalTimeScale = 1f;
      frozen.UpdateMachine(0.1f);
      copy.UpdateMachine(0.1f);

      Assert.AreEqual(1, frozenCalls);
      Assert.AreEqual(1, copyCalls, "The timer's own flag is loaded, not derived from time.");
    }

    [Test]
    public void PowerSwitchesInitialStatesAndTimeScalesOfEveryStateAreSaved()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      saved.Child.SetTurnedOn(false);
      saved.Graph.InitialUnit = saved.Strike;
      saved.Strike.LocalTimeScale = 2f;
      saved.Idle.LocalTimeScale = 0.25f;

      loaded.Machine.Load(saved.Machine.Save());

      Assert.IsFalse(loaded.Child.IsTurnedOn);
      Assert.IsFalse(loaded.Child.IsGraphActive);
      Assert.AreSame(loaded.Strike, loaded.Graph.InitialUnit);
      Assert.AreEqual(2f, loaded.Strike.LocalTimeScale);
      Assert.AreEqual(0.25f, loaded.Idle.LocalTimeScale);
      Assert.AreEqual(0.5f, loaded.Charge.LocalTimeScale);
      Assert.IsTrue(loaded.Machine.IsActive);
    }

    [Test]
    public void AnOwnerSnapshotSurvivesSerializationAndLoadsTheSameFuture()
    {
      var saved = new Owner();
      var loaded = new Owner();
      Track(saved.Rig);
      Track(loaded.Rig);
      RunToCharge(saved.Rig);
      Assert.AreEqual(1, saved.Charges);

      var snapshot = saved.Save();
      var json = JsonUtility.ToJson(snapshot.Machine);
      loaded.Load(new OwnerSnapshot
      {
        Machine = JsonUtility.FromJson<StateMachineSnapshot>(json),
        Charges = snapshot.Charges
      });
      loaded.Rig.Tick = saved.Rig.Tick;
      saved.Rig.Log.Clear();
      Assert.AreEqual(1, loaded.Charges);

      for (var i = 0; i < 25; i++)
      {
        if (i == 15)
        {
          saved.Rig.Machine.LocalRaise(new Go());
          loaded.Rig.Machine.LocalRaise(new Go());
        }

        saved.Rig.Step();
        loaded.Rig.Step();
      }

      CollectionAssert.AreEqual(saved.Rig.Log, loaded.Rig.Log);
      Assert.AreEqual(saved.Charges, loaded.Charges);
      Assert.AreEqual(2, loaded.Charges, "Charge was entered again after the load.");
    }

    [Test]
    public void AGraphSnapshotLoadsOneGraphAndItsDescendants()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      loaded.Machine.Start();
      loaded.Log.Clear();

      var snapshot = saved.Child.Save();
      Assert.AreEqual(1, snapshot.Graphs.Length);
      Assert.AreEqual(2, snapshot.States.Length);
      loaded.Child.Load(snapshot);

      CollectionAssert.IsEmpty(loaded.Log);
      Assert.AreSame(loaded.Idle, loaded.Graph.CurrentUnit, "Only the loaded graph changes.");
      Assert.AreSame(loaded.ChildB, loaded.Child.CurrentUnit);
      Assert.AreEqual(saved.ChildB.DeltaTimeSinceStart, loaded.ChildB.DeltaTimeSinceStart);
      Assert.AreEqual(saved.ChildB.FixedElapsed, loaded.ChildB.FixedElapsed);
    }

    [Test]
    public void ASnapshotLoadsOnlyIntoTheSameTopology()
    {
      var saved = Track(new Rig());
      var other = Track(new Rig());
      other.Graph.CreateState();
      var snapshot = saved.Machine.Save();

      Assert.Throws<InvalidOperationException>(() => other.Machine.Load(snapshot));
    }

    [Test]
    public void EveryTimerKindKeepsItsOwnBookkeepingThroughALoad([Values] TimerKind kind)
    {
      var saved = Single(out var savedState);
      var loaded = Single(out var loadedState);
      var savedCalls = new List<int>();
      var loadedCalls = new List<int>();
      var savedTick = 0;
      var loadedTick = 0;
      Schedule(savedState, kind, () => savedCalls.Add(savedTick));
      Schedule(loadedState, kind, () => loadedCalls.Add(loadedTick));
      // A callback halves the clock, so elapsed time and fired timers stop being
      // proportional to the tick count.
      savedState.At(0.25f, () => savedState.LocalTimeScale = 0.5f);
      loadedState.At(0.25f, () => loadedState.LocalTimeScale = 0.5f);
      saved.Start();
      for (; savedTick < 5; savedTick++) Tick(saved);
      Assert.IsNotEmpty(savedCalls, "A timer fired before the save.");

      loaded.Load(saved.Save());
      loadedTick = savedTick;
      Assert.IsEmpty(loadedCalls, "Load runs no timer.");
      savedCalls.Clear();

      for (var i = 0; i < 30; i++)
      {
        Tick(saved);
        Tick(loaded);
        savedTick++;
        loadedTick++;
      }

      CollectionAssert.AreEqual(savedCalls, loadedCalls);
      Assert.IsNotEmpty(loadedCalls, "A timer fired after the load.");
    }

    [Test]
    public void APeriodicTimerDoesNotReplayIntervalsAfterALoad(
      [Values(TimerKind.AtEvery, TimerKind.AtEveryFixed)] TimerKind kind)
    {
      var saved = Single(out var savedState);
      var loaded = Single(out var loadedState);
      var savedCalls = 0;
      var loadedCalls = 0;
      Schedule(savedState, kind, () => savedCalls++);
      Schedule(loadedState, kind, () => loadedCalls++);
      saved.Start();
      for (var i = 0; i < 80; i++) Tick(saved);

      loaded.Load(saved.Save());
      savedCalls = 0;
      Tick(saved);
      Tick(loaded);

      Assert.AreEqual(savedCalls, loadedCalls);
      Assert.LessOrEqual(loadedCalls, 1);
    }

    [Test]
    public void TheFixedFastPathFollowsTheLoadedTimerFlags()
    {
      var fired = Single(out var firedState);
      var pending = Single(out var pendingState);
      var firedCalls = 0;
      var pendingCalls = 0;
      firedState.AtFixed(0.125f, () => firedCalls++);
      pendingState.AtFixed(0.125f, () => pendingCalls++);
      fired.Start();
      pending.Start();
      fired.FixedUpdateMachine(0.125f);
      Assert.AreEqual(1, firedCalls);
      Assert.IsFalse(firedState.RequiresFixedTickDispatch);
      var pendingSnapshot = pending.Save();

      pending.Load(fired.Save());
      Assert.IsFalse(pendingState.RequiresFixedTickDispatch, "All loaded one-shots fired.");
      pending.FixedUpdateMachine(0.125f);
      Assert.AreEqual(0, pendingCalls);

      fired.Load(pendingSnapshot);
      Assert.IsTrue(firedState.RequiresFixedTickDispatch, "A loaded one-shot is pending.");
      fired.FixedUpdateMachine(0.125f);
      Assert.AreEqual(2, firedCalls);
    }

    [Test]
    public void OnlyTheLoadedCurrentStateHandlesEventsAndEventTransitions()
    {
      var saved = new StateMachine();
      var loaded = new StateMachine();
      _machines.Add(saved);
      _machines.Add(loaded);
      var savedPings = new int[3];
      var loadedPings = new int[3];
      var savedStates = BuildEventGraph(saved, savedPings);
      var loadedStates = BuildEventGraph(loaded, loadedPings);
      saved.Start();
      loaded.Start();
      saved.LocalRaise(new Go());
      Assert.AreSame(savedStates[1], savedStates[1].BaseGraph.CurrentUnit,
        "The saved state was entered by an event.");

      loaded.Load(saved.Save());
      loaded.LocalRaise(new Ping());
      CollectionAssert.AreEqual(new[] { 0, 1, 0 }, loadedPings);

      saved.LocalRaise(new Go());
      loaded.LocalRaise(new Go());
      Assert.AreSame(loadedStates[2], loadedStates[2].BaseGraph.CurrentUnit);
      Assert.AreSame(savedStates[2], savedStates[2].BaseGraph.CurrentUnit);
    }

    [Test]
    public void AHostedGraphThatWasNotEnteredLoadsWithoutACurrentState()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      saved.Machine.Start();
      saved.Step();
      Assert.IsNull(saved.Child.CurrentUnit);
      RunToCharge(loaded);
      loaded.Log.Clear();

      loaded.Machine.Load(saved.Machine.Save());

      Assert.AreSame(loaded.Idle, loaded.Graph.CurrentUnit);
      Assert.IsNull(loaded.Child.CurrentUnit);
      CollectionAssert.IsEmpty(loaded.Log, "No Charge or child state exits.");
      for (var i = 0; i < 4; i++) loaded.Step();
      StringAssert.DoesNotContain("child", string.Join("\n", loaded.Log));
    }

    [Test]
    public void APausedMachineLoadsPausedAndResumesLikeTheSavedOne()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      saved.Machine.SetTurnedOn(false);

      loaded.Machine.Load(saved.Machine.Save());
      loaded.Tick = saved.Tick;
      saved.Log.Clear();
      Assert.IsFalse(loaded.Machine.IsActive);
      var elapsed = loaded.Charge.DeltaTimeSinceStart;
      loaded.Step();
      saved.Step();
      Assert.AreEqual(elapsed, loaded.Charge.DeltaTimeSinceStart, "A paused machine does not tick.");
      CollectionAssert.IsEmpty(loaded.Log);

      saved.Machine.SetTurnedOn(true);
      loaded.Machine.SetTurnedOn(true);
      for (var i = 0; i < 25; i++)
      {
        saved.Step();
        loaded.Step();
      }

      CollectionAssert.AreEqual(saved.Log, loaded.Log);
      Assert.IsNotEmpty(loaded.Log);
    }

    [Test]
    public void AnUnstartedSnapshotStartsLikeTheSavedMachine()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(loaded);
      loaded.Log.Clear();
      loaded.Tick = 0;

      loaded.Machine.Load(saved.Machine.Save());
      CollectionAssert.IsEmpty(loaded.Log);
      for (var i = 0; i < 5; i++)
      {
        saved.Step();
        loaded.Step();
      }

      CollectionAssert.AreEqual(saved.Log, loaded.Log);
      Assert.AreEqual("1: idle enter", loaded.Log[0], "The lazy start enters the initial state.");
    }

    [Test]
    public void ACallbackThatLoadsStopsTheRemainingTimerPass()
    {
      var machine = Single(out var state);
      var calls = 0;
      StateMachineSnapshot start = null;
      state.AtEvery(0.125f, () =>
      {
        calls++;
        machine.Load(start);
      });
      machine.Start();
      start = machine.Save();

      machine.UpdateMachine(1f);

      Assert.AreEqual(1, calls, "The catch-up of seven more intervals stops at the load.");
      Assert.AreEqual(0f, state.DeltaTimeSinceStart);
    }

    [UnityTest]
    public IEnumerator AParameterlessTickAfterALoadDoesNotConsumeTimeBeforeTheLoad()
    {
      while (Time.time <= 0f) yield return null;
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);

      loaded.Machine.Load(saved.Machine.Save());
      loaded.Machine.UpdateMachine();

      Assert.AreEqual(saved.Charge.DeltaTimeSinceStart, loaded.Charge.DeltaTimeSinceStart);
      Assert.AreEqual(saved.ChildB.DeltaTimeSinceStart, loaded.ChildB.DeltaTimeSinceStart);
    }

    [Test]
    public void SavingChangesNothing()
    {
      var saved = Track(new Rig());
      var twin = Track(new Rig());
      RunToCharge(saved);
      RunToCharge(twin);

      var first = JsonUtility.ToJson(saved.Machine.Save());
      var second = JsonUtility.ToJson(saved.Machine.Save());
      Assert.AreEqual(first, second);
      saved.Log.Clear();
      twin.Log.Clear();
      for (var i = 0; i < 25; i++)
      {
        if (i == 15)
        {
          saved.Machine.LocalRaise(new Go());
          twin.Machine.LocalRaise(new Go());
        }

        saved.Step();
        twin.Step();
      }

      CollectionAssert.AreEqual(twin.Log, saved.Log);
    }

    [Test]
    public void LoadDoesNotAllocate()
    {
      var saved = Track(new Rig());
      var loaded = Track(new Rig());
      RunToCharge(saved);
      var snapshot = saved.Machine.Save();
      loaded.Machine.Load(snapshot);

      GC.GetAllocatedBytesForCurrentThread();
      var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 0; i < 100; i++) loaded.Machine.Load(snapshot);
      var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

      Assert.AreEqual(0, allocated);
    }

    [Test]
    public void EveryTopologyMismatchIsRejected()
    {
      var saved = Track(new Rig());
      RunToCharge(saved);
      var snapshot = saved.Machine.Save();

      var extraState = Track(new Rig());
      extraState.Graph.CreateState();
      var extraTimer = Track(new Rig());
      extraTimer.Strike.AtEvery(1f, () => { });
      var extraGraph = Track(new Rig());
      extraGraph.Strike.CreateGraph().CreateState();
      var extraRoot = Track(new Rig());
      extraRoot.Machine.CreateGraph().CreateState();

      Assert.Throws<InvalidOperationException>(() => extraState.Machine.Load(snapshot));
      Assert.Throws<InvalidOperationException>(() => extraTimer.Machine.Load(snapshot));
      Assert.Throws<InvalidOperationException>(() => extraGraph.Machine.Load(snapshot));
      Assert.Throws<InvalidOperationException>(() => extraRoot.Machine.Load(snapshot));

      var trailing = saved.Machine.Save();
      Array.Resize(ref trailing.States, trailing.States.Length + 1);
      trailing.States[trailing.States.Length - 1] = trailing.States[0];
      Assert.Throws<InvalidOperationException>(() => Track(new Rig()).Machine.Load(trailing));
    }

    [Test]
    public void ADisposedMachineCannotSaveOrLoad()
    {
      var saved = Track(new Rig());
      var snapshot = saved.Machine.Save();
      saved.Machine.Dispose();

      Assert.Throws<ObjectDisposedException>(() => saved.Machine.Save());
      Assert.Throws<ObjectDisposedException>(() => saved.Machine.Load(snapshot));
    }

    [UnityTest]
    public IEnumerator AManagedMachineLoadedBeforeItsOwnerIsEnabledIsNotEnteredAgain()
    {
      var saved = Track(new Rig());
      RunToCharge(saved);
      var snapshot = saved.Machine.Save();
      _object = new GameObject("Loaded managed owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      owner.enabled = false;
      Rig loaded = null;
      owner.CreateManagedStateMachine(machine => loaded = new Rig(machine));
      yield return null;
      CollectionAssert.IsEmpty(loaded.Log, "A disabled owner's machine has not started.");

      loaded.Machine.Load(snapshot);
      owner.enabled = true;
      yield return null;
      yield return null;

      Assert.AreSame(loaded.Charge, loaded.Graph.CurrentUnit);
      Assert.AreSame(loaded.ChildB, loaded.Child.CurrentUnit);
      Assert.IsTrue(loaded.Machine.IsActive);
      foreach (var entry in loaded.Log)
        StringAssert.DoesNotContain("enter", entry, "Enabling the owner does not enter again.");
      Assert.GreaterOrEqual(loaded.Charge.DeltaTimeSinceStart, saved.Charge.DeltaTimeSinceStart);
    }

    StateMachine Single(out StateUnit state)
    {
      var machine = new StateMachine();
      _machines.Add(machine);
      state = machine.CreateGraph().CreateState();
      return machine;
    }

    static StateUnit[] BuildEventGraph(StateMachine machine, int[] pings)
    {
      var graph = machine.CreateGraph();
      var states = new[] { graph.CreateState(), graph.CreateState(), graph.CreateState() };
      for (var i = 0; i < states.Length; i++)
      {
        var index = i;
        states[i].On<Ping>(_ => pings[index]++);
      }

      (states[0] > states[1]).On<Go>();
      (states[1] > states[2]).On<Go>();
      return states;
    }

    static void Schedule(StateUnit state, TimerKind kind, Action callback)
    {
      switch (kind)
      {
        case TimerKind.At:
          state.At(0.375f, callback);
          state.At(1.25f, callback);
          break;
        case TimerKind.AtEvery: state.AtEvery(0.375f, callback); break;
        case TimerKind.AtFixed:
          state.AtFixed(0.375f, callback);
          state.AtFixed(1.25f, callback);
          break;
        case TimerKind.AtEveryFixed: state.AtEveryFixed(0.375f, callback); break;
      }
    }

    static void Tick(StateMachine machine)
    {
      machine.UpdateMachine(0.125f);
      machine.FixedUpdateMachine(0.125f);
    }

    Rig Track(Rig rig)
    {
      _machines.Add(rig.Machine);
      return rig;
    }

    // Idle for three ticks, then Charge for six: a scaled clock, fired and pending
    // timers, and a child graph that has moved to its second state.
    static void RunToCharge(Rig rig)
    {
      rig.Machine.Start();
      for (var i = 0; i < 3; i++) rig.Step();
      rig.Machine.LocalRaise(new Go());
      for (var i = 0; i < 6; i++) rig.Step();
    }
  }
}
