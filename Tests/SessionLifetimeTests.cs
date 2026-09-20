using System;
using System.Collections;
using Nopnag.EventBusLib;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Nopnag.StateMachineLib.Tests
{
  public sealed class SessionLifetimeTests
  {
    sealed class Tick : BusEvent { }
    sealed class Advance : BusEvent { }
    GameObject _object;

    [SetUp] public void Begin() => EventBus.ClearAll();
    [TearDown] public void End()
    {
      if (_object != null) Object.DestroyImmediate(_object);
      EventBus.ClearAll();
    }

    [Test]
    public void RawMachineRequiresStartAndDisposeRemovesGlobalAndLocalSubscriptions()
    {
      var machine = new StateMachine();
      var calls = 0;
      var graph = machine.CreateGraph();
      var a = graph.CreateState();
      var b = graph.CreateState();
      a.On<Tick>(_ => calls++);
      (a > b).On<Advance>();
      var local = ((IGraphHost)graph).LocalEventBus;
      try
      {
        EventBus.Raise(new Tick());
        Assert.That(calls, Is.Zero);
        machine.Start();
        EventBus.Raise(new Tick());
        local.Raise(new Tick());
        Assert.That(calls, Is.EqualTo(2));
        local.Raise(new Advance());
        Assert.That(graph.CurrentUnit, Is.SameAs(b));
        machine.Dispose();
        EventBus.Raise(new Tick());
        local.Raise(new Tick());
        Assert.DoesNotThrow(() => EventBus.Raise(new Advance()));
        Assert.DoesNotThrow(() => local.Raise(new Advance()));
        Assert.That(calls, Is.EqualTo(2));
      }
      finally { machine.Dispose(); }
    }

    [UnityTest]
    public IEnumerator ManagedOwnerDestructionDisposesAtNextWrapperTick()
    {
      _object = new GameObject("Session lifetime owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      var calls = 0;
      StateGraph graph = null;
      var machine = owner.CreateManagedStateMachine(sm =>
      {
        graph = sm.CreateGraph();
        graph.CreateState().On<Tick>(_ => calls++);
      });
      var local = ((IGraphHost)graph).LocalEventBus;
      EventBus.Raise(new Tick());
      Assert.That(calls, Is.EqualTo(1), "Managed creation starts synchronously.");
      Object.DestroyImmediate(owner);
      yield return null;
      EventBus.Raise(new Tick());
      local.Raise(new Tick());
      Assert.That(calls, Is.EqualTo(1));
      Assert.Throws<ObjectDisposedException>(() => machine.UpdateMachine());
    }

    [UnityTest]
    public IEnumerator DisabledOwnerDefersStartAndKeepsSubscriptionsWhilePaused()
    {
      _object = new GameObject("Disabled session owner");
      var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
      owner.enabled = false;
      var entries = 0;
      var calls = 0;
      owner.CreateManagedStateMachine(sm =>
      {
        var state = sm.CreateGraph().CreateState();
        state.OnEnter = () => entries++;
        state.On<Tick>(_ => calls++);
      });
      yield return null;
      EventBus.Raise(new Tick());
      Assert.That(entries, Is.Zero);
      Assert.That(calls, Is.Zero);
      owner.enabled = true;
      yield return null;
      EventBus.Raise(new Tick());
      Assert.That(entries, Is.EqualTo(1));
      Assert.That(calls, Is.EqualTo(1));
      owner.enabled = false;
      yield return null;
      EventBus.Raise(new Tick());
      Assert.That(calls, Is.EqualTo(1));
      owner.enabled = true;
      yield return null;
      EventBus.Raise(new Tick());
      Assert.That(entries, Is.EqualTo(1));
      Assert.That(calls, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator FiveOwnerLifetimesHaveOneCallbackAndOneTransitionPerRaise()
    {
      var calls = 0;
      var transitions = 0;
      for (var session = 0; session < 5; session++)
      {
        EventBus.ClearAll();
        _object = new GameObject("Repeated session owner");
        var owner = _object.AddComponent<TestMonoBehaviourForWrapper>();
        StateGraph graph = null;
        StateUnit b = null;
        var machine = owner.CreateManagedStateMachine(sm =>
        {
          graph = sm.CreateGraph();
          var a = graph.CreateState();
          b = graph.CreateState();
          var c = graph.CreateState();
          a.On<Tick>(_ => calls++);
          b.OnEnter = () => transitions++;
          c.OnEnter = () => transitions++;
          (a > b).On<Advance>();
          (b > c).On<Advance>();
        });
        yield return null;
        var local = ((IGraphHost)graph).LocalEventBus;
        if (session % 2 == 0) EventBus.Raise(new Tick()); else local.Raise(new Tick());
        if (session % 2 == 0) EventBus.Raise(new Advance()); else local.Raise(new Advance());
        Assert.That(calls, Is.EqualTo(session + 1));
        Assert.That(transitions, Is.EqualTo(session + 1));
        Assert.That(graph.CurrentUnit, Is.SameAs(b));
        Object.DestroyImmediate(_object);
        _object = null;
        // Verify disposal BEFORE ClearAll could hide a leaked subscription.
        EventBus.Raise(new Tick());
        local.Raise(new Tick());
        EventBus.Raise(new Advance());
        local.Raise(new Advance());
        Assert.That(calls, Is.EqualTo(session + 1));
        Assert.That(transitions, Is.EqualTo(session + 1));
        Assert.Throws<ObjectDisposedException>(() => machine.UpdateMachine());
      }
    }
  }
}
