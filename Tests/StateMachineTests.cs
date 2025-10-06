using System;
using System.Collections;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Added for marker and configurator types

// Assuming EventBusLib namespace

namespace Nopnag.StateMachineLib.Tests
{
  // --- Test Events ---
  public class TestEventA : BusEvent
  {
  }

  public class TestEventB : BusEvent
  {
  }

  public class TestDamageEvent : BusEvent
  {
  }

  public class TestEvent : BusEvent
  {
  } // Added from DisposalTests

  public class StateMachineTests
  {
    public class CustomParam : object, IParameter
    {
    }

    public class IntParameter : IParameter
    {
      public int Value;

      public override bool Equals(object obj)
      {
        return obj is IntParameter other && Value == other.Value;
      }

      public override int GetHashCode()
      {
        return Value.GetHashCode();
      }
    }

    public class TestEventWithFlag : BusEvent
    {
      public bool AllowTransition;
    }

    public class TestEventWithParam : BusEvent
    {
      public int Value { get; set; }
    }

    bool _actionListenerCalled;
    bool _anyStatePredicateCondition; // For testing AnyState.When
    bool _eventAListenerCalled, _eventBListenerCalled, _damageListenerCalled;

    StateGraph _graphA, _graphB;                                  // For parallel graph tests
    StateUnit  _state1, _state2, _state3, _stunnedState, _state4; // Added _state4 for more states

    bool         _state1Entered,     _state1Updated,     _state1Exited;
    int          _state1UpdateCount, _state2UpdateCount, _state3UpdateCount, _state4UpdateCount;
    float        _state1UpdateTime,  _state2UpdateTime,  _state3UpdateTime,  _state4UpdateTime;
    bool         _state2Entered,     _state2Updated,     _state2Exited;
    bool         _state3Entered,     _state3Updated,     _state3Exited;
    bool         _state4Entered,     _state4Updated,     _state4Exited; // Added flags for _state4
    StateMachine _stateMachine;
    bool         _stunnedEntered, _stunnedUpdated, _stunnedExited;

    // ---- Start of StateMachineWrapper Tests ----
    bool _wrapperTest_StateEntered     = false;
    bool _wrapperTest_StateExited      = false;
    int  _wrapperTest_StateUpdateCount = 0;
    bool _wrapperTest_StateUpdated     = false;

    [UnityTest]
    public IEnumerator AnyState_Operator_OnEvent_TransitionsFromActiveState()
    {
      // AnyState -> _state2 on TestEventA
      (StateGraph.Any > _state2).On<TestEventA>();

      _graphA.InitialUnit = _state1;
      _stateMachine.Start(); // Enters _state1
      yield return null;

      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsFalse(_state2Entered);

      EventBus<TestEventA>.Raise(new TestEventA());
      _stateMachine.UpdateMachine(); // Process event-based AnyState transition
      yield return null;

      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Did not transition to State2 on TestEventA via AnyState.");
      Assert.IsTrue(_state1Exited, "State1 should have exited.");
      Assert.IsTrue(_state2Entered, "State2 should have entered.");

      // Test from another state (_state2 -> _state1 -> _state3 on TestEventA from AnyState)
      _stateMachine.Exit();
      yield return null;
      Setup(); // Reset all flags and states

      (StateGraph.Any > _state3).On<TestEventA>();
      // Setup a way to get to a different state first, e.g., _state2
      (_state1 > _state2).Immediately();

      _graphA.InitialUnit = _state1;
      _stateMachine.Start(); // _state1 -> _state2 immediately
      yield return null;
      _stateMachine
        .UpdateMachine(); // Ensure Now() transition completes if it needs an update cycle
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Should be in State2 initially for this part.");
      Assert.IsFalse(_state3Entered);

      EventBus<TestEventA>.Raise(new TestEventA());
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.AreEqual(_state3, _graphA.CurrentUnit,
        "Did not transition to State3 from State2 on TestEventA via AnyState.");
      Assert.IsTrue(_state2Exited, "State2 should have exited.");
      Assert.IsTrue(_state3Entered, "State3 should have entered.");
    }

    [UnityTest]
    public IEnumerator AnyState_Operator_OnEvent_WithPredicate_TransitionsIfPredicateTrue()
    {
      var allowAnyStateTransition = false;
      (StateGraph.Any > _stunnedState).On<TestDamageEvent>(evt => allowAnyStateTransition);

      _graphA.InitialUnit = _state1;
      _stateMachine.Start(); // Enters _state1
      yield return null;

      // 1. Predicate is false, event fires, should NOT transition
      allowAnyStateTransition = false;
      EventBus<TestDamageEvent>.Raise(new TestDamageEvent());
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit,
        "Should not transition if predicate is false.");
      Assert.IsFalse(_stunnedEntered);

      // 2. Predicate is true, event fires, should transition
      allowAnyStateTransition = true;
      EventBus<TestDamageEvent>.Raise(new TestDamageEvent());
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_stunnedState, _graphA.CurrentUnit,
        "Should transition to StunnedState if predicate is true.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_stunnedEntered);
    }

    [UnityTest]
    public IEnumerator AnyState_Operator_OnEvent_WithQuery_TransitionsFromActiveState()
    {
      var targetParam = new IntParameter { Value = 101 };
      var wrongParam  = new IntParameter { Value = 102 };
      var query       = EventBus<TestEventWithParam>.Where<IntParameter>(targetParam);

      (StateGraph.Any > _state2).On(query);

      _graphA.InitialUnit = _state1;
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      // Wrong param, should not transition
      var evtWrong = new TestEventWithParam();
      evtWrong.Set(wrongParam);
      EventBus<TestEventWithParam>.Raise(evtWrong);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit,
        "S1->S2 Query: Transitioned on wrong param.");
      Assert.IsFalse(_state2Entered);

      // Correct param, should transition
      var evtCorrect = new TestEventWithParam();
      evtCorrect.Set(targetParam);
      EventBus<TestEventWithParam>.Raise(evtCorrect);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "S1->S2 Query: Did not transition on correct param.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);

      // Reset for transition from another state
      _stateMachine.Exit();
      yield return null;
      Setup();

      var nextTargetParam = new IntParameter { Value = 201 };
      var nextQuery       = EventBus<TestEventWithParam>.Where<IntParameter>(nextTargetParam);
      (StateGraph.Any > _state3).On(nextQuery); // Any -> _state3
      (_state1 > _state2).Immediately();        // Go to _state2 first

      _graphA.InitialUnit = _state1;
      _stateMachine.Start();
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Setup S2->S3 Query: Should be in State2.");
      _state1Exited = _state2Exited = _state3Entered = false;

