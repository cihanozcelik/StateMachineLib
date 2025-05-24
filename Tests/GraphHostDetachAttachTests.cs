using System;
using System.Collections;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Nopnag.StateMachineLib.Tests
{
  /// <summary>
  /// Tests for IGraphHost DetachGraph/AttachGraph functionality.
  /// Covers state preservation, event handling, transitions, and hierarchical scenarios
  /// during graph detach/attach cycles.
  /// </summary>
  public class GraphHostDetachAttachTests
  {
    // --- Test Events ---
    public class TestDetachEvent : BusEvent
    {
      public int Value { get; set; }
    }

    public class TestAttachEvent : BusEvent
    {
      public string Message { get; set; }
    }

    public class TestTimerEvent : BusEvent
    {
    }

    // --- Test Fields ---
    StateMachine _stateMachine;
    StateGraph _mainGraph;
    StateGraph _detachableGraph;
    StateUnit _mainState1, _mainState2;
    StateUnit _detachState1, _detachState2, _detachState3;

    // Event tracking flags
    bool _detachEventReceived;
    bool _attachEventReceived;
    bool _timerEventReceived;
    int _eventCallCount;
    string _lastEventMessage;

    // State lifecycle tracking
    bool _detachState1Entered, _detachState1Updated, _detachState1Exited;
    bool _detachState2Entered, _detachState2Updated, _detachState2Exited;
    bool _detachState3Entered, _detachState3Updated, _detachState3Exited;
    int _detachState1UpdateCount, _detachState2UpdateCount;
    float _detachState1UpdateTime, _detachState2UpdateTime;

    // Timer callback tracking
    bool _timerCallbackCalled;
    bool _periodicCallbackCalled;
    int _periodicCallbackCount;

    [SetUp]
    public void Setup()
    {
      // Create main state machine and graph
      _stateMachine = new StateMachine();
      _mainGraph = _stateMachine.CreateGraph();
      
      // Create main states
      _mainState1 = _mainGraph.CreateState();
      _mainState2 = _mainGraph.CreateState();
      
      // Create detachable graph and states
      _detachableGraph = _stateMachine.CreateGraph();
      _detachState1 = _detachableGraph.CreateState();
      _detachState2 = _detachableGraph.CreateState();
      _detachState3 = _detachableGraph.CreateState();

      // Reset all flags
      ResetFlags();

      // Setup state callbacks for detachable graph
      SetupDetachableStateCallbacks();

      // Set initial states
      _mainGraph.InitialUnit = _mainState1;
      _detachableGraph.InitialUnit = _detachState1;
    }

    void ResetFlags()
    {
      _detachEventReceived = false;
      _attachEventReceived = false;
      _timerEventReceived = false;
      _eventCallCount = 0;
      _lastEventMessage = null;

      _detachState1Entered = _detachState1Updated = _detachState1Exited = false;
      _detachState2Entered = _detachState2Updated = _detachState2Exited = false;
      _detachState3Entered = _detachState3Updated = _detachState3Exited = false;
      
      _detachState1UpdateCount = _detachState2UpdateCount = 0;
      _detachState1UpdateTime = _detachState2UpdateTime = 0f;

      _timerCallbackCalled = false;
      _periodicCallbackCalled = false;
      _periodicCallbackCount = 0;
    }

    void SetupDetachableStateCallbacks()
    {
      _detachState1.OnEnter = () =>
      {
        _detachState1Entered = true;
        Debug.Log("DetachState1 Enter");
      };
      
      _detachState1.OnUpdate = (dt) =>
      {
        _detachState1Updated = true;
        _detachState1UpdateTime = dt;
        _detachState1UpdateCount++;
        Debug.Log($"DetachState1 Update: {dt}");
      };
      
      _detachState1.OnExit = () =>
      {
        _detachState1Exited = true;
        Debug.Log("DetachState1 Exit");
      };

      _detachState2.OnEnter = () =>
      {
        _detachState2Entered = true;
        Debug.Log("DetachState2 Enter");
      };
      
      _detachState2.OnUpdate = (dt) =>
      {
        _detachState2Updated = true;
        _detachState2UpdateTime = dt;
        _detachState2UpdateCount++;
        Debug.Log($"DetachState2 Update: {dt}");
      };
      
      _detachState2.OnExit = () =>
      {
        _detachState2Exited = true;
        Debug.Log("DetachState2 Exit");
      };

      _detachState3.OnEnter = () =>
      {
        _detachState3Entered = true;
        Debug.Log("DetachState3 Enter");
      };
      
      _detachState3.OnExit = () =>
      {
        _detachState3Exited = true;
        Debug.Log("DetachState3 Exit");
      };
    }

    [TearDown]
    public void Teardown()
    {
      _stateMachine?.Dispose();
    }

    // ==================== 1. Basic Detach/Attach Behavior ====================

    [UnityTest]
    public IEnumerator DetachGraph_PreservesCurrentState()
    {
      // Start state machine - both graphs should be active
      _stateMachine.Start();
      yield return null;
      
      Assert.IsTrue(_detachState1Entered, "DetachState1 should have entered initially");
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "DetachState1 should be current");
      Assert.IsTrue(_detachableGraph.IsGraphActive, "Detachable graph should be active");

      // Detach the graph
      _stateMachine.DetachGraph(_detachableGraph);
      
      // State should be preserved but graph should be inactive
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "Current state should be preserved after detach");
      Assert.IsFalse(_detachableGraph.IsGraphActive, "Graph should be inactive after detach");
      Assert.IsFalse(_detachState1Exited, "State should not have exited during detach");
    }

    [UnityTest]
    public IEnumerator AttachGraph_RestoresFromPreservedState()
    {
      // Start and then detach
      _stateMachine.Start();
      yield return null;
      _stateMachine.DetachGraph(_detachableGraph);
      
      // Reset enter flag to test re-entry
      _detachState1Entered = false;
      
      // Re-attach the graph
      _stateMachine.AttachGraph(_detachableGraph);
      
      // Graph should be active again with same state
      Assert.IsTrue(_detachableGraph.IsGraphActive, "Graph should be active after re-attach");
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "Should resume from same state");
      Assert.IsFalse(_detachState1Entered, "State should not re-enter on attach (already entered)");
    }

    [UnityTest]
    public IEnumerator DetachAttach_MultipleGraphs_IndependentBehavior()
    {
      // Create second detachable graph
      var secondGraph = _stateMachine.CreateGraph();
      var secondState = secondGraph.CreateState();
      secondGraph.InitialUnit = secondState;
      
      bool secondStateEntered = false;
      secondState.OnEnter = () => secondStateEntered = true;

      // Start all graphs
      _stateMachine.Start();
      yield return null;
      
      Assert.IsTrue(_detachState1Entered, "First graph state should enter");
      Assert.IsTrue(secondStateEntered, "Second graph state should enter");

      // Detach only first graph
      _stateMachine.DetachGraph(_detachableGraph);
      
      // First graph should be detached, second should remain active
      Assert.IsFalse(_detachableGraph.IsGraphActive, "First graph should be detached");
      Assert.IsTrue(secondGraph.IsGraphActive, "Second graph should remain active");
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "First graph state preserved");
      Assert.AreEqual(secondState, secondGraph.CurrentUnit, "Second graph state unchanged");
    }

    // ==================== 2. Event Handling During Detach ====================

    [UnityTest]
    public IEnumerator DetachedGraph_EventListeners_DoNotRespond()
    {
      // Setup event listener on detachable state
      _detachState1.On<TestDetachEvent>(evt =>
      {
        _detachEventReceived = true;
        _eventCallCount++;
      });

      // Start and verify listener works
      _stateMachine.Start();
      yield return null;
      
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent { Value = 1 });
      yield return null;
      
      Assert.IsTrue(_detachEventReceived, "Event should be received when graph is attached");
      Assert.AreEqual(1, _eventCallCount, "Event count should be 1");

      // Detach graph and reset flags
      _stateMachine.DetachGraph(_detachableGraph);
      _detachEventReceived = false;
      _eventCallCount = 0;

      // Raise event again - should not be received
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent { Value = 2 });
      yield return null;
      
      Assert.IsFalse(_detachEventReceived, "Event should NOT be received when graph is detached");
      Assert.AreEqual(0, _eventCallCount, "Event count should remain 0");
    }

    [UnityTest]
    public IEnumerator ReattachedGraph_EventListeners_RespondAgain()
    {
      // Setup event listener
      _detachState1.On<TestDetachEvent>(evt =>
      {
        _detachEventReceived = true;
        _eventCallCount++;
      });

      // Start, detach, then re-attach
      _stateMachine.Start();
      yield return null;
      _stateMachine.DetachGraph(_detachableGraph);
      _stateMachine.AttachGraph(_detachableGraph);

      // Reset flags and test event response
      _detachEventReceived = false;
      _eventCallCount = 0;

      EventBus<TestDetachEvent>.Raise(new TestDetachEvent { Value = 3 });
      yield return null;
      
      Assert.IsTrue(_detachEventReceived, "Event should be received after re-attach");
      Assert.AreEqual(1, _eventCallCount, "Event count should be 1 after re-attach");
    }

    [UnityTest]
    public IEnumerator DetachedGraph_LocalEventBus_DoesNotRespond()
    {
      // Setup local event listener
      _detachState1.On<TestAttachEvent>(evt =>
      {
        _attachEventReceived = true;
        _lastEventMessage = evt.Message;
      });

      _stateMachine.Start();
      yield return null;

      // Test local event works when attached
      _stateMachine.LocalRaise(new TestAttachEvent { Message = "attached" });
      yield return null;
      
      Assert.IsTrue(_attachEventReceived, "Local event should work when attached");
      Assert.AreEqual("attached", _lastEventMessage, "Message should be received");

      // Detach and test local event doesn't work
      _stateMachine.DetachGraph(_detachableGraph);
      _attachEventReceived = false;
      _lastEventMessage = null;

      _stateMachine.LocalRaise(new TestAttachEvent { Message = "detached" });
      yield return null;
      
      Assert.IsFalse(_attachEventReceived, "Local event should NOT work when detached");
      Assert.IsNull(_lastEventMessage, "Message should not be received when detached");
    }

    // ==================== 3. State Transitions During Detach ====================

    [UnityTest]
    public IEnumerator DetachedGraph_TimerTransitions_DoNotTrigger()
    {
      // Setup timer-based transition
      BasicTransition.Connect(_detachState1, _detachState2, elapsedTime => elapsedTime > 0.1f);

      _stateMachine.Start();
      yield return null;
      
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "Should start in state1");

      // Detach before timer expires
      _stateMachine.DetachGraph(_detachableGraph);
      
      // Wait for timer duration
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Should still be in state1 (transition didn't trigger)
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "Should remain in state1 when detached");
      Assert.IsFalse(_detachState2Entered, "State2 should not have entered");
      Assert.IsFalse(_detachState1Exited, "State1 should not have exited");
    }

    [UnityTest]
    public IEnumerator ReattachedGraph_TimerTransitions_ResumeFromCorrectTime()
    {
      // Setup timer-based transition (longer duration)
      BasicTransition.Connect(_detachState1, _detachState2, elapsedTime => elapsedTime > 0.2f);

      _stateMachine.Start();
      yield return null;
      
      // Wait partway through timer
      yield return new WaitForSeconds(0.1f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Detach and re-attach
      _stateMachine.DetachGraph(_detachableGraph);
      _stateMachine.AttachGraph(_detachableGraph);
      
      // Wait for remaining time
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Transition should have triggered (total time > 0.2f)
      Assert.AreEqual(_detachState2, _detachableGraph.CurrentUnit, "Should transition to state2");
      Assert.IsTrue(_detachState1Exited, "State1 should have exited");
      Assert.IsTrue(_detachState2Entered, "State2 should have entered");
    }

    [UnityTest]
    public IEnumerator DetachedGraph_EventTransitions_DoNotTrigger()
    {
      // Setup event-based transition
      TransitionByEvent.Connect<TestTimerEvent>(_detachState1, _detachState2);

      _stateMachine.Start();
      yield return null;
      
      // Detach graph
      _stateMachine.DetachGraph(_detachableGraph);
      
      // Raise transition event
      EventBus<TestTimerEvent>.Raise(new TestTimerEvent());
      yield return null;
      
      // Transition should not have occurred
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "Should remain in state1");
      Assert.IsFalse(_detachState2Entered, "State2 should not have entered");
    }

    // ==================== 4. Timer Callbacks During Detach ====================

    [UnityTest]
    public IEnumerator DetachedGraph_TimerCallbacks_DoNotTrigger()
    {
      // Setup timer callbacks
      _detachState1.At(0.1f, () => _timerCallbackCalled = true);
      _detachState1.AtEvery(0.05f, () =>
      {
        _periodicCallbackCalled = true;
        _periodicCallbackCount++;
      });

      _stateMachine.Start();
      yield return null;
      
      // Detach immediately
      _stateMachine.DetachGraph(_detachableGraph);
      
      // Wait for callback times
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Callbacks should not have triggered
      Assert.IsFalse(_timerCallbackCalled, "Timer callback should not trigger when detached");
      Assert.IsFalse(_periodicCallbackCalled, "Periodic callback should not trigger when detached");
      Assert.AreEqual(0, _periodicCallbackCount, "Periodic callback count should be 0");
    }

    [UnityTest]
    public IEnumerator ReattachedGraph_TimerCallbacks_ResumeCorrectly()
    {
      // Setup timer callback with longer duration
      _detachState1.At(0.2f, () => _timerCallbackCalled = true);

      _stateMachine.Start();
      yield return null;
      
      // Wait partway through
      yield return new WaitForSeconds(0.1f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Detach and re-attach
      _stateMachine.DetachGraph(_detachableGraph);
      _stateMachine.AttachGraph(_detachableGraph);
      
      // Wait for remaining time
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine();
      yield return null;
      
      // Callback should have triggered (total time > 0.2f)
      Assert.IsTrue(_timerCallbackCalled, "Timer callback should trigger after re-attach");
    }

    // ==================== 5. Edge Cases ====================

    [UnityTest]
    public IEnumerator DetachGraph_AlreadyDetached_NoError()
    {
      _stateMachine.Start();
      yield return null;
      
      // Detach twice
      _stateMachine.DetachGraph(_detachableGraph);
      Assert.DoesNotThrow(() => _stateMachine.DetachGraph(_detachableGraph), 
        "Detaching already detached graph should not throw");
      
      // State should still be preserved
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "State should still be preserved");
    }

    [UnityTest]
    public IEnumerator AttachGraph_AlreadyAttached_NoError()
    {
      _stateMachine.Start();
      yield return null;
      
      // Attach twice (already attached)
      Assert.DoesNotThrow(() => _stateMachine.AttachGraph(_detachableGraph), 
        "Attaching already attached graph should not throw");
      
      // Should remain functional
      Assert.IsTrue(_detachableGraph.IsGraphActive, "Graph should remain active");
      Assert.AreEqual(_detachState1, _detachableGraph.CurrentUnit, "State should be unchanged");
    }

    [UnityTest]
    public IEnumerator DetachGraph_NullGraph_NoError()
    {
      _stateMachine.Start();
      yield return null;
      
      Assert.DoesNotThrow(() => _stateMachine.DetachGraph(null), 
        "Detaching null graph should not throw");
    }

    [UnityTest]
    public IEnumerator AttachGraph_NullGraph_ThrowsException()
    {
      _stateMachine.Start();
      yield return null;
      
      Assert.Throws<ArgumentNullException>(() => _stateMachine.AttachGraph(null), 
        "Attaching null graph should throw ArgumentNullException");
      yield return null;
    }

    // ==================== 6. Hierarchical Scenarios ====================

    [UnityTest]
    public IEnumerator DetachGraph_WithSubgraphs_PreservesHierarchy()
    {
      // Create parent state with subgraph
      var parentState = _detachableGraph.CreateState();
      var subGraph = parentState.CreateGraph();
      var subState1 = subGraph.CreateState();
      var subState2 = subGraph.CreateState();
      subGraph.InitialUnit = subState1;

      bool subState1Entered = false;
      bool subState2Entered = false;
      subState1.OnEnter = () => subState1Entered = true;
      subState2.OnEnter = () => subState2Entered = true;

      // Setup transition in parent state to subgraph
      (_detachState1 > parentState).Immediately();

      _stateMachine.Start();
      yield return null;
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.AreEqual(parentState, _detachableGraph.CurrentUnit, "Should be in parent state");
      Assert.IsTrue(subState1Entered, "Subgraph should have started");

      // Detach main graph
      _stateMachine.DetachGraph(_detachableGraph);

      // Hierarchy should be preserved
      Assert.AreEqual(parentState, _detachableGraph.CurrentUnit, "Parent state should be preserved");
      Assert.AreEqual(subState1, subGraph.CurrentUnit, "Subgraph state should be preserved");
      Assert.IsFalse(subGraph.IsGraphActive, "Subgraph should be inactive when parent detached");
    }

    [UnityTest]
    public IEnumerator DetachedGraph_SubgraphEvents_DoNotRespond()
    {
      // Create subgraph hierarchy
      var parentState = _detachableGraph.CreateState();
      var subGraph = parentState.CreateGraph();
      var subState = subGraph.CreateState();
      subGraph.InitialUnit = subState;

      bool subEventReceived = false;
      subState.On<TestDetachEvent>(evt => subEventReceived = true);

      (_detachState1 > parentState).Immediately();

      _stateMachine.Start();
      yield return null;
      _stateMachine.UpdateMachine();
      yield return null;

      // Verify subgraph event works when attached
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.IsTrue(subEventReceived, "Subgraph event should work when attached");

      // Detach and test subgraph event doesn't work
      _stateMachine.DetachGraph(_detachableGraph);
      subEventReceived = false;

      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.IsFalse(subEventReceived, "Subgraph event should NOT work when parent detached");
    }

    [UnityTest]
    public IEnumerator ReattachedGraph_SubgraphsResumeCorrectly()
    {
      // Create subgraph with timer transition
      var parentState = _detachableGraph.CreateState();
      var subGraph = parentState.CreateGraph();
      var subState1 = subGraph.CreateState();
      var subState2 = subGraph.CreateState();
      subGraph.InitialUnit = subState1;

      bool subState2Entered = false;
      subState2.OnEnter = () => subState2Entered = true;

      BasicTransition.Connect(subState1, subState2, elapsedTime => elapsedTime > 0.2f);
      (_detachState1 > parentState).Immediately();

      _stateMachine.Start();
      yield return null;
      _stateMachine.UpdateMachine();
      yield return null;

      // Wait partway through subgraph timer
      yield return new WaitForSeconds(0.1f);
      _stateMachine.UpdateMachine();
      yield return null;

      // Detach and re-attach
      _stateMachine.DetachGraph(_detachableGraph);
      _stateMachine.AttachGraph(_detachableGraph);

      // Wait for remaining time
      yield return new WaitForSeconds(0.15f);
      _stateMachine.UpdateMachine();
      yield return null;

      // Subgraph transition should have completed
      Assert.IsTrue(subState2Entered, "Subgraph transition should complete after re-attach");
      Assert.AreEqual(subState2, subGraph.CurrentUnit, "Should be in subState2");
    }

    // ==================== 7. Multiple Detach/Attach Cycles ====================

    [UnityTest]
    public IEnumerator MultipleDetachAttachCycles_PreservesState()
    {
      _stateMachine.Start();
      yield return null;

      var originalState = _detachableGraph.CurrentUnit;

      // Perform multiple detach/attach cycles
      for (int i = 0; i < 5; i++)
      {
        _stateMachine.DetachGraph(_detachableGraph);
        Assert.IsFalse(_detachableGraph.IsGraphActive, $"Cycle {i}: Graph should be detached");
        Assert.AreEqual(originalState, _detachableGraph.CurrentUnit, $"Cycle {i}: State should be preserved");

        _stateMachine.AttachGraph(_detachableGraph);
        Assert.IsTrue(_detachableGraph.IsGraphActive, $"Cycle {i}: Graph should be re-attached");
        Assert.AreEqual(originalState, _detachableGraph.CurrentUnit, $"Cycle {i}: State should still be preserved");
      }
    }

    [UnityTest]
    public IEnumerator MultipleDetachAttachCycles_EventSubscriptionsStable()
    {
      int eventCallCount = 0;
      _detachState1.On<TestDetachEvent>(evt => eventCallCount++);

      _stateMachine.Start();
      yield return null;

      // Test initial event works
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.AreEqual(1, eventCallCount, "Initial event should work");

      // Multiple cycles
      for (int i = 0; i < 3; i++)
      {
        _stateMachine.DetachGraph(_detachableGraph);
        _stateMachine.AttachGraph(_detachableGraph);

        eventCallCount = 0;
        EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
        yield return null;
        Assert.AreEqual(1, eventCallCount, $"Cycle {i}: Event should work after re-attach");
      }
    }

    [UnityTest]
    public IEnumerator MultipleDetachAttachCycles_NoMemoryLeak()
    {
      // This test checks that multiple cycles don't accumulate listeners
      int totalEventCalls = 0;
      _detachState1.On<TestDetachEvent>(evt => totalEventCalls++);

      _stateMachine.Start();
      yield return null;

      // Perform multiple detach/attach cycles
      for (int i = 0; i < 5; i++)
      {
        _stateMachine.DetachGraph(_detachableGraph);
        _stateMachine.AttachGraph(_detachableGraph);
      }

      // Single event should only trigger once (no duplicate subscriptions)
      totalEventCalls = 0;
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      
      Assert.AreEqual(1, totalEventCalls, "Event should only be called once (no duplicate subscriptions)");
    }

    // ==================== 8. Cross-Graph Communication ====================

    [UnityTest]
    public IEnumerator DetachedGraph_DoesNotReceiveStateMachineLocalEvents()
    {
      // Setup listeners on both graphs
      bool mainGraphEventReceived = false;
      bool detachableGraphEventReceived = false;

      _mainState1.On<TestAttachEvent>(evt => mainGraphEventReceived = true);
      _detachState1.On<TestAttachEvent>(evt => detachableGraphEventReceived = true);

      _stateMachine.Start();
      yield return null;

      // Test both graphs receive StateMachine LocalRaise when attached
      _stateMachine.LocalRaise(new TestAttachEvent { Message = "test" });
      yield return null;

      Assert.IsTrue(mainGraphEventReceived, "Main graph should receive StateMachine LocalRaise");
      Assert.IsTrue(detachableGraphEventReceived, "Detachable graph should receive StateMachine LocalRaise when attached");

      // Detach one graph and reset flags
      _stateMachine.DetachGraph(_detachableGraph);
      mainGraphEventReceived = false;
      detachableGraphEventReceived = false;

      // Test only attached graph receives StateMachine LocalRaise
      _stateMachine.LocalRaise(new TestAttachEvent { Message = "detached_test" });
      yield return null;

      Assert.IsTrue(mainGraphEventReceived, "Main graph should still receive StateMachine LocalRaise");
      Assert.IsFalse(detachableGraphEventReceived, "Detached graph should NOT receive StateMachine LocalRaise");
    }

    [UnityTest]
    public IEnumerator DetachedGraph_CannotSendLocalEvents()
    {
      bool mainGraphEventReceived = false;
      _mainState1.On<TestDetachEvent>(evt => mainGraphEventReceived = true);

      _stateMachine.Start();
      yield return null;

      // Detach graph
      _stateMachine.DetachGraph(_detachableGraph);

      // Detached graph tries to send local event
      Assert.Throws<ObjectDisposedException>(() => 
        _detachableGraph.LocalRaise(new TestDetachEvent()),
        "Detached graph should not be able to send local events");
    }

    [UnityTest]
    public IEnumerator CrossGraph_GlobalEventBus_WorksIndependentOfAttachment()
    {
      bool mainGraphEventReceived = false;
      bool detachableGraphEventReceived = false;

      _mainState1.On<TestDetachEvent>(evt => mainGraphEventReceived = true);
      _detachState1.On<TestDetachEvent>(evt => detachableGraphEventReceived = true);

      _stateMachine.Start();
      yield return null;

      // Detach one graph
      _stateMachine.DetachGraph(_detachableGraph);

      // Global event should still reach attached graph, but not detached
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;

      Assert.IsTrue(mainGraphEventReceived, "Attached graph should receive global events");
      Assert.IsFalse(detachableGraphEventReceived, "Detached graph should NOT respond to global events");
    }

    // ==================== 9. Resource Management ====================

    [UnityTest]
    public IEnumerator DetachGraph_CleansUpTimerCallbacks()
    {
      int callbackCount = 0;
      _detachState1.At(0.1f, () => callbackCount++);
      _detachState1.AtEvery(0.05f, () => callbackCount++);

      _stateMachine.Start();
      yield return null;

      // Detach graph
      _stateMachine.DetachGraph(_detachableGraph);

      // Wait for callback times and force updates
      yield return new WaitForSeconds(0.2f);
      _stateMachine.UpdateMachine();
      yield return null;

      Assert.AreEqual(0, callbackCount, "Timer callbacks should not execute when detached");
    }

    [UnityTest]
    public IEnumerator DetachGraph_PreservesEventSubscriptions()
    {
      // This test verifies subscriptions are preserved but inactive
      bool eventReceived = false;
      _detachState1.On<TestDetachEvent>(evt => eventReceived = true);

      _stateMachine.Start();
      yield return null;

      // Verify subscription works
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.IsTrue(eventReceived, "Event should work initially");

      // Detach and verify subscription is preserved but inactive
      _stateMachine.DetachGraph(_detachableGraph);
      eventReceived = false;

      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.IsFalse(eventReceived, "Event should not work when detached");

      // Re-attach and verify subscription works again
      _stateMachine.AttachGraph(_detachableGraph);
      EventBus<TestDetachEvent>.Raise(new TestDetachEvent());
      yield return null;
      Assert.IsTrue(eventReceived, "Event should work again after re-attach");
    }

    // ==================== 10. Integration with StateMachine Lifecycle ====================

    [UnityTest]
    public IEnumerator StateMachineExit_vs_DetachGraph_Behavior()
    {
      _stateMachine.Start();
      yield return null;

      var originalState = _detachableGraph.CurrentUnit;

      // Test DetachGraph preserves state
      _stateMachine.DetachGraph(_detachableGraph);
      Assert.AreEqual(originalState, _detachableGraph.CurrentUnit, "DetachGraph should preserve state");
      Assert.IsFalse(_detachableGraph.IsGraphActive, "Graph should be inactive after detach");

      // Re-attach and test StateMachine.Exit() behavior
      _stateMachine.AttachGraph(_detachableGraph);
      _stateMachine.Exit();

      Assert.AreEqual(originalState, _detachableGraph.CurrentUnit, "Exit should preserve state");
      Assert.IsFalse(_detachableGraph.IsGraphActive, "Graph should be inactive after Exit");

      // Restart should work
      _stateMachine.Start();
      yield return null;
      Assert.IsTrue(_detachableGraph.IsGraphActive, "Graph should be active after restart");
    }

    [UnityTest]
    public IEnumerator StateMachineDispose_vs_DetachGraph_Behavior()
    {
      _stateMachine.Start();
      yield return null;

      // Detach graph first
      _stateMachine.DetachGraph(_detachableGraph);
      var preservedState = _detachableGraph.CurrentUnit;

      // Dispose StateMachine
      _stateMachine.Dispose();

      // Detached graph should still be accessible (not disposed)
      Assert.AreEqual(preservedState, _detachableGraph.CurrentUnit, "Detached graph should survive StateMachine disposal");
      
      // But accessing disposed StateMachine should throw
      Assert.Throws<ObjectDisposedException>(() => _stateMachine.Start(), 
        "Disposed StateMachine should throw on access");
    }

    [UnityTest]
    public IEnumerator DetachedGraph_SurvivesStateMachineRestart()
    {
      _stateMachine.Start();
      yield return null;

      var originalState = _detachableGraph.CurrentUnit;

      // Detach graph
      _stateMachine.DetachGraph(_detachableGraph);

      // Exit and restart StateMachine
      _stateMachine.Exit();
      _stateMachine.Start();
      yield return null;

      // Detached graph should still be preserved
      Assert.AreEqual(originalState, _detachableGraph.CurrentUnit, "Detached graph should survive StateMachine restart");
      Assert.IsFalse(_detachableGraph.IsGraphActive, "Detached graph should remain detached after restart");

      // Re-attach should work
      _stateMachine.AttachGraph(_detachableGraph);
      Assert.IsTrue(_detachableGraph.IsGraphActive, "Re-attach should work after StateMachine restart");
    }
  }
} 