using System.Collections.Generic;
using Nopnag.EventBusLib;
using NUnit.Framework;

namespace Nopnag.StateMachineLib.Tests
{
  public class LocalEventPropagationTests
  {
    sealed class TestEvent : BusEvent
    {
    }

    readonly List<IIListener> _listeners = new();
    StateMachine              _stateMachine;

    [SetUp]
    public void SetUp()
    {
      _stateMachine = new StateMachine();
    }

    [TearDown]
    public void TearDown()
    {
      foreach (var listener in _listeners) listener.Unsubscribe();
      _listeners.Clear();
      _stateMachine.Dispose();
    }

    [Test]
    public void LocalRaise_StoppedAtHost_DoesNotReachChildGraph()
    {
      var childGraph = CreateActiveGraph(_stateMachine);
      var hostCalls  = 0;
      var childCalls = 0;

      Listen((IGraphHost)_stateMachine, e =>
      {
        hostCalls++;
        e.StopPropagation();
      });
      Listen(childGraph, _ => childCalls++);

      _stateMachine.Start();
      _stateMachine.LocalRaise(new TestEvent());

      Assert.AreEqual(1, hostCalls);
      Assert.AreEqual(0, childCalls);
    }

    [Test]
    public void LocalRaise_StoppedAtChildGraph_DoesNotReachFollowingSiblingGraph()
    {
      var firstGraph  = CreateActiveGraph(_stateMachine);
      var secondGraph = CreateActiveGraph(_stateMachine);
      var firstCalls  = 0;
      var secondCalls = 0;

      Listen(firstGraph, e =>
      {
        firstCalls++;
        e.StopPropagation();
      });
      Listen(secondGraph, _ => secondCalls++);

      _stateMachine.Start();
      _stateMachine.LocalRaise(new TestEvent());

      Assert.AreEqual(1, firstCalls);
      Assert.AreEqual(0, secondCalls);
    }

    static StateGraph CreateActiveGraph(IGraphHost host)
    {
      var graph = host.CreateGraph();
      graph.CreateState();
      return graph;
    }

    void Listen(IGraphHost host, ListenerDelegate<TestEvent> listener)
    {
      _listeners.Add(host.LocalEventBus.On<TestEvent>().Listen(listener));
    }
  }
}
