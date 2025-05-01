using System;
using System.Collections;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
// Assuming EventBusLib namespace

namespace Nopnag.StateMachineLib.Tests
{
    // --- Test Events ---
    public class TestEventA : BusEvent { }
    public class TestEventB : BusEvent { }
    public class TestDamageEvent : BusEvent { }

    public class StateMachineTests
    {
        StateMachine _stateMachine;
        StateGraph _graphA, _graphB; // For parallel graph tests
        StateUnit _state1, _state2, _state3, _stunnedState;
        Action _testAction;

        bool _state1Entered, _state1Updated, _state1Exited;
        bool _state2Entered, _state2Updated, _state2Exited;
        bool _state3Entered, _state3Updated, _state3Exited;
        bool _stunnedEntered, _stunnedUpdated, _stunnedExited;
        bool _eventAListenerCalled, _eventBListenerCalled, _damageListenerCalled;
        bool _actionListenerCalled;
        float _state1UpdateTime, _state2UpdateTime, _state3UpdateTime;
        int _state1UpdateCount, _state2UpdateCount, _state3UpdateCount;

        [SetUp]
        public void Setup()
        {
            // Reset flags and counters before each test
            _stateMachine = new StateMachine();
            _graphA = _stateMachine.CreateGraph();

            _state1 = _graphA.CreateUnit("State1");
            _state2 = _graphA.CreateUnit("State2");
            _state3 = _graphA.CreateUnit("State3");
            _stunnedState = _graphA.CreateUnit("StunnedState");

            // Reset flags
            _state1Entered = _state1Updated = _state1Exited = false;
            _state2Entered = _state2Updated = _state2Exited = false;
            _state3Entered = _state3Updated = _state3Exited = false;
            _stunnedEntered = _stunnedUpdated = _stunnedExited = false;
            _eventAListenerCalled = _eventBListenerCalled = _damageListenerCalled = false;
            _actionListenerCalled = false;
            _state1UpdateTime = _state2UpdateTime = _state3UpdateTime = 0f;
            _state1UpdateCount = _state2UpdateCount = _state3UpdateCount = 0;
            _testAction = null; // Reset action delegate

            // Assign basic functions to track calls
            _state1.EnterStateFunction = () => { _state1Entered = true; Debug.Log("State1 Enter"); };
            _state1.UpdateStateFunction = (dt) => { _state1Updated = true; _state1UpdateTime = dt; _state1UpdateCount++; Debug.Log($"State1 Update: {dt}"); };
            _state1.ExitStateFunction = () => { _state1Exited = true; Debug.Log("State1 Exit"); };

            _state2.EnterStateFunction = () => { _state2Entered = true; Debug.Log("State2 Enter"); };
            _state2.UpdateStateFunction = (dt) => { _state2Updated = true; _state2UpdateTime = dt; _state2UpdateCount++; Debug.Log($"State2 Update: {dt}"); };
            _state2.ExitStateFunction = () => { _state2Exited = true; Debug.Log("State2 Exit"); };

            _state3.EnterStateFunction = () => { _state3Entered = true; Debug.Log("State3 Enter"); };
            _state3.UpdateStateFunction = (dt) => { _state3Updated = true; _state3UpdateTime = dt; _state3UpdateCount++; Debug.Log($"State3 Update: {dt}"); };
            _state3.ExitStateFunction = () => { _state3Exited = true; Debug.Log("State3 Exit"); };

            _stunnedState.EnterStateFunction = () => { _stunnedEntered = true; Debug.Log("Stunned Enter"); };
            _stunnedState.UpdateStateFunction = (dt) => { _stunnedUpdated = true; Debug.Log($"Stunned Update: {dt}"); };
            _stunnedState.ExitStateFunction = () => { _stunnedExited = true; Debug.Log("Stunned Exit"); };
            
            // Clear EventBus listeners (important for test isolation)
            // Note: A proper EventBus might need a ClearAllListeners method for robust testing.
            // For now, we rely on NUnit creating new instances/contexts.
        }

        [TearDown]
        public void Teardown()
        {
             _stateMachine?.Exit(); // Ensure state machine is exited after tests
             // Potentially clear EventBus listeners here if needed and possible
        }

        [UnityTest]
        public IEnumerator InitialState_EntersCorrectly()
        {
            _stateMachine.Start();
            yield return null; // Wait one frame for Enter

            Assert.IsTrue(_state1Entered, "Initial state (State1) did not enter.");
            Assert.IsFalse(_state1Exited, "Initial state (State1) exited prematurely.");
            Assert.IsFalse(_state2Entered, "State2 should not have entered.");
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
        }