      // Correct param for Any->S3, should transition from S2
      var evtCorrectForS3 = new TestEventWithParam();
      evtCorrectForS3.Set(nextTargetParam);
      EventBus<TestEventWithParam>.Raise(evtCorrectForS3);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state3, _graphA.CurrentUnit,
        "S2->S3 Query: Did not transition on correct param from State2.");
      Assert.IsTrue(_state2Exited);
      Assert.IsTrue(_state3Entered);
    }

    [UnityTest]
    public IEnumerator AnyState_Operator_OnEvent_WithQueryAndPredicate_TransitionsIfPredicateTrue()
    {
      var targetParam              = new IntParameter { Value = 301 };
      var query                    = EventBus<TestEventWithParam>.Where<IntParameter>(targetParam);
      var allowTransitionPredicate = false;

      (StateGraph.Any > _stunnedState).On(query, evt => allowTransitionPredicate);

      _graphA.InitialUnit = _state1;
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      // Correct param, predicate false
      allowTransitionPredicate = false;
      var evtCorrectParam = new TestEventWithParam();
      evtCorrectParam.Set(targetParam);
      EventBus<TestEventWithParam>.Raise(evtCorrectParam);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit,
        "Query+Predicate: Transitioned with predicate false.");
      Assert.IsFalse(_stunnedEntered);

      // Correct param, predicate true
      allowTransitionPredicate = true;
      EventBus<TestEventWithParam>.Raise(evtCorrectParam); // Raise same event again
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_stunnedState, _graphA.CurrentUnit,
        "Query+Predicate: Did not transition with predicate true.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_stunnedEntered);

      // Reset for transition from another state
      _stateMachine.Exit();
      yield return null;
      Setup();

      var nextTargetParam = new IntParameter { Value = 401 };
      var nextQuery = EventBus<TestEventWithParam>.Where<IntParameter>(nextTargetParam);
      var nextAllowTransitionPredicate = false;
      (StateGraph.Any > _state4).On(nextQuery,
        evt => nextAllowTransitionPredicate); // Any -> _state4
      (_state1 > _state2).Immediately();      // Go to _state2 first

      _graphA.InitialUnit = _state1;
      _stateMachine.Start();
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Setup S2->S4 Query+Predicate: Should be in State2.");
      _state1Exited = _state2Exited = _state4Entered = false; // Reset flags for S4

      // Correct param for Any->S4, predicate true
      nextAllowTransitionPredicate = true;
      var evtCorrectForS4 = new TestEventWithParam();
      evtCorrectForS4.Set(nextTargetParam);
      EventBus<TestEventWithParam>.Raise(evtCorrectForS4);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state4, _graphA.CurrentUnit,
        "S2->S4 Query+Predicate: Did not transition from State2 with predicate true.");
      Assert.IsTrue(_state2Exited);
      Assert.IsTrue(_state4Entered);
    }

    [UnityTest]
    public IEnumerator AnyState_Operator_When_TransitionsFromActiveState_AndHasPriority()
    {
      // Setup: 
      // AnyState -> _state3 if _anyStatePredicateCondition is true
      // _state1 -> _state2 if elapsedTime > 0.05f (local transition)
      _anyStatePredicateCondition = false;
      (StateGraph.Any > _state3).When(_ => _anyStatePredicateCondition);
      (_state1 > _state2).When(elapsedTime => elapsedTime >= 0.05f);

      _graphA.InitialUnit = _state1;
      _stateMachine.Start(); // Enters _state1
      yield return null;

      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsTrue(_state1Entered);
      Assert.IsFalse(_state3Entered);

      // Trigger AnyState condition first
      _anyStatePredicateCondition = true;
      _stateMachine.UpdateMachine(); // Should check AnyState first
      yield return null;

      Assert.AreEqual(_state3, _graphA.CurrentUnit,
        "AnyState transition should have priority and moved to State3.");
      Assert.IsTrue(_state1Exited, "State1 should have exited due to AnyState transition.");
      Assert.IsTrue(_state3Entered, "State3 should have been entered via AnyState transition.");
      Assert.IsFalse(_state2Entered,
        "State2 should not have been entered as AnyState had priority.");

      // Reset for next part of the test: Local transition without AnyState firing
      _stateMachine.Exit();
      yield return null;
      Setup(); // Reset all flags and states

      _anyStatePredicateCondition = false; // Ensure AnyState won't fire
      (StateGraph.Any > _state3).When(_ => _anyStatePredicateCondition);
      (_state1 > _state2).When(elapsedTime => elapsedTime >= 0.1f);

      _graphA.InitialUnit = _state1;
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      yield return new WaitForSeconds(0.15f); // Allow time for local transition
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Local transition from State1 to State2 should have occurred.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
      Assert.IsFalse(_state3Entered,
        "State3 should not have been entered as AnyState condition was false.");
    }

    [UnityTest]
    public IEnumerator BasicTransition_WorksAfterTime()
    {
      var transitionTime = 0.1f;
      BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime >= transitionTime);

      _stateMachine.Start();
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      yield return null; // Let initial Enter run

      // Wait slightly longer than the required transition time
      yield return new WaitForSeconds(transitionTime + 0.02f);

      // Update the machine ONCE to process the accumulated time and check transition
      _stateMachine.UpdateMachine();
      yield return null; // Allow Enter/Exit logic of the new state to run

      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Did not transition to State2 after waiting.");
      Assert.IsTrue(_state1Exited, "State1 did not exit after transition.");
      Assert.IsTrue(_state2Entered, "State2 did not enter after transition.");
    }

    [UnityTest]
    public IEnumerator ConditionalTransition_SelectsCorrectState()
    {
      var conditionForS2 = false;
      var conditionForS3 = false;

      ConditionalTransition.Connect(_state1, elapsedTime =>
      {
        if (conditionForS3) return _state3;
        if (conditionForS2) return _state2;
        return null;
      });

      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      conditionForS2 = true;
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit, "Did not transition to State2");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);

      // Reset and transition to S3
      _stateMachine.Exit();
      yield return null;
      conditionForS2      = false;
      conditionForS3      = true;
      _graphA.InitialUnit = _state1; // Reset initial state if needed, depends on SM design
      _state1Exited       = false;   // Reset flags
      _state2Exited       = false;
      _state3Entered      = false;

      _stateMachine.Start();
      yield return null;
      _stateMachine.UpdateMachine(); // Need to update to trigger conditional transition
      yield return null;
      Assert.AreEqual(_state3, _graphA.CurrentUnit, "Did not transition to State3");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state3Entered);
    }

    [UnityTest]
    public IEnumerator ConditionalTransitionByIndex_SelectsCorrectState()
    {
      var         targetIndex = -1;
      StateUnit[] targets     = { _state2, _state3 };
      ConditionalTransitionByIndex.Connect(_state1, targets, elapsedTime => targetIndex);

      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      targetIndex = 0; // Target State2
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Did not transition to State2 by index 0.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);

      // Reset and transition to S3
      _stateMachine.Exit();
      yield return null;
      targetIndex         = -1;
      _graphA.InitialUnit = _state1;
      _state1Exited       = false;
      _state2Exited       = false;
      _state3Entered      = false;
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      targetIndex = 1; // Target State3
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state3, _graphA.CurrentUnit,
        "Did not transition to State3 by index 1.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state3Entered);
    }

    [UnityTest]
    public IEnumerator DirectTransition_WorksImmediately()
    {
      DirectTransition.Connect(_state1, _state2);

      _stateMachine.Start(); // Start should trigger Enter and immediate transition check
      yield return null;     // Wait frame for transition

      Assert.AreEqual(_state2, _graphA.CurrentUnit, "Did not transition immediately.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    [UnityTest]
    public IEnumerator FluentAPI_After_WorksAfterDuration()
    {
      var  graph    = _stateMachine.CreateGraph();
      var  s1       = graph.CreateState();
      var  s2       = graph.CreateState();
      bool s1Exited = false, s2Entered = false;

      s1.OnExit  = () => s1Exited  = true;
      s2.OnEnter = () => s2Entered = true;

      var transitionDuration = 0.1f;
      (s1 > s2).After(transitionDuration);

      graph.InitialUnit = s1;
      _stateMachine.Start();
      Assert.IsTrue(s1.IsActive);
      yield return null;

      // Check it doesn't transition before time
      yield return new WaitForSeconds(transitionDuration * 0.5f);
      graph.UpdateGraph();
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s1), "Transitioned with .After() too early.");
      Assert.IsFalse(s2Entered, "s2 entered too early.");

      // Wait for the full duration + a bit
      yield return new WaitForSeconds(transitionDuration * 0.5f + 0.02f);
      graph.UpdateGraph();
      yield return null;

      Assert.IsTrue(graph.IsUnitActive(s2),
        "Did not transition to s2 using fluent .After() API after waiting.");
      Assert.IsTrue(s1Exited, "s1 did not exit after fluent .After() API transition.");
      Assert.IsTrue(s2Entered, "s2 did not enter after fluent .After() API transition.");
    }

    [UnityTest]
    public IEnumerator FluentAPI_ConditionalTransition_DynamicTarget_SelectsCorrectState()
    {
      StateUnit targetStateFromPredicate = null;

      // Fluent API equivalent of ConditionalTransition.Connect(_state1, predicate)
      (_state1 > StateGraph.DynamicTarget).When(elapsedTime => targetStateFromPredicate);

      _stateMachine.Start(); // _state1 is the initial state from Setup
      yield return null;
      Assert.IsTrue(_graphA.IsUnitActive(_state1), "Initial: Should be in State1.");
      Assert.IsTrue(_state1Entered, "Initial: State1.OnEnter should have run.");
      Assert.IsFalse(_state1Exited, "Initial: State1.OnExit should not have run.");

      // --- Test transition to _state2 ---
      bool s1ExitedForS2 = false, s2EnteredForS2 = false;
      _state1.OnExit  = () => s1ExitedForS2  = true;
      _state2.OnEnter = () => s2EnteredForS2 = true;
      // Reset other flags that might have been set by Setup or previous parts if this test were more complex
      _state1Entered = false; // Reset since Setup already ran it.
      _state2Exited  = false;

      targetStateFromPredicate = _state2;
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.IsTrue(_graphA.IsUnitActive(_state2), "To State2: Did not transition to State2.");
      Assert.IsTrue(s1ExitedForS2, "To State2: State1.OnExit did not run.");
      Assert.IsTrue(s2EnteredForS2, "To State2: State2.OnEnter did not run.");

      // --- Reset and transition to _state3 ---
      _stateMachine.Exit(); // Exits current state (_state2)
      yield return null;

      // Reset flags for next phase
      _graphA.InitialUnit = _state1; // Explicitly reset initial state for the graph
      s1ExitedForS2       = false;
      s2EnteredForS2      = false;
      bool s1ExitedForS3 = false, s3EnteredForS3 = false;
      _state1.OnEnter = () => { _state1Entered = true; }; // Re-assign OnEnter to track re-entry
      _state1.OnExit  = () => s1ExitedForS3 = true;
      _state2.OnEnter = null; // Clear previous test-specific assignment
      _state3.OnEnter = () => s3EnteredForS3 = true;
      _state1Entered  = false; // For the upcoming Start()

      targetStateFromPredicate = null; // Ensure no immediate transition on Start()
      _stateMachine.Start();           // Re-enters _state1
      yield return null;
      Assert.IsTrue(_graphA.IsUnitActive(_state1),
        "To State3 Reset: Should be in State1 after reset.");
      Assert.IsTrue(_state1Entered, "To State3 Reset: State1.OnEnter should have run on restart.");
      Assert.IsFalse(s1ExitedForS3, "To State3 Reset: State1.OnExit should not have run yet.");

      targetStateFromPredicate = _state3;
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.IsTrue(_graphA.IsUnitActive(_state3), "To State3: Did not transition to State3.");
      Assert.IsTrue(s1ExitedForS3, "To State3: State1.OnExit did not run for S3 transition.");
      Assert.IsTrue(s3EnteredForS3, "To State3: State3.OnEnter did not run.");
    }

    [UnityTest]
    public IEnumerator FluentAPI_Immediately_Works()
    {
      var  graph    = _stateMachine.CreateGraph();
      var  s1       = graph.CreateState();
      var  s2       = graph.CreateState();
      bool s1Exited = false, s2Entered = false;

      s1.OnExit  = () => s1Exited  = true;
      s2.OnEnter = () => s2Entered = true;

      // New Fluent API for DirectTransition
      (s1 > s2).Immediately();

      graph.InitialUnit = s1;
      _stateMachine.Start(); // Start should trigger Enter and immediate transition check
      yield return null;     // Wait a frame for the transition to occur

      Assert.IsTrue(graph.IsUnitActive(s2),
        "Did not transition to s2 using fluent .Immediately() API.");
      Assert.IsTrue(s1Exited, "s1 did not exit after fluent .Immediately() API transition.");
      Assert.IsTrue(s2Entered, "s2 did not enter after fluent .Immediately() API transition.");
    }

    [UnityTest]
    public IEnumerator FluentAPI_OnEvent_Generic_WorksOnRaise()
    {
      var graph = _stateMachine.CreateGraph();
      var s1    = graph.CreateState();
      var s2    = graph.CreateState();
      s1.OnExit =
        () => _state1Exited = true; // Use existing flags for simplicity if setup defines them
      s2.OnEnter     = () => _state2Entered = true;
      _state1Exited  = false;
      _state2Entered = false; // Reset

      (s1 > s2).On<TestEventA>();

      graph.InitialUnit = s1;
      _stateMachine.Start();
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s1));

      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null;

      Assert.IsTrue(graph.IsUnitActive(s2),
        "Did not transition on Event (generic) using fluent API.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    [UnityTest]
    public IEnumerator FluentAPI_OnEvent_WithPredicate_WorksIfPredicateTrue()
    {
      var graph = _stateMachine.CreateGraph();
      var s1    = graph.CreateState();
      var s2    = graph.CreateState();
      s1.OnExit      = () => _state1Exited  = true;
      s2.OnEnter     = () => _state2Entered = true;
      _state1Exited  = false;
      _state2Entered = false;
      var allowTransition = false;

      (s1 > s2).On<TestEventB>(evt => allowTransition);

      graph.InitialUnit = s1;
      _stateMachine.Start();
      yield return null;

      allowTransition = false;
      EventBus<TestEventB>.Raise(new TestEventB());
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s1), "Transitioned with predicate false.");

      allowTransition = true;
      EventBus<TestEventB>.Raise(new TestEventB());
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s2), "Did not transition with predicate true.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    [UnityTest]
    public IEnumerator FluentAPI_OnEvent_WithQuery_WorksForMatchingParam()
    {
      var graph = _stateMachine.CreateGraph();
      var s1    = graph.CreateState();
      var s2    = graph.CreateState();
      s1.OnExit      = () => _state1Exited  = true;
      s2.OnEnter     = () => _state2Entered = true;
      _state1Exited  = false;
      _state2Entered = false;
      var targetParam = new IntParameter { Value = 10 };
      var wrongParam  = new IntParameter { Value = 11 };
      var query       = EventBus<TestEventWithParam>.Where<IntParameter>(targetParam);

      (s1 > s2).On(query);

      graph.InitialUnit = s1;
      _stateMachine.Start();
      yield return null;

      var evtWrong = new TestEventWithParam();
      evtWrong.Set(wrongParam);
      EventBus<TestEventWithParam>.Raise(evtWrong);
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s1), "Transitioned on wrong param with query.");

      var evtCorrect = new TestEventWithParam();
      evtCorrect.Set(targetParam);
      EventBus<TestEventWithParam>.Raise(evtCorrect);
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s2), "Did not transition on correct param with query.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    [UnityTest]
    public IEnumerator FluentAPI_OnEvent_WithQueryAndPredicate_WorksForMatchingParamAndPredicate()
    {
      var graph = _stateMachine.CreateGraph();
      var s1    = graph.CreateState();
      var s2    = graph.CreateState();
      s1.OnExit      = () => _state1Exited  = true;
      s2.OnEnter     = () => _state2Entered = true;
      _state1Exited  = false;
      _state2Entered = false;
      var targetParam              = new IntParameter { Value = 20 };
      var query                    = EventBus<TestEventWithParam>.Where<IntParameter>(targetParam);
      var allowTransitionPredicate = false;

      (s1 > s2).On(query, evt => allowTransitionPredicate);

      graph.InitialUnit = s1;
      _stateMachine.Start();
      yield return null;

      // Correct param, predicate false
      allowTransitionPredicate = false;
      var evt1 = new TestEventWithParam();
      evt1.Set(targetParam);
      EventBus<TestEventWithParam>.Raise(evt1);
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s1), "Transitioned with predicate false (query+predicate).");

      // Correct param, predicate true
      allowTransitionPredicate = true;
      var evt2 = new TestEventWithParam();
      evt2.Set(targetParam);
      EventBus<TestEventWithParam>.Raise(evt2);
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s2),
        "Did not transition with predicate true (query+predicate).");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    // --- Tests for Fluent Transition API ---

    [UnityTest]
    public IEnumerator FluentAPI_When_WorksAfterTime_UsingOperatorGreaterThan()
    {
      var  graph    = _stateMachine.CreateGraph(); // Use a fresh graph
      var  s1       = graph.CreateState();
      var  s2       = graph.CreateState();
      bool s1Exited = false, s2Entered = false;

      s1.OnExit  = () => s1Exited  = true;
      s2.OnEnter = () => s2Entered = true;

      var transitionTime = 0.1f;
      // New Fluent API for BasicTransition
      (s1 > s2).When(elapsedTime => elapsedTime >= transitionTime);

      graph.InitialUnit = s1;
      _stateMachine.Start(); // Isolate graph for this test
      Assert.IsTrue(graph.IsUnitActive(s1));
      yield return null; // Let initial Enter run

      // Wait slightly longer than the required transition time
      yield return new WaitForSeconds(transitionTime + 0.02f);

      graph.UpdateGraph();
      yield return null; // Allow Enter/Exit logic of the new state to run

      Assert.IsTrue(graph.IsUnitActive(s2),
        "Did not transition to s2 using fluent API after waiting.");
      Assert.IsTrue(s1Exited, "s1 did not exit after fluent API transition.");
      Assert.IsTrue(s2Entered, "s2 did not enter after fluent API transition.");
    }

    [UnityTest]
    public IEnumerator FluentAPI_When_WorksAfterTime_UsingOperatorLessThan()
    {
      var  graph     = _stateMachine.CreateGraph();
      var  s1_source = graph.CreateState(); // Source
      var  s2_target = graph.CreateState(); // Target
      bool s1Exited  = false, s2Entered = false;

      s1_source.OnExit  = () => s1Exited  = true;
      s2_target.OnEnter = () => s2Entered = true;

      var transitionTime = 0.1f;
      // New Fluent API using < operator: (target < source)
      (s2_target < s1_source).When(elapsedTime => elapsedTime >= transitionTime);

      graph.InitialUnit = s1_source;
      _stateMachine.Start();
      Assert.IsTrue(graph.IsUnitActive(s1_source));
      yield return null;

      yield return new WaitForSeconds(transitionTime + 0.02f);

      graph.UpdateGraph();
      yield return null;

      Assert.IsTrue(graph.IsUnitActive(s2_target),
        "Did not transition to s2_target using fluent API with < operator after waiting.");
      Assert.IsTrue(s1Exited,
        "s1_source did not exit after fluent API transition with < operator.");
      Assert.IsTrue(s2Entered,
        "s2_target did not enter after fluent API transition with < operator.");
    }

    [UnityTest]
    public IEnumerator InitialState_EntersCorrectly()
    {
      _stateMachine.Start();
      yield return null; // Wait one frame for Enter

      Assert.IsTrue(_state1Entered, "Initial state (State1) did not enter.");
      Assert.IsFalse(_state1Exited, "Initial state (State1) exited prematurely.");
      Assert.IsFalse(_state2Entered, "State2 should not have entered.");
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
    }

    // --- Tests for New API --- 

    [UnityTest]
    public IEnumerator NewAPI_InitialState_EntersCorrectly_UsingCreateState()
    {
      var graph     = _stateMachine.CreateGraph();
      var s1        = graph.CreateState(); // New API
      var s1Entered = false;
      s1.OnEnter = () => { s1Entered = true; }; // New API

      graph.InitialUnit = s1; // Set initial unit for this graph
      _stateMachine.Start();  // This will start all graphs, including the new one
      yield return null;

      Assert.IsTrue(s1Entered,
        "Initial state (s1) created with CreateState() did not enter using OnEnter.");
      Assert.IsTrue(graph.IsUnitActive(s1), "s1 should be active.");
      // Assert.IsNull(graph.CurrentUnit, "GetCurrentStateName should be null for states created with CreateState()."); 
      // GetCurrentStateName will return the name of _currentUnit from _graphA if _graphA was also started.
      // To test GetCurrentStateName accurately for this new graph, we might need to ensure only this graph is present or active.
      // For now, let's focus on IsUnitActive and the OnEnter flag.
    }

    [UnityTest]
    public IEnumerator NewAPI_OnEvent_Generic_OnlyWhileActive_UsingCreateState()
    {
      var graph                       = _stateMachine.CreateGraph();
      var s1                          = graph.CreateState();
      var s2                          = graph.CreateState(); // Another state to transition to
      var s1_TestEventAListenerCalled = false;

      s1.OnEnter = () => Debug.Log("s1 entered for NewAPI_OnEvent_Generic");
      s1.On<TestEventA>(evt => { s1_TestEventAListenerCalled = true; }); // New API

      graph.InitialUnit = s1;
      _stateMachine.Start(); // Isolate graph
      yield return null;

      // Event raised while s1 is active
      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null;
      Assert.IsTrue(s1_TestEventAListenerCalled,
        "s1_TestEventAListenerCalled should be true after event raised while s1 active.");

      // Transition to s2
      s1_TestEventAListenerCalled = false;         // Reset flag
      BasicTransition.Connect(s1, s2, dt => true); // Immediate transition
      graph.UpdateGraph();                         // Process transition
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s2), "Should have transitioned to s2.");

      // Event raised while s1 is NOT active (s2 is active)
      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null;
      Assert.IsFalse(s1_TestEventAListenerCalled,
        "s1_TestEventAListenerCalled should be false after event raised while s1 NOT active.");
    }

    [UnityTest]
    public IEnumerator
      NewAPI_OnEvent_WithEventQuery_OnlyWhileActiveAndMatchingParam_UsingCreateState()
    {
      var graph                  = _stateMachine.CreateGraph();
      var s1                     = graph.CreateState();
      var s2                     = graph.CreateState();
      var s1_QueryListenerCalled = false;
      var targetValue            = 5;

      var matchingQueryParam = new IntParameter { Value = targetValue };
      var query =
        EventBus<TestEventWithParam>
          .Where<IntParameter>(matchingQueryParam); // Use the IntParameter instance for the query

      s1.On<TestEventWithParam>(query, evt => { s1_QueryListenerCalled = true; }); // New API

      graph.InitialUnit = s1;
      _stateMachine.Start();
      yield return null;

      // Raise matching event while s1 active
      var matchingEvent = new TestEventWithParam
        { Value = targetValue };             // Event can still hold its own value if needed
      matchingEvent.Set(matchingQueryParam); // Attach the IParameter for the query system
      EventBus<TestEventWithParam>.Raise(matchingEvent);
      yield return null;
      Assert.IsTrue(s1_QueryListenerCalled,
        "s1_QueryListenerCalled should be true for matching event while s1 active.");

      // Raise non-matching event while s1 active (different IntParameter instance/value)
      s1_QueryListenerCalled = false; // Reset
      var nonMatchingQueryParam = new IntParameter { Value       = targetValue + 1 };
      var nonMatchingEvent      = new TestEventWithParam { Value = targetValue + 1 };
      nonMatchingEvent.Set(nonMatchingQueryParam); // Attach a different IParameter
      EventBus<TestEventWithParam>.Raise(nonMatchingEvent);
      yield return null;
      Assert.IsFalse(s1_QueryListenerCalled,
        "s1_QueryListenerCalled should be false for non-matching event while s1 active.");

      // Transition to s2
      BasicTransition.Connect(s1, s2, dt => true);
      graph.UpdateGraph();
      yield return null;
      Assert.IsTrue(graph.IsUnitActive(s2), "Should have transitioned to s2.");
      s1_QueryListenerCalled = false; // Reset

      // Raise matching event while s1 NOT active
      var matchingEvent_S1Inactive = new TestEventWithParam { Value = targetValue };
      matchingEvent_S1Inactive.Set(matchingQueryParam);
      EventBus<TestEventWithParam>.Raise(matchingEvent_S1Inactive);
      yield return null;
      Assert.IsFalse(s1_QueryListenerCalled,
        "s1_QueryListenerCalled should be false for matching event while s1 NOT active.");
    }

    [UnityTest]
    public IEnumerator NewAPI_StateFunctions_CalledCorrectly_UsingCreateState()
    {
      var graph =
        _stateMachine
          .CreateGraph(); // Use a fresh graph to avoid interference from _graphA if Start is called on _stateMachine
      var  s1             = graph.CreateState();
      var  s2             = graph.CreateState();
      var  s1EnteredCount = 0;
      var  s1ExitCount    = 0;
      bool s1Entered      = false, s1Updated = false, s1Exited = false;
      var  s1UpdateCount  = 0;

      s1.OnEnter = () =>
      {
        s1Entered = true;
        s1EnteredCount++;
      };
      s1.OnUpdate = (dt) =>
      {
        s1Updated = true;
        s1UpdateCount++;
      };
      s1.OnExit = () =>
      {
        s1Exited = true;
        s1ExitCount++;
      };

      graph.InitialUnit = s1;
      // We will call graph.EnterGraph() and graph.UpdateGraph() directly to isolate this graph for the test
      // instead of _stateMachine.Start() and _stateMachine.UpdateMachine()

      _stateMachine.Start();
      yield return null; // For OnEnter

      Assert.IsTrue(s1Entered, "s1.OnEnter was not called.");
      Assert.IsTrue(s1EnteredCount <= 1, $"s1.OnEnter was called more than once. {s1EnteredCount}");

      graph.UpdateGraph();
      yield return null; // For OnUpdate
      Assert.IsTrue(s1Updated, "s1.OnUpdate was not called.");
      Assert.AreEqual(1, s1UpdateCount, "s1.OnUpdate was called an incorrect number of times.");

      graph.UpdateGraph(); // Call update again
      yield return null;
      Assert.AreEqual(2, s1UpdateCount, "s1.OnUpdate was not called a second time.");

      BasicTransition.Connect(s1, s2, dt => true); // Transition immediately on next update check
      graph.UpdateGraph(); // This should trigger transition, s1.OnExit, and s2.OnEnter
      yield return null;

      Assert.IsTrue(s1Exited, "s1.OnExit was not called.");
      Assert.IsTrue(s1ExitCount <= 1, $"s1.OnExit was called more than once. {s1ExitCount}");
    }

    [UnityTest]
    public IEnumerator ParallelGraphs_UpdateIndependently()
    {
      _graphB = _stateMachine.CreateGraph(); // Create second parallel graph
      var  graphBState1   = _graphB.CreateState();
      var  graphBState2   = _graphB.CreateState();
      bool graphB1Entered = false, graphB2Entered = false;

      graphBState1.OnEnter = () => graphB1Entered = true;
      graphBState2.OnEnter = () => graphB2Entered = true;

      // Set initial states for both graphs
      _graphA.InitialUnit = _state1;
      _graphB.InitialUnit = graphBState1;

      // Transition in Graph A
      BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime > 0.05f);
      // Transition in Graph B
      BasicTransition.Connect(graphBState1, graphBState2, elapsedTime => elapsedTime > 0.1f);

      _stateMachine.Start(); // Starts both graphs
      yield return null;
      Assert.IsTrue(_state1Entered, "GraphA initial state did not enter.");
      Assert.IsTrue(graphB1Entered, "GraphB initial state did not enter.");
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.AreEqual(graphBState1, _graphB.CurrentUnit);

      yield return new WaitForSeconds(0.08f); // Time for GraphA transition only
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit, "GraphA did not transition.");
      Assert.AreEqual(graphBState1, _graphB.CurrentUnit,
        "GraphB transitioned too early.");

      yield return new WaitForSeconds(0.08f); // Time for GraphB transition
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit);
      Assert.AreEqual(graphBState2, _graphB.CurrentUnit, "GraphB did not transition.");
      Assert.IsTrue(graphB2Entered);
    }

    [SetUp]
    public void Setup()
    {
      // Reset flags and counters before each test
      _stateMachine = new StateMachine();
      _graphA       = _stateMachine.CreateGraph();

      _state1       = _graphA.CreateState();
      _state2       = _graphA.CreateState();
      _state3       = _graphA.CreateState();
      _stunnedState = _graphA.CreateState();
      _state4       = _graphA.CreateState();

      // Reset flags
      _state1Entered        = _state1Updated        = _state1Exited         = false;
      _state2Entered        = _state2Updated        = _state2Exited         = false;
      _state3Entered        = _state3Updated        = _state3Exited         = false;
      _stunnedEntered       = _stunnedUpdated       = _stunnedExited        = false;
      _eventAListenerCalled = _eventBListenerCalled = _damageListenerCalled = false;
      _actionListenerCalled = false;
      _state1UpdateTime     = _state2UpdateTime  = _state3UpdateTime  = 0f;
      _state1UpdateCount    = _state2UpdateCount = _state3UpdateCount = 0;

      // Assign basic functions to track calls
      _state1.OnEnter = () =>
      {
        _state1Entered = true;
        Debug.Log("State1 Enter");
      };
      _state1.OnUpdate = (dt) =>
      {
        _state1Updated    = true;
        _state1UpdateTime = dt;
        _state1UpdateCount++;
        Debug.Log($"State1 Update: {dt}");
      };
      _state1.OnExit = () =>
      {
        _state1Exited = true;
        Debug.Log("State1 Exit");
      };

      _state2.OnEnter = () =>
      {
        _state2Entered = true;
        Debug.Log("State2 Enter");
      };
      _state2.OnUpdate = (dt) =>
      {
        _state2Updated    = true;
        _state2UpdateTime = dt;
        _state2UpdateCount++;
        Debug.Log($"State2 Update: {dt}");
      };
      _state2.OnExit = () =>
      {
        _state2Exited = true;
        Debug.Log("State2 Exit");
      };

      _state3.OnEnter = () =>
      {
        _state3Entered = true;
        Debug.Log("State3 Enter");
      };
      _state3.OnUpdate = (dt) =>
      {
        _state3Updated    = true;
        _state3UpdateTime = dt;
        _state3UpdateCount++;
        Debug.Log($"State3 Update: {dt}");
      };
      _state3.OnExit = () =>
      {
        _state3Exited = true;
        Debug.Log("State3 Exit");
      };

      _stunnedState.OnEnter = () =>
      {
        _stunnedEntered = true;
        Debug.Log("Stunned Enter");
      };
      _stunnedState.OnUpdate = (dt) =>
      {
        _stunnedUpdated = true;
        Debug.Log($"Stunned Update: {dt}");
      };
      _stunnedState.OnExit = () =>
      {
        _stunnedExited = true;
        Debug.Log("Stunned Exit");
      };

      _state4.OnEnter = () =>
      {
        _state4Entered = true;
        Debug.Log("State4 Enter");
      };
      _state4.OnUpdate = (dt) =>
      {
        Debug.Log($"State4 Update: {dt}");
      }; // Update flags can be added if needed by tests
      _state4.OnExit = () =>
      {
        _state4Exited = true;
        Debug.Log("State4 Exit");
      };

      // Set initial state for the main graph
      _graphA.InitialUnit = _state1;

      // Clear EventBus listeners (important for test isolation)
      // Note: A proper EventBus might need a ClearAllListeners method for robust testing.
      // For now, we rely on NUnit creating new instances/contexts.
    }

    [UnityTest]
    public IEnumerator StateFunctions_AreCalledCorrectly()
    {
      BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime > 0.05f);
      _stateMachine.Start();
      yield return null; // For Enter
      Assert.IsTrue(_state1Entered);

      _stateMachine.UpdateMachine(); // For first update
      yield return null;
      Assert.IsTrue(_state1Updated);
      Assert.Greater(_state1UpdateCount, 0);

      yield return new WaitForSeconds(0.1f); // Wait for transition
      _stateMachine.UpdateMachine();         // Trigger transition
      yield return null;                     // For exit/enter

      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);

      _stateMachine.UpdateMachine(); // First update of state2
      yield return null;
      Assert.IsTrue(_state2Updated);
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_ExitsStateMachine_WhenMonoBehaviourIsDestroyed()
    {
      ResetWrapperTestFlags();
      var go = new GameObject("TestMB_ForWrapper_Destroy");
      var mb = go.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = true;

      // var wrapper = new StateMachineWrapper(mb); // Old way
      var sm = mb.CreateManagedStateMachine(); // New way

      var graph     = sm.CreateGraph();
      var testState = graph.CreateState();
      SetupWrapperTestState(testState);
      graph.InitialUnit = testState;

      yield return null; // Frame 1: SM Starts and Updates
      Assert.IsTrue(_wrapperTest_StateEntered, "State did not enter.");
      _wrapperTest_StateUpdateCount = 0; // Reset update count before destruction.

      Object.DestroyImmediate(go); // Destroy MonoBehaviour

      yield return
        null; // Frame 2: Wrapper should detect destruction and call Exit on the StateMachine.

      Assert.IsTrue(_wrapperTest_StateExited,
        "State did not exit after MonoBehaviour was destroyed.");

      yield return null;
      Assert.AreEqual(0, _wrapperTest_StateUpdateCount,
        "State should not have updated after MB destruction and Exit.");
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_HandlesMonoBehaviourDisabledInitially_ThenEnabled()
    {
      ResetWrapperTestFlags();
      var go = new GameObject("TestMB_ForWrapper_InitialDisabled");
      var mb = go.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = false; // Initially disabled

      // using (var wrapper = new StateMachineWrapper(mb)) // Old way
      var sm = mb.CreateManagedStateMachine(); // New way

      var graph     = sm.CreateGraph();
      var testState = graph.CreateState();
      SetupWrapperTestState(testState);
      graph.InitialUnit = testState;

      yield return null; // Frame 1: MB is disabled. SM should not Start or Update.
      Assert.IsFalse(_wrapperTest_StateEntered,
        "State entered while MonoBehaviour was initially disabled.");
      Assert.AreEqual(0, _wrapperTest_StateUpdateCount,
        "State updated while MonoBehaviour was initially disabled.");

      mb.enabled = true; // Enable MonoBehaviour
      yield return null; // Frame 2: SM should now Start and Update.

      Assert.IsTrue(_wrapperTest_StateEntered,
        "State did not enter after MonoBehaviour was enabled.");
      Assert.AreEqual(1, _wrapperTest_StateUpdateCount,
        "State did not update after MonoBehaviour was enabled.");

      Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_PausesUpdates_WhenMonoBehaviourIsDisabled()
    {
      ResetWrapperTestFlags();
      var go = new GameObject("TestMB_ForWrapper_Pause");
      var mb = go.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = true;

      // using (var wrapper = new StateMachineWrapper(mb)) // Old way
      var sm = mb.CreateManagedStateMachine(); // New way

      var graph     = sm.CreateGraph();
      var testState = graph.CreateState();
      SetupWrapperTestState(testState);
      graph.InitialUnit = testState;

      yield return null; // Frame 1: SM Starts and Updates (count = 1)
      Assert.IsTrue(_wrapperTest_StateEntered, "State did not enter.");
      Assert.AreEqual(1, _wrapperTest_StateUpdateCount, "State did not update initially.");

      mb.enabled = false; // Disable MonoBehaviour
      yield return null;  // Frame 2: SM should NOT update
      Assert.AreEqual(1, _wrapperTest_StateUpdateCount,
        "State updated while MonoBehaviour was disabled.");

      yield return null; // Frame 3: SM should STILL NOT update
      Assert.AreEqual(1, _wrapperTest_StateUpdateCount,
        "State updated again while MonoBehaviour was still disabled.");

      mb.enabled = true; // Re-enable
      yield return null; // Frame 4: SM should resume updates
      Assert.AreEqual(2, _wrapperTest_StateUpdateCount,
        "State did not resume updates after MonoBehaviour re-enabled.");

      Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_StartsAndUpdates_WhenMonoBehaviourIsEnabled()
    {
      ResetWrapperTestFlags();
      var go = new GameObject("TestMB_ForWrapper_Active");
      var mb = go.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = true;

      // using (var wrapper = new StateMachineWrapper(mb)) // Old way
      var sm = mb.CreateManagedStateMachine(); // New way

      var graph     = sm.CreateGraph();
      var testState = graph.CreateState();
      SetupWrapperTestState(testState);
      graph.InitialUnit = testState; // Set initial state

      yield return null; // Frame 1: SM should Start and Update.

      Assert.IsTrue(_wrapperTest_StateEntered, "State did not enter after first frame.");
      Assert.AreEqual(1, _wrapperTest_StateUpdateCount, "State did not update on first frame.");

      yield return null; // Frame 2: SM should update again.
      Assert.AreEqual(2, _wrapperTest_StateUpdateCount, "State did not update on second frame.");

      Object.DestroyImmediate(go); // Cleanup
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_MultipleComponents_IndependentEnableDisable()
    {
      ResetWrapperTestFlags();
      
      // Create GameObject with TWO MonoBehaviours
      var go = new GameObject("TestMultipleComponents");
      var mb1 = go.AddComponent<TestMonoBehaviourForWrapper>();
      var mb2 = go.AddComponent<TestMonoBehaviourForWrapper>();
      
      mb1.enabled = true;
      mb2.enabled = true;

      // Create two separate StateMachines
      var sm1 = mb1.CreateManagedStateMachine();
      var sm2 = mb2.CreateManagedStateMachine();

      var graph1 = sm1.CreateGraph();
      var state1 = graph1.CreateState();
      graph1.InitialUnit = state1;

      var graph2 = sm2.CreateGraph();
      var state2 = graph2.CreateState();
      graph2.InitialUnit = state2;

      // Track updates independently
      int sm1UpdateCount = 0;
      int sm2UpdateCount = 0;

      state1.OnEnter = () => Debug.Log("SM1 State Entered");
      state1.OnUpdate = _ => sm1UpdateCount++;

      state2.OnEnter = () => Debug.Log("SM2 State Entered");
      state2.OnUpdate = _ => sm2UpdateCount++;

      yield return null; // Frame 1: Both start and update

      Assert.AreEqual(1, sm1UpdateCount, "SM1 should have updated once");
      Assert.AreEqual(1, sm2UpdateCount, "SM2 should have updated once");

      // Disable ONLY mb1
      mb1.enabled = false;
      yield return null; // Frame 2: Only SM2 should update

      Assert.AreEqual(1, sm1UpdateCount, "SM1 should NOT update (owner disabled)");
      Assert.AreEqual(2, sm2UpdateCount, "SM2 should continue updating");

      // Disable mb2 as well
      mb2.enabled = false;
      yield return null; // Frame 3: Neither should update

      Assert.AreEqual(1, sm1UpdateCount, "SM1 should still be paused");
      Assert.AreEqual(2, sm2UpdateCount, "SM2 should now be paused too");

      // Re-enable mb1 only
      mb1.enabled = true;
      yield return null; // Frame 4: Only SM1 should resume

      Assert.AreEqual(2, sm1UpdateCount, "SM1 should resume");
      Assert.AreEqual(2, sm2UpdateCount, "SM2 should still be paused");

      // Re-enable mb2
      mb2.enabled = true;
      yield return null; // Frame 5: Both should update

      Assert.AreEqual(3, sm1UpdateCount, "SM1 should update");
      Assert.AreEqual(3, sm2UpdateCount, "SM2 should resume");

      Object.DestroyImmediate(go);
    }

    [UnityTest]
    public IEnumerator StateMachineWrapper_OnExit_AccessesDestroyedChildObject_ShouldNotThrowException()
    {
      ResetWrapperTestFlags();
      
      // Create parent GameObject with MonoBehaviour
      var parentGO = new GameObject("ParentWithStateMachine");
      var mb = parentGO.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = true;

      // Create child GameObject (sub-element)
      var childGO = new GameObject("ChildElement");
      childGO.transform.SetParent(parentGO.transform);
      childGO.SetActive(false); // Initially inactive

      // Create managed StateMachine
      var sm = mb.CreateManagedStateMachine();
      var graph = sm.CreateGraph();
      var idleState = graph.CreateState();
      graph.InitialUnit = idleState;

      // Setup idle state to activate/deactivate child
      idleState.OnEnter = () =>
      {
        Debug.Log("Idle OnEnter: Activating child");
        _wrapperTest_StateEntered = true;
        childGO.SetActive(true); // Activate child when entering idle
      };

      idleState.OnExit = () =>
      {
        Debug.Log("Idle OnExit: Attempting to deactivate child");
        _wrapperTest_StateExited = true;
        
        // Real-world scenario: User doesn't use try-catch
        // This WILL throw exception if childGO is already destroyed
        childGO.SetActive(false); // Try to deactivate child when exiting idle
        Debug.Log("Child deactivated successfully");
      };

      yield return null; // Frame 1: SM Starts, idle enters, child activates

      Assert.IsTrue(_wrapperTest_StateEntered, "Idle state did not enter.");
      Assert.IsTrue(childGO.activeSelf, "Child was not activated on idle enter.");

      // Now destroy the parent (which destroys children first, then triggers wrapper cleanup)
      // If execution order is: Destroy children → Destroy parent → Wrapper detects → Exit called
      // Then childGO will be destroyed BEFORE OnExit is called = EXCEPTION
      Object.DestroyImmediate(parentGO); // This destroys childGO first, then parentGO

      yield return null; // Frame 2: Wrapper detects destruction and calls Exit

      // The OnExit should have been called WITHOUT throwing exception
      Assert.IsTrue(_wrapperTest_StateExited, "Idle state OnExit was not called.");
      
      // If we reach here without exception, the fix is working!
      // If exception is thrown, Unity Test Framework will catch it and test will FAIL
    }
    
    [UnityTest]
    public IEnumerator StateMachineWrapper_OnExit_WithDestroyAsync_ExposesTimingIssue()
    {
      ResetWrapperTestFlags();
      
      // Create parent GameObject with MonoBehaviour
      var parentGO = new GameObject("ParentWithStateMachine");
      var mb = parentGO.AddComponent<TestMonoBehaviourForWrapper>();
      mb.enabled = true;

      // Create child GameObject (sub-element)
      var childGO = new GameObject("ChildElement");
      childGO.transform.SetParent(parentGO.transform);
      childGO.SetActive(false); // Initially inactive

      // Create managed StateMachine
      var sm = mb.CreateManagedStateMachine();
      var graph = sm.CreateGraph();
      var idleState = graph.CreateState();
      graph.InitialUnit = idleState;

      // Setup idle state to activate/deactivate child
      idleState.OnEnter = () =>
      {
        Debug.Log("Idle OnEnter: Activating child");
        _wrapperTest_StateEntered = true;
        childGO.SetActive(true);
      };

      idleState.OnExit = () =>
      {
        Debug.Log("Idle OnExit: Attempting to deactivate child");
        _wrapperTest_StateExited = true;
        
        // Try to access child - this should be safe even if parent is being destroyed
        childGO.SetActive(false);
        Debug.Log("Child deactivated successfully");
      };

      yield return null; // Frame 1: SM Starts, idle enters, child activates

      Assert.IsTrue(_wrapperTest_StateEntered, "Idle state did not enter.");
      Assert.IsTrue(childGO.activeSelf, "Child was not activated on idle enter.");

      // Use async Destroy instead of DestroyImmediate
      // This might expose different timing issues
      Object.Destroy(parentGO); // Async destroy - happens at end of frame
      
      yield return null; // Frame 2: Destruction happens, wrapper should detect and call Exit

      // The OnExit should have been called WITHOUT throwing exception
      Assert.IsTrue(_wrapperTest_StateExited, "Idle state OnExit was not called.");
    }

    // ---- Tests for StateUnit At and AtEvery ----

    [UnityTest]
    public IEnumerator StateUnit_At_CallbackInvoked_AfterTargetTime()
    {
      var atCallbackCalled = false;
      _state1.At(0.1f, () => atCallbackCalled = true);

      _stateMachine.Start();
      yield return null; // Initial Enter

      Assert.IsFalse(atCallbackCalled, "At callback called prematurely.");

      yield return new WaitForSeconds(0.12f); // Wait past target time
      _stateMachine.UpdateMachine();          // Process time
      yield return null;

      Assert.IsTrue(atCallbackCalled, "At callback was not called after target time.");
    }

    [UnityTest]
    public IEnumerator StateUnit_At_CallbackInvoked_WithZeroTargetTime()
    {
      var atCallbackCalled = false;
      _state1.At(0f, () => atCallbackCalled = true);

      _stateMachine.Start(); // Enter State1, should check At(0) 
      // CheckScheduledCallbacks is called in StateUnit.Start() after EnterStateFunction
      // and also in Update() before transitions. So it should be called.
      yield return null; // Let Start and initial Update cycle if any from UpdateMachine run
      _stateMachine.UpdateMachine(); // Ensure one update cycle
      yield return null;

      Assert.IsTrue(atCallbackCalled, "At(0) callback was not called.");
    }

    [UnityTest]
    public IEnumerator StateUnit_At_CallbackNotInvoked_BeforeTargetTime()
    {
      var atCallbackCalled = false;
      _state1.At(0.2f, () => atCallbackCalled = true);

      _stateMachine.Start();
      yield return null;

      yield return new WaitForSeconds(0.1f); // Wait, but not enough
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.IsFalse(atCallbackCalled, "At callback called before target time.");
    }

    [UnityTest]
    public IEnumerator StateUnit_At_CallbackResetsAndInvokes_OnStateReEnter()
    {
      var atCallbackCalled = false;
      var atCallbackCount  = 0;
      _state1.At(0.05f, () =>
      {
        atCallbackCalled = true;
        atCallbackCount++;
      });

      _stateMachine.Start(); // Enter State1
      yield return new WaitForSeconds(0.07f);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.IsTrue(atCallbackCalled, "At callback not called on first enter.");
      Assert.AreEqual(1, atCallbackCount, "At callback count incorrect on first enter.");

      // Transition to State2 and back to State1 using EventBus-based transitions
      atCallbackCalled = false; // Reset flag for re-enter
      TransitionByEvent.Connect<TestEventA>(_state1, _state2);
      TransitionByEvent.Connect<TestEventB>(_state2, _state1);

      EventBus<TestEventA>.Raise(new TestEventA()); // Trigger _state1 -> _state2
      yield return null;                            // Allow transition to complete
      Assert.AreEqual(_state2, _graphA.CurrentUnit);

      EventBus<TestEventB>.Raise(new TestEventB()); // Trigger _state2 -> _state1 (re-enter _state1)
      yield return null;                            // Allow transition to complete
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsFalse(atCallbackCalled,
        "At callback called immediately on re-enter without waiting.");

      yield return new WaitForSeconds(0.07f);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.IsTrue(atCallbackCalled, "At callback not called after re-enter and wait.");
      Assert.AreEqual(2, atCallbackCount, "At callback count incorrect on re-enter.");
    }

    [UnityTest]
    public IEnumerator StateUnit_AtEvery_CallbackInvoked_AtIntervals()
    {
      var atEveryCallbackCount = 0;
      var interval             = 0.1f;
      _state1.AtEvery(interval, () => atEveryCallbackCount++);

      _stateMachine.Start();
      yield return null;

      Assert.AreEqual(0, atEveryCallbackCount, "AtEvery callback called prematurely.");

      yield return new WaitForSeconds(interval * 1.2f); // Past 1st interval
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(1, atEveryCallbackCount, "AtEvery callback not called after 1st interval.");

      yield return new WaitForSeconds(interval); // Past 2nd interval
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(2, atEveryCallbackCount, "AtEvery callback not called after 2nd interval.");

      yield return new WaitForSeconds(interval); // Past 3rd interval
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(3, atEveryCallbackCount, "AtEvery callback not called after 3rd interval.");
    }

    [UnityTest]
    public IEnumerator StateUnit_AtEvery_CallbackNotInvoked_BeforeFirstInterval()
    {
      var atEveryCallbackCount = 0;
      _state1.AtEvery(0.2f, () => atEveryCallbackCount++);

      _stateMachine.Start();
      yield return null;

      yield return new WaitForSeconds(0.1f); // Not enough time
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(0, atEveryCallbackCount, "AtEvery callback called before first interval.");
    }

    [UnityTest]
    public IEnumerator StateUnit_AtEvery_CallbackResetsAndInvokes_OnStateReEnter()
    {
      var atEveryCallbackCount = 0;
      var interval             = 0.05f;
      _state1.AtEvery(interval, () => atEveryCallbackCount++);

      _stateMachine.Start();                            // Enter State1
      yield return new WaitForSeconds(interval * 1.2f); // 1st interval
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(1, atEveryCallbackCount, "AtEvery not called on 1st interval (first enter).");

      yield return new WaitForSeconds(interval); // 2nd interval
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(2, atEveryCallbackCount, "AtEvery not called on 2nd interval (first enter).");

      // Transition to State2 and back to State1 using EventBus-based transitions
      atEveryCallbackCount = 0; // Reset counter for re-enter
      TransitionByEvent.Connect<TestEventA>(_state1, _state2);
      TransitionByEvent.Connect<TestEventB>(_state2, _state1);

      EventBus<TestEventA>.Raise(new TestEventA()); // _state1 -> _state2
      yield return null;                            // Allow transition to complete
      Assert.AreEqual(_state2, _graphA.CurrentUnit);

      EventBus<TestEventB>.Raise(new TestEventB()); // _state2 -> _state1 (re-enter _state1)
      yield return null;                            // Allow transition to complete
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.AreEqual(0, atEveryCallbackCount,
        "AtEvery called immediately on re-enter without waiting.");

      yield return new WaitForSeconds(interval * 1.2f); // 1st interval after re-enter
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(1, atEveryCallbackCount,
        "AtEvery not called on 1st interval (after re-enter).");

      yield return new WaitForSeconds(interval); // 2nd interval after re-enter
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(2, atEveryCallbackCount,
        "AtEvery not called on 2nd interval (after re-enter).");
    }

    [UnityTest]
    public IEnumerator StateUnit_AtEvery_HandlesZeroOrNegativeInterval_Gracefully()
    {
      var callbackCount = 0;
      LogAssert.Expect(LogType.Warning, "StateUnit.AtEvery: intervalTime must be positive.");
      _state1.AtEvery(0f, () => callbackCount++);

      LogAssert.Expect(LogType.Warning, "StateUnit.AtEvery: intervalTime must be positive.");
      _state1.AtEvery(-0.1f, () => callbackCount++);

      _stateMachine.Start();
      yield return new WaitForSeconds(0.1f);
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.AreEqual(0, callbackCount, "Callback invoked for zero or negative interval.");
    }

    [UnityTest]
    public IEnumerator StateUnit_AtEvery_MultipleInvocations_IfMultipleIntervalsPassInOneUpdate()
    {
      var atEveryCallbackCount = 0;
      var interval             = 0.05f; // Small interval
      _state1.AtEvery(interval, () => atEveryCallbackCount++);

      _stateMachine.Start();
      yield return null;

      // Wait for a duration that covers multiple intervals (e.g., 3.5 intervals)
      // Time.timeScale might need adjustment for this to be reliable in tests, but WaitForSeconds 
      // should make Unity's internal time advance sufficiently.
      yield return new WaitForSeconds(interval * 3.5f);

      _stateMachine.UpdateMachine(); // Single update call after a longer pause
      yield return null;

      // StateUnit.CheckPeriodicCallbacks uses a while loop, so it should catch up.
      Assert.AreEqual(3, atEveryCallbackCount, "AtEvery did not invoke for all missed intervals.");

      // Check one more interval to ensure it continues correctly
      atEveryCallbackCount = 0; // Reset for clarity
      yield return new WaitForSeconds(interval * 1.2f);
      _stateMachine.UpdateMachine();
      yield return null;
      Assert.AreEqual(1, atEveryCallbackCount,
        "AtEvery did not continue correctly after catching up.");
    }

    [UnityTest]
    public IEnumerator StateUnit_Listen_OnlyWhileActive()
    {
      var called = false;
      _state1.Listen<TestEventA>(evt => called = true);
      _stateMachine.Start();
      yield return null;
      // Should be in state1, so event triggers
      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null;
      Assert.IsTrue(called, "Listener should be called while state is active");

      // Transition to state2
      DirectTransition.Connect(_state1, _state2);
      _state1.OnExit = () => { };
      _stateMachine.UpdateMachine();
      yield return null;
      called = false;
      // Now state1 is not active, should NOT trigger
      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null;
      Assert.IsFalse(called, "Listener should NOT be called when state is not active");
    }

    [UnityTest]
    public IEnumerator StateUnit_Listen_WithEventQuery_OnlyWhileActiveAndMatchingParam()
    {
      var param1 = new CustomParam();
      var param2 = new CustomParam();
      var called = false;
      var query  = EventBus<TestEventA>.Where<CustomParam>(param1);
      _state1.Listen(query, evt => called = true);
      _stateMachine.Start();
      yield return null;
      // Should be in state1, event with param2 should NOT trigger
      var evtWrong = new TestEventA();
      evtWrong.Set<CustomParam>(param2);
      EventBus<TestEventA>.Raise(evtWrong);
      yield return null;
      Assert.IsFalse(called, "Listener should NOT be called for wrong param");
      // Event with param1 should trigger
      var evtRight = new TestEventA();
      evtRight.Set<CustomParam>(param1);
      EventBus<TestEventA>.Raise(evtRight);
      yield return null;
      Assert.IsTrue(called, "Listener should be called for correct param while active");
      // Transition to state2
      DirectTransition.Connect(_state1, _state2);
      _state1.OnExit = () => { };
      _stateMachine.UpdateMachine();
      yield return null;
      called = false;
      // Now state1 is not active, even correct param should NOT trigger
      var evtAfter = new TestEventA();
      evtAfter.Set<CustomParam>(param1);
      EventBus<TestEventA>.Raise(evtAfter);
      yield return null;
      Assert.IsFalse(called,
        "Listener should NOT be called when state is not active, even for correct param");
    }

    [UnityTest]
    public IEnumerator Subgraph_LifecycleIsCorrect()
    {
      var  subGraph    = _state1.CreateGraph(); // Use new IGraphHost API
      var  subStateA   = subGraph.CreateState();
      var  subStateB   = subGraph.CreateState();
      bool subAEntered = false, subAUpdated = false, subAExited = false;
      var  subBEntered = false;

      subStateA.OnEnter  = () => subAEntered   = true;
      subStateA.OnUpdate = (dt) => subAUpdated = true;
      subStateA.OnExit   = () => subAExited    = true;
      subStateB.OnEnter  = () => subBEntered   = true;

      // Set initial state for subgraph
      subGraph.InitialUnit = subStateA;

      // Transition within subgraph
      BasicTransition.Connect(subStateA, subStateB, elapsedTime => elapsedTime > 0.05f);
      // Transition out of main state (and thus subgraph)
      BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime > 0.15f);

      _stateMachine.Start(); // Enters state1, which should enter subGraph & subStateA
      yield return null;
      Assert.IsTrue(_state1Entered);
      Assert.IsTrue(subAEntered, "Subgraph initial state (SubA) did not enter.");

      yield return new WaitForSeconds(0.1f); // Time for sub-transition
      _stateMachine.UpdateMachine();         // Trigger sub-transition
      yield return null;
      Assert.IsTrue(subAExited, "SubA did not exit.");
      Assert.IsTrue(subBEntered, "SubB did not enter.");
      Assert.AreEqual(_state1, _graphA.CurrentUnit); // Still in main state1

      yield return new WaitForSeconds(0.1f); // Time for main transition
      _stateMachine.UpdateMachine();         // Trigger main transition
      yield return null;
      Assert.IsTrue(_state1Exited, "State1 (with subgraph) did not exit.");
      // Check if subgraph exit was triggered (might need explicit subgraph exit logic check if StateUnit.Exit handles it)
      Assert.IsTrue(_state2Entered, "State2 did not enter after subgraph state exited.");
      Assert.AreEqual(_state2, _graphA.CurrentUnit);
    }

    [TearDown]
    public void Teardown()
    {
      _stateMachine?.Exit(); // Ensure state machine is exited after tests
      // Potentially clear EventBus listeners here if needed and possible
    }

    // Test for damage event transition (similar to README example)
    [UnityTest]
    public IEnumerator TransitionByEvent_DamageCausesStunState()
    {
      // Connect all normal states to stunned state via event
      TransitionByEvent.Connect<TestDamageEvent>(_state1, _stunnedState);
      TransitionByEvent.Connect<TestDamageEvent>(_state2, _stunnedState);
      // Connect stunned back to state1 after duration
      BasicTransition.Connect(_stunnedState, _state1, elapsedTime => elapsedTime > 0.1f);

      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      EventBus<TestDamageEvent>.Raise(new TestDamageEvent());
      yield return null; // Wait for state change

      Assert.AreEqual(_stunnedState, _graphA.CurrentUnit,
        "Did not transition to StunnedState on damage event.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_stunnedEntered);

      // Wait for stun duration
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine(); // Trigger transition out of stun
      yield return null;

      Assert.AreEqual(_state1, _graphA.CurrentUnit,
        "Did not transition back to State1 after stun duration.");
      Assert.IsTrue(_stunnedExited);
      // state1Entered should be true again if it resets on enter, check Setup/Teardown logic
    }

    [UnityTest]
    public IEnumerator TransitionByEvent_WithEventQuery_WorksOnlyForMatchingParameter()
    {
      // Arrange
      var param1       = new CustomParam();
      var param2       = new CustomParam();
      var transitioned = false;
      _state1.OnExit = () => transitioned = true;

      // EventQuery: only match events with param1
      var query = EventBus<TestEventA>.Where<CustomParam>(param1);
      TransitionByEvent.Connect(_state1, _state2, query);

      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      // Raise event with param2 (should NOT transition)
      var evtWrong = new TestEventA();
      evtWrong.Set<CustomParam>(param2);
      EventBus<TestEventA>.Raise(evtWrong);
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsFalse(transitioned, "Transitioned on wrong parameter!");

      // Raise event with param1 (should transition)
      var evtRight = new TestEventA();
      evtRight.Set<CustomParam>(param1);
      EventBus<TestEventA>.Raise(evtRight);
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit);
      Assert.IsTrue(transitioned, "Did not transition on correct parameter!");
    }

    [UnityTest]
    public IEnumerator
      TransitionByEvent_WithEventQueryAndPredicate_WorksOnlyForMatchingParameterAndPredicate()
    {
      // Arrange
      var param1       = new CustomParam();
      var param2       = new CustomParam();
      var transitioned = false;
      _state1.OnExit = () => transitioned = true;

      // Predicate: only allow transition if event's AllowTransition property is true
      Func<TestEventWithFlag, bool> predicate = evt => evt.AllowTransition;

      // EventQuery: only match events with param1
      var query = EventBus<TestEventWithFlag>.Where<CustomParam>(param1);
      TransitionByEvent.Connect(_state1, _state2, query, predicate);

      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      // Raise event with param2 (should NOT transition)
      var evtWrongParam = new TestEventWithFlag { AllowTransition = true };
      evtWrongParam.Set<CustomParam>(param2);
      EventBus<TestEventWithFlag>.Raise(evtWrongParam);
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsFalse(transitioned, "Transitioned on wrong parameter!");

      // Raise event with param1 but predicate false (should NOT transition)
      var evtWrongPredicate = new TestEventWithFlag { AllowTransition = false };
      evtWrongPredicate.Set<CustomParam>(param1);
      EventBus<TestEventWithFlag>.Raise(evtWrongPredicate);
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);
      Assert.IsFalse(transitioned, "Transitioned when predicate was false!");

      // Raise event with param1 and predicate true (should transition)
      var evtRight = new TestEventWithFlag { AllowTransition = true };
      evtRight.Set<CustomParam>(param1);
      EventBus<TestEventWithFlag>.Raise(evtRight);
      yield return null;
      Assert.AreEqual(_state2, _graphA.CurrentUnit);
      Assert.IsTrue(transitioned, "Did not transition on correct parameter and predicate!");
    }

    [UnityTest]
    public IEnumerator TransitionByEvent_WithPredicate_WorksOnRaiseIfPredicateTrue()
    {
      var allowTransition = false;
      TransitionByEvent.Connect<TestEventB>(_state1, _state2, evt => allowTransition);
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      // Raise when predicate is false
      allowTransition = false;
      EventBus<TestEventB>.Raise(new TestEventB());
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit,
        "Transitioned when predicate was false.");

      // Raise when predicate is true
      allowTransition = true;
      EventBus<TestEventB>.Raise(new TestEventB());
      yield return null; // Wait for state change

      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Did not transition when predicate was true.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    [UnityTest]
    public IEnumerator TransitionByEvent_WorksOnRaise()
    {
      TransitionByEvent.Connect<TestEventA>(_state1, _state2);
      _stateMachine.Start();
      yield return null;
      Assert.AreEqual(_state1, _graphA.CurrentUnit);

      EventBus<TestEventA>.Raise(new TestEventA());
      yield return null; // Wait for state change

      Assert.AreEqual(_state2, _graphA.CurrentUnit,
        "Did not transition on Event raise.");
      Assert.IsTrue(_state1Exited);
      Assert.IsTrue(_state2Entered);
    }

    void ResetWrapperTestFlags()
    {
      _wrapperTest_StateEntered     = false;
      _wrapperTest_StateUpdated     = false;
      _wrapperTest_StateExited      = false;
      _wrapperTest_StateUpdateCount = 0;
    }

    void SetupWrapperTestState(StateUnit state)
    {
      state.OnEnter = () =>
      {
        _wrapperTest_StateEntered = true;
        Debug.Log("WrapperTestState Enter");
      };
      state.OnUpdate = (dt) =>
      {
        _wrapperTest_StateUpdated = true;
        _wrapperTest_StateUpdateCount++;
        Debug.Log("WrapperTestState Update");
      };
      state.OnExit = () =>
      {
        _wrapperTest_StateExited = true;
        Debug.Log("WrapperTestState Exit");
      };
    }

    // Removed AnyState_Operator_Now_TransitionsImmediately test method
    // ---- End of StateMachineWrapper Tests ----
  } // This is the original closing brace of StateMachineTests class

  // Helper MonoBehaviour for wrapper tests (this was correctly placed outside)
  public class TestMonoBehaviourForWrapper : MonoBehaviour
  {
    // This class can be empty or have methods if needed for more complex tests
    // For now, its existence and enabled/disabled state are what we monitor.
    public bool WasDestroyed { get; private set; }

    void OnDestroy()
    {
      WasDestroyed = true;
    }
  }

  // Copied from StateMachineDisposalTests.cs
  // Public TestEvent is already added near the top of the namespace.
  public class
    StateMachineDisposalTests // This class should be inside the namespace Nopnag.StateMachineLib.Tests
  {
    class TestMonoBehaviour : MonoBehaviour // This is specific to disposal tests
    {
      public StateMachine ManagedStateMachine { get; private set; }

      void Awake()
      {
        ManagedStateMachine = this.CreateManagedStateMachine();
      }
    }

    int          _eventHandlerCallCount_disposal;
    StateUnit    _s1_disposal;
    StateUnit    _s2_disposal;
    StateMachine _sm_disposal;

    [SetUp]
    public void Disposal_Setup()
    {
      _eventHandlerCallCount_disposal = 0;
    }

    [Test]
    public void ManualDispose_UnsubscribesEventDrivenTransition_DisposalTest()
    {
      _sm_disposal = new StateMachine();
      // Use the fields from Disposal_Setup for convenience, assuming they are reset or this test manages them.
      // If _s1_disposal and _s2_disposal are not reset by Setup, declare them locally.
      var graph = _sm_disposal.CreateGraph();
      _s1_disposal      = graph.CreateState();
      _s2_disposal      = graph.CreateState();
      graph.InitialUnit = _s1_disposal;

      // Setup: Only an event-driven transition
      (_s1_disposal > _s2_disposal).On<TestEvent>();

      _sm_disposal.Start();

      // Trigger event before dispose
      EventBus.Raise(new TestEvent());
      _sm_disposal.UpdateMachine();
      Assert.IsTrue(_s2_disposal.IsActive,
        "EventDrivenTransition_BeforeDispose: _s2_disposal should be active after event.");

      // Dispose
      _sm_disposal.Dispose();

      // Trigger event after dispose - SM should not process it
      EventBus.Raise(new TestEvent());

      // Assert that attempts to use the disposed SM throw exceptions
      Assert.Throws<ObjectDisposedException>(() => { _sm_disposal.UpdateMachine(); },
        "EventDrivenTransition_AfterDispose: UpdateMachine() on disposed SM should throw.");

      Assert.Throws<ObjectDisposedException>(() =>
      {
        var _ = graph.CurrentUnit; // This graph belongs to the disposed SM
      }, "EventDrivenTransition_AfterDispose: Accessing graph of disposed SM should throw.");

      // Optionally, check that a specific listener was not called if we had one, 
      // but this test focuses on the transition mechanism itself not firing.
    }

    [Test]
    public void ManualDispose_UnsubscribesEventListeners_DisposalTest()
    {
      _sm_disposal = new StateMachine();
      SetupStateMachineWithListeners_ForDisposalTest(_sm_disposal);

      EventBus.Raise(new TestEvent());
      _sm_disposal.UpdateMachine(); // Ensure event processing and transitions

      Assert.AreEqual(1, _eventHandlerCallCount_disposal,
        "DisposalTest: StateUnit listener should have been called once before dispose.");
      // Assert.IsTrue(_s1_disposal.IsActive, "DisposalTest: _s1_disposal should be active before dispose."); // This state should be false if transition to s2 occurred
      // Check that transition to _s2_disposal occurred due to the event
      Assert.IsTrue(_s2_disposal.IsActive,
        "DisposalTest: _s2_disposal should be active after first event (transition).");


      _sm_disposal.Dispose();
      _eventHandlerCallCount_disposal = 0;
      // After dispose, the original _s2_disposal is no longer the current state of any active graph in _sm_disposal
      // Raising another event should not call the listener, and should not transition anything in the disposed SM.

      EventBus.Raise(new TestEvent());
      Assert.AreEqual(0, _eventHandlerCallCount_disposal,
        "DisposalTest: StateUnit listener should NOT be called after dispose.");

      // Verify that no state is active or that the state did not change again in the disposed SM context.
      // Since the SM is disposed, its graphs are exited, and units are no longer "active" in that SM.
      // Accessing _s1_disposal.IsActive might be misleading if the underlying graph is gone or inactive.
      // A key test is that the event handler count remains 0.
      // We can also assert that an attempt to use the disposed SM throws an exception.
      Assert.Throws<ObjectDisposedException>(() =>
      {
        _sm_disposal.CreateGraph(); // Attempt to use disposed SM
      });
    }

    [UnityTest]
    public IEnumerator WrapperDispose_UnsubscribesEventListeners_DisposalTest()
    {
      var go = new GameObject("TestGO_DisposalWrapper");
      var testMb =
        go.AddComponent<TestMonoBehaviour>(); // Uses the inner TestMonoBehaviour for disposal tests


      Assert.IsNotNull(testMb.ManagedStateMachine,
        "DisposalTest: Managed StateMachine should be created.");

      var managedSM = testMb.ManagedStateMachine;
      // Need to use local variables for states and listener for this test scope
      StateUnit local_s1        = null;
      StateUnit local_s2        = null;
      var       local_callCount = 0;
      ListenerDelegate<TestEvent> local_listener = e =>
      {
        Debug.Log("Local call in WrapperDispose");
        local_callCount++;
      };

      var graph = managedSM.CreateGraph();
      local_s1          = graph.CreateState();
      local_s2          = graph.CreateState();
      graph.InitialUnit = local_s1;
      local_s1.On<TestEvent>(local_listener);
      (local_s1 > local_s2).On<TestEvent>(); // UNCOMMENT THIS LINE
      managedSM.Start();                     // RE-ADD THIS LINE

      yield return null; // Allow SM to process Start

      Debug.Log("Current unit after Start and yield in WrapperDispose: " + graph.CurrentUnit?.Name);
      Assert.IsTrue(local_s1.IsActive,
        "DisposalTest: Wrapper - local_s1 should be active after Start and before raising event.");

      EventBus.Raise(new TestEvent());
      managedSM.UpdateMachine(); // Manually call UpdateMachine to process the event
      Assert.AreEqual(1, local_callCount,
        "DisposalTest: Wrapper - Listener on local_s1 should have been called once before MB destroy.");
      Assert.IsTrue(local_s2.IsActive,
        "DisposalTest: Wrapper - local_s2 should be active after first event (transition)."); // UNCOMMENT THIS ASSERT

      Object.DestroyImmediate(go);

      yield return null; // Allow wrapper to process destruction and dispose the SM

      local_callCount = 0;
      EventBus.Raise(new TestEvent());
      yield return null;
      Assert.AreEqual(0, local_callCount,
        "DisposalTest: Wrapper - Listener should NOT be called after MB destroy.");

      // Verify the state machine is disposed
      Assert.Throws<ObjectDisposedException>(() =>
        {
          managedSM.CreateGraph(); // Attempt to use disposed SM
        },
        "DisposalTest: Wrapper - Accessing the disposed StateMachine should throw ObjectDisposedException.");
    }

    void Disposal_EventListener(TestEvent e)
    {
      _eventHandlerCallCount_disposal++;
    }

    void SetupStateMachineWithListeners_ForDisposalTest(StateMachine sm)
    {
      var graph = sm.CreateGraph();
      _s1_disposal = graph.CreateState();
      _s2_disposal = graph.CreateState();

      graph.InitialUnit = _s1_disposal;

      _s1_disposal.On<TestEvent>(Disposal_EventListener);
      (_s1_disposal > _s2_disposal).On<TestEvent>();

      sm.Start();
    }
  }
} // End of namespace Nopnag.StateMachineLib.Tests