using Nopnag.EventBusLib;
using NUnit.Framework;

namespace Nopnag.StateMachineLib.Tests
{
  public class EventRaiseIsolationTests
  {
    sealed class NonPredicateEvent : BusEvent { }
    sealed class PredicateEvent : BusEvent { }
    sealed class StateHandlerEvent : BusEvent { }
    sealed class SecondRaiseEvent : BusEvent { }
    sealed class ParallelGraphsEvent : BusEvent { }
    sealed class SeparateMachinesEvent : BusEvent { }

    [Test]
    public void EventTransition_WithoutPredicate_CannotChainWithinGraph()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var stateA = graph.CreateState();
        var stateB = graph.CreateState();
        var stateC = graph.CreateState();

        (stateA > stateB).On<NonPredicateEvent>();
        (stateB > stateC).On<NonPredicateEvent>();
        graph.InitialUnit = stateA;
        machine.Start();

        var @event = new NonPredicateEvent();
        EventBus<NonPredicateEvent>.Raise(@event);

        Assert.AreSame(stateB, graph.CurrentUnit);
        Assert.IsFalse(@event.IsPropagationStopped);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void EventTransition_WithPredicate_DoesNotEvaluateNewStateForSameRaise()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var stateA = graph.CreateState();
        var stateB = graph.CreateState();
        var stateC = graph.CreateState();
        var secondaryPredicateCalls = 0;

        (stateA > stateB).On<PredicateEvent>();
        (stateB > stateC).On<PredicateEvent>(_ =>
        {
          secondaryPredicateCalls++;
          return true;
        });
        graph.InitialUnit = stateA;
        machine.Start();

        var @event = new PredicateEvent();
        graph.LocalRaise(@event);

        Assert.AreSame(stateB, graph.CurrentUnit);
        Assert.AreEqual(0, secondaryPredicateCalls);
        Assert.IsFalse(@event.IsPropagationStopped);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void StateHandler_NewlyActivatedState_DoesNotHandleSameRaise()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var stateA = graph.CreateState();
        var stateB = graph.CreateState();
        var handlerCalls = 0;

        (stateA > stateB).On<StateHandlerEvent>();
        stateB.On<StateHandlerEvent>(_ => handlerCalls++);
        graph.InitialUnit = stateA;
        machine.Start();

        EventBus<StateHandlerEvent>.Raise(new StateHandlerEvent());

        Assert.AreSame(stateB, graph.CurrentUnit);
        Assert.AreEqual(0, handlerCalls);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void EventTransition_SecondRaise_CanAdvanceNewCurrentState()
    {
      var machine = new StateMachine();
      try
      {
        var graph = machine.CreateGraph();
        var stateA = graph.CreateState();
        var stateB = graph.CreateState();
        var stateC = graph.CreateState();
        var secondaryPredicateCalls = 0;

        (stateA > stateB).On<SecondRaiseEvent>();
        (stateB > stateC).On<SecondRaiseEvent>(_ =>
        {
          secondaryPredicateCalls++;
          return true;
        });
        graph.InitialUnit = stateA;
        machine.Start();

        var reusableEvent = new SecondRaiseEvent();
        EventBus<SecondRaiseEvent>.Raise(reusableEvent);

        Assert.AreSame(stateB, graph.CurrentUnit);
        Assert.AreEqual(0, secondaryPredicateCalls);

        EventBus<SecondRaiseEvent>.Raise(reusableEvent);

        Assert.AreSame(stateC, graph.CurrentUnit);
        Assert.AreEqual(1, secondaryPredicateCalls);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void SameEventRaise_CanTransitionMultipleParallelGraphs()
    {
      var machine = new StateMachine();
      try
      {
        var firstGraph = machine.CreateGraph();
        var firstA = firstGraph.CreateState();
        var firstB = firstGraph.CreateState();
        (firstA > firstB).On<ParallelGraphsEvent>();
        firstGraph.InitialUnit = firstA;

        var secondGraph = machine.CreateGraph();
        var secondA = secondGraph.CreateState();
        var secondB = secondGraph.CreateState();
        (secondA > secondB).On<ParallelGraphsEvent>();
        secondGraph.InitialUnit = secondA;

        machine.Start();

        var @event = new ParallelGraphsEvent();
        EventBus<ParallelGraphsEvent>.Raise(@event);

        Assert.AreSame(firstB, firstGraph.CurrentUnit);
        Assert.AreSame(secondB, secondGraph.CurrentUnit);
        Assert.IsFalse(@event.IsPropagationStopped);
      }
      finally
      {
        machine.Dispose();
      }
    }

    [Test]
    public void SameEventRaise_CanTransitionGraphsInSeparateStateMachines()
    {
      var firstMachine = new StateMachine();
      var secondMachine = new StateMachine();
      try
      {
        var firstGraph = firstMachine.CreateGraph();
        var firstA = firstGraph.CreateState();
        var firstB = firstGraph.CreateState();
        (firstA > firstB).On<SeparateMachinesEvent>();
        firstGraph.InitialUnit = firstA;

        var secondGraph = secondMachine.CreateGraph();
        var secondA = secondGraph.CreateState();
        var secondB = secondGraph.CreateState();
        (secondA > secondB).On<SeparateMachinesEvent>();
        secondGraph.InitialUnit = secondA;

        firstMachine.Start();
        secondMachine.Start();

        var @event = new SeparateMachinesEvent();
        EventBus<SeparateMachinesEvent>.Raise(@event);

        Assert.AreSame(firstB, firstGraph.CurrentUnit);
        Assert.AreSame(secondB, secondGraph.CurrentUnit);
        Assert.IsFalse(@event.IsPropagationStopped);
      }
      finally
      {
        firstMachine.Dispose();
        secondMachine.Dispose();
      }
    }
  }
}