        [UnityTest]
        public IEnumerator BasicTransition_WorksAfterTime()
        {
            float transitionTime = 0.1f;
            BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime >= transitionTime);

            _stateMachine.Start();
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            yield return null; // Let initial Enter run

            // Wait slightly longer than the required transition time
            yield return new WaitForSeconds(transitionTime + 0.02f); 

            // Update the machine ONCE to process the accumulated time and check transition
            _stateMachine.UpdateMachine(); 
            yield return null; // Allow Enter/Exit logic of the new state to run

            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition to State2 after waiting.");
            Assert.IsTrue(_state1Exited, "State1 did not exit after transition.");
            Assert.IsTrue(_state2Entered, "State2 did not enter after transition.");
        }
        
        [UnityTest]
        public IEnumerator DirectTransition_WorksImmediately()
        {
            DirectTransition.Connect(_state1, _state2);

            _stateMachine.Start(); // Start should trigger Enter and immediate transition check
            yield return null; // Wait frame for transition
            
            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition immediately.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
        }

        [UnityTest]
        public IEnumerator ConditionalTransition_SelectsCorrectState()
        {
            bool conditionForS2 = false;
            bool conditionForS3 = false;

            ConditionalTransition.Connect(_state1, elapsedTime => {
                if (conditionForS3) return _state3;
                if (conditionForS2) return _state2;
                return null;
            });

            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            conditionForS2 = true;
            _stateMachine.UpdateMachine();
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition to State2");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);

            // Reset and transition to S3
            _stateMachine.Exit();
            yield return null;
            conditionForS2 = false;
            conditionForS3 = true;
            _graphA.InitialUnit = _state1; // Reset initial state if needed, depends on SM design
            _state1Exited = false; // Reset flags
            _state2Exited = false;
            _state3Entered = false;
            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State3", _graphA.GetCurrentStateName(), "Did not transition to State3");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state3Entered);
        }
        
        [UnityTest]
        public IEnumerator ConditionalTransitionByIndex_SelectsCorrectState()
        {
            int targetIndex = -1;
            StateUnit[] targets = { _state2, _state3 };
            ConditionalTransitionByIndex.Connect(_state1, targets, elapsedTime => targetIndex);

            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            targetIndex = 0; // Target State2
            _stateMachine.UpdateMachine();
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition to State2 by index 0.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
            
            // Reset and transition to S3
            _stateMachine.Exit();
            yield return null;
            targetIndex = -1;
            _graphA.InitialUnit = _state1; 
            _state1Exited = false;
            _state2Exited = false;
            _state3Entered = false;
            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            
            targetIndex = 1; // Target State3
            _stateMachine.UpdateMachine();
            yield return null;
            Assert.AreEqual("State3", _graphA.GetCurrentStateName(), "Did not transition to State3 by index 1.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state3Entered);
        }

        [UnityTest]
        public IEnumerator TransitionByAction_WorksOnInvoke()
        {
            TransitionByAction.Connect(_state1, _state2, ref _testAction);
            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            _testAction?.Invoke();
            yield return null; // Wait for state change

            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition on Action invoke.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
        }

        [UnityTest]
        public IEnumerator TransitionByEvent_WorksOnRaise()
        {
            TransitionByEvent.Connect<TestEventA>(_state1, _state2);
            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            EventBus<TestEventA>.Raise(new TestEventA());
            yield return null; // Wait for state change

            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition on Event raise.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
        }
        
        [UnityTest]
        public IEnumerator TransitionByEvent_WithPredicate_WorksOnRaiseIfPredicateTrue()
        {
            bool allowTransition = false;
            TransitionByEvent.Connect<TestEventB>(_state1, _state2, evt => allowTransition);
            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            // Raise when predicate is false
            allowTransition = false;
            EventBus<TestEventB>.Raise(new TestEventB());
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName(), "Transitioned when predicate was false.");

            // Raise when predicate is true
            allowTransition = true;
            EventBus<TestEventB>.Raise(new TestEventB());
            yield return null; // Wait for state change

            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "Did not transition when predicate was true.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
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
            _stateMachine.UpdateMachine(); // Trigger transition
            yield return null; // For exit/enter
            
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_state2Entered);
            
            _stateMachine.UpdateMachine(); // First update of state2
            yield return null;
            Assert.IsTrue(_state2Updated);
        }

        [UnityTest]
        public IEnumerator Subgraph_LifecycleIsCorrect()
        {
            var subGraph = _state1.GetSubStateGraph(); // Create and assign subgraph
            var subStateA = subGraph.CreateUnit("SubA");
            var subStateB = subGraph.CreateUnit("SubB");
            bool subAEntered = false, subAUpdated = false, subAExited = false;
            bool subBEntered = false;

            subStateA.EnterStateFunction = () => subAEntered = true;
            subStateA.UpdateStateFunction = (dt) => subAUpdated = true;
            subStateA.ExitStateFunction = () => subAExited = true;
            subStateB.EnterStateFunction = () => subBEntered = true;

            // Transition within subgraph
            BasicTransition.Connect(subStateA, subStateB, elapsedTime => elapsedTime > 0.05f);
            // Transition out of main state (and thus subgraph)
            BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime > 0.15f);

            _stateMachine.Start(); // Enters state1, which should enter subGraph & subStateA
            yield return null;
            Assert.IsTrue(_state1Entered);
            Assert.IsTrue(subAEntered, "Subgraph initial state (SubA) did not enter.");

            yield return new WaitForSeconds(0.1f); // Time for sub-transition
            _stateMachine.UpdateMachine(); // Trigger sub-transition
            yield return null;
            Assert.IsTrue(subAExited, "SubA did not exit.");
            Assert.IsTrue(subBEntered, "SubB did not enter.");
            Assert.AreEqual("State1", _graphA.GetCurrentStateName()); // Still in main state1

            yield return new WaitForSeconds(0.1f); // Time for main transition
             _stateMachine.UpdateMachine(); // Trigger main transition
             yield return null;
            Assert.IsTrue(_state1Exited, "State1 (with subgraph) did not exit.");
             // Check if subgraph exit was triggered (might need explicit subgraph exit logic check if StateUnit.Exit handles it)
            Assert.IsTrue(_state2Entered, "State2 did not enter after subgraph state exited.");
            Assert.AreEqual("State2", _graphA.GetCurrentStateName());
        }

        [UnityTest]
        public IEnumerator ParallelGraphs_UpdateIndependently()
        {
            _graphB = _stateMachine.CreateGraph(); // Create second parallel graph
            var graphBState1 = _graphB.CreateUnit("GraphB_State1");
            var graphBState2 = _graphB.CreateUnit("GraphB_State2");
            bool graphB1Entered = false, graphB2Entered = false;
            
            graphBState1.EnterStateFunction = () => graphB1Entered = true;
            graphBState2.EnterStateFunction = () => graphB2Entered = true;

            // Transition in Graph A
            BasicTransition.Connect(_state1, _state2, elapsedTime => elapsedTime > 0.05f);
            // Transition in Graph B
            BasicTransition.Connect(graphBState1, graphBState2, elapsedTime => elapsedTime > 0.1f);

            _stateMachine.Start(); // Starts both graphs
            yield return null;
            Assert.IsTrue(_state1Entered, "GraphA initial state did not enter.");
            Assert.IsTrue(graphB1Entered, "GraphB initial state did not enter.");
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            Assert.AreEqual("GraphB_State1", _graphB.GetCurrentStateName());

            yield return new WaitForSeconds(0.08f); // Time for GraphA transition only
            _stateMachine.UpdateMachine();
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName(), "GraphA did not transition.");
            Assert.AreEqual("GraphB_State1", _graphB.GetCurrentStateName(), "GraphB transitioned too early.");

            yield return new WaitForSeconds(0.08f); // Time for GraphB transition
            _stateMachine.UpdateMachine();
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName());
            Assert.AreEqual("GraphB_State2", _graphB.GetCurrentStateName(), "GraphB did not transition.");
            Assert.IsTrue(graphB2Entered);
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
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            EventBus<TestDamageEvent>.Raise(new TestDamageEvent());
            yield return null; // Wait for state change

            Assert.AreEqual("StunnedState", _graphA.GetCurrentStateName(), "Did not transition to StunnedState on damage event.");
            Assert.IsTrue(_state1Exited);
            Assert.IsTrue(_stunnedEntered);
            
            // Wait for stun duration
            yield return new WaitForSeconds(0.15f);
            _stateMachine.UpdateMachine(); // Trigger transition out of stun
            yield return null; 
            
            Assert.AreEqual("State1", _graphA.GetCurrentStateName(), "Did not transition back to State1 after stun duration.");
            Assert.IsTrue(_stunnedExited);
            // state1Entered should be true again if it resets on enter, check Setup/Teardown logic
        }

        [UnityTest]
        public IEnumerator TransitionByEvent_WithEventQuery_WorksOnlyForMatchingParameter()
        {
            // Arrange
            var param1 = new CustomParam();
            var param2 = new CustomParam();
            bool transitioned = false;
            _state1.ExitStateFunction = () => transitioned = true;

            // EventQuery: only match events with param1
            var query = EventBus<TestEventA>.Where<CustomParam>(param1);
            TransitionByEvent.Connect(_state1, _state2, query);

            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            // Raise event with param2 (should NOT transition)
            var evtWrong = new TestEventA();
            evtWrong.Set<CustomParam>(param2);
            EventBus<TestEventA>.Raise(evtWrong);
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            Assert.IsFalse(transitioned, "Transitioned on wrong parameter!");

            // Raise event with param1 (should transition)
            var evtRight = new TestEventA();
            evtRight.Set<CustomParam>(param1);
            EventBus<TestEventA>.Raise(evtRight);
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName());
            Assert.IsTrue(transitioned, "Did not transition on correct parameter!");
        }

        [UnityTest]
        public IEnumerator TransitionByEvent_WithEventQueryAndPredicate_WorksOnlyForMatchingParameterAndPredicate()
        {
            // Arrange
            var param1 = new CustomParam();
            var param2 = new CustomParam();
            bool transitioned = false;
            _state1.ExitStateFunction = () => transitioned = true;

            // Predicate: only allow transition if event's AllowTransition property is true
            Func<TestEventWithFlag, bool> predicate = evt => evt.AllowTransition;

            // EventQuery: only match events with param1
            var query = EventBus<TestEventWithFlag>.Where<CustomParam>(param1);
            TransitionByEvent.Connect(_state1, _state2, query, predicate);

            _stateMachine.Start();
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());

            // Raise event with param2 (should NOT transition)
            var evtWrongParam = new TestEventWithFlag { AllowTransition = true };
            evtWrongParam.Set<CustomParam>(param2);
            EventBus<TestEventWithFlag>.Raise(evtWrongParam);
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            Assert.IsFalse(transitioned, "Transitioned on wrong parameter!");

            // Raise event with param1 but predicate false (should NOT transition)
            var evtWrongPredicate = new TestEventWithFlag { AllowTransition = false };
            evtWrongPredicate.Set<CustomParam>(param1);
            EventBus<TestEventWithFlag>.Raise(evtWrongPredicate);
            yield return null;
            Assert.AreEqual("State1", _graphA.GetCurrentStateName());
            Assert.IsFalse(transitioned, "Transitioned when predicate was false!");

            // Raise event with param1 and predicate true (should transition)
            var evtRight = new TestEventWithFlag { AllowTransition = true };
            evtRight.Set<CustomParam>(param1);
            EventBus<TestEventWithFlag>.Raise(evtRight);
            yield return null;
            Assert.AreEqual("State2", _graphA.GetCurrentStateName());
            Assert.IsTrue(transitioned, "Did not transition on correct parameter and predicate!");
        }

        [UnityTest]
        public IEnumerator StateUnit_Listen_OnlyWhileActive()
        {
            bool called = false;
            _state1.Listen<TestEventA>(evt => called = true);
            _stateMachine.Start();
            yield return null;
            // Should be in state1, so event triggers
            EventBus<TestEventA>.Raise(new TestEventA());
            yield return null;
            Assert.IsTrue(called, "Listener should be called while state is active");

            // Transition to state2
            DirectTransition.Connect(_state1, _state2);
            _state1.ExitStateFunction = () => { };
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
            bool called = false;
            var query = EventBus<TestEventA>.Where<CustomParam>(param1);
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
            _state1.ExitStateFunction = () => { };
            _stateMachine.UpdateMachine();
            yield return null;
            called = false;
            // Now state1 is not active, even correct param should NOT trigger
            var evtAfter = new TestEventA();
            evtAfter.Set<CustomParam>(param1);
            EventBus<TestEventA>.Raise(evtAfter);
            yield return null;
            Assert.IsFalse(called, "Listener should NOT be called when state is not active, even for correct param");
        }

        public class CustomParam : object, IParameter { }
        public class TestEventWithFlag : BusEvent { public bool AllowTransition; }
    }
} 