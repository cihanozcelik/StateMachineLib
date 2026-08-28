using System;
using System.Collections;
using Nopnag.EventBusLib;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Nopnag.StateMachineLib.Tests
{
  public class FixedTimeAndLocalScaleTests
  {
    sealed class FrozenStateEvent : BusEvent
    {
    }

    StateMachine _stateMachine;
    StateGraph _graph;
    StateUnit _state;

    [SetUp]
    public void SetUp()
    {
      _stateMachine = new StateMachine();
      _graph = _stateMachine.CreateGraph();
      _state = _graph.CreateState();
    }

    [TearDown]
    public void TearDown()
    {
      _stateMachine.Dispose();
    }

    [Test]
    public void ExplicitUpdateAndFixedTicksAdvanceIndependentClocks()
    {
      _stateMachine.Start();

      _stateMachine.UpdateMachine(0.3f);
      Assert.AreEqual(0.3f, _state.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.3f, _state.DeltaTime, 0.0001f);
      Assert.AreEqual(0f, _state.FixedElapsed, 0.0001f);

      _stateMachine.FixedUpdateMachine(0.02f);
      Assert.AreEqual(0.3f, _state.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.02f, _state.FixedDeltaTime, 0.0001f);
      Assert.AreEqual(0.02f, _state.FixedElapsed, 0.0001f);
    }

    [Test]
    public void LegacyAndNewFixedCallbacksKeepTheirOwnContracts()
    {
      var legacyElapsed = -1f;
      var fixedDelta = -1f;
      var fixedElapsed = -1f;
      _state.OnFixedUpdate = elapsed => legacyElapsed = elapsed;
      _state.OnFixedTick = (delta, elapsed) =>
      {
        fixedDelta = delta;
        fixedElapsed = elapsed;
      };

      _stateMachine.Start();
      _stateMachine.UpdateMachine(0.4f);
      _stateMachine.FixedUpdateMachine(0.02f);

      Assert.AreEqual(0.4f, legacyElapsed, 0.0001f,
        "The existing OnFixedUpdate API must continue receiving Update elapsed time.");
      Assert.AreEqual(0.02f, fixedDelta, 0.0001f);
      Assert.AreEqual(0.02f, fixedElapsed, 0.0001f);
    }

    [Test]
    public void LocalTimeScaleScalesUpdateAndFixedTime()
    {
      _state.LocalTimeScale = 0.5f;
      _stateMachine.Start();

      _stateMachine.UpdateMachine(0.2f);
      _stateMachine.FixedUpdateMachine(0.04f);

      Assert.AreEqual(0.1f, _state.DeltaTime, 0.0001f);
      Assert.AreEqual(0.1f, _state.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.02f, _state.FixedDeltaTime, 0.0001f);
      Assert.AreEqual(0.02f, _state.FixedElapsed, 0.0001f);
    }

    [Test]
    public void ZeroLocalTimeScaleFreezesTicksTimersAndPollingTransitions()
    {
      var next = _graph.CreateState();
      var allowTransition = false;
      var updateCalls = 0;
      var fixedCalls = 0;
      var lateCalls = 0;
      var timerCalls = 0;
      _state.OnUpdate = _ => updateCalls++;
      _state.OnFixedTick = (_, __) => fixedCalls++;
      _state.OnLateUpdate = _ => lateCalls++;
      _state.At(0.1f, () => timerCalls++);
      (_state > next).When(_ => allowTransition);

      _stateMachine.Start();
      _state.LocalTimeScale = 0f;
      allowTransition = true;

      _stateMachine.UpdateMachine(1f);
      _stateMachine.FixedUpdateMachine(1f);
      _stateMachine.LateUpdateMachine();

      Assert.AreSame(_state, _graph.CurrentUnit);
      Assert.AreEqual(0f, _state.DeltaTimeSinceStart);
      Assert.AreEqual(0f, _state.FixedElapsed);
      Assert.AreEqual(0, updateCalls);
      Assert.AreEqual(0, fixedCalls);
      Assert.AreEqual(0, lateCalls);
      Assert.AreEqual(0, timerCalls);
    }

    [Test]
    public void FrozenTickReportsZeroDeltaInsteadOfRetainingPreviousDelta()
    {
      _stateMachine.Start();
      _stateMachine.UpdateMachine(0.2f);
      _stateMachine.FixedUpdateMachine(0.04f);
      _state.LocalTimeScale = 0f;

      _stateMachine.UpdateMachine(1f);
      _stateMachine.FixedUpdateMachine(1f);

      Assert.AreEqual(0f, _state.DeltaTime);
      Assert.AreEqual(0f, _state.FixedDeltaTime);
      Assert.AreEqual(0.2f, _state.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.04f, _state.FixedElapsed, 0.0001f);
    }

    [Test]
    public void FrozenStateStillProcessesEvents()
    {
      var handlerCalls = 0;
      _state.On<FrozenStateEvent>(_ => handlerCalls++);
      _state.LocalTimeScale = 0f;
      _stateMachine.Start();

      EventBus<FrozenStateEvent>.Raise(new FrozenStateEvent());

      Assert.AreEqual(1, handlerCalls);
      Assert.AreSame(_state, _graph.CurrentUnit);
    }

    [Test]
    public void FrozenStateCanStillTakeEventTransition()
    {
      var next = _graph.CreateState();
      (_state > next).On<FrozenStateEvent>();
      _state.LocalTimeScale = 0f;
      _stateMachine.Start();

      EventBus<FrozenStateEvent>.Raise(new FrozenStateEvent());

      Assert.AreSame(next, _graph.CurrentUnit);
    }

    [Test]
    public void AfterFixedUsesOnlyFixedElapsed()
    {
      var next = _graph.CreateState();
      (_state > next).AfterFixed(0.1f);
      _stateMachine.Start();

      _stateMachine.UpdateMachine(10f);
      Assert.AreSame(_state, _graph.CurrentUnit);

      _stateMachine.FixedUpdateMachine(0.04f);
      _stateMachine.FixedUpdateMachine(0.04f);
      Assert.AreSame(_state, _graph.CurrentUnit);

      _stateMachine.FixedUpdateMachine(0.02f);
      Assert.AreSame(next, _graph.CurrentUnit);
    }

    [Test]
    public void FixedTimersUseFixedElapsedAndRespectScale()
    {
      var atCalls = 0;
      var everyCalls = 0;
      _state.LocalTimeScale = 0.5f;
      _state.AtFixed(0.1f, () => atCalls++);
      _state.AtEveryFixed(0.05f, () => everyCalls++);
      _stateMachine.Start();

      _stateMachine.FixedUpdateMachine(0.2f);

      Assert.AreEqual(1, atCalls);
      Assert.AreEqual(2, everyCalls);
      Assert.AreEqual(0.1f, _state.FixedElapsed, 0.0001f);
    }

    [Test]
    public void ParentStateScalePropagatesToHostedGraphs()
    {
      var childGraph = _state.CreateGraph();
      var childState = childGraph.CreateState();
      _state.LocalTimeScale = 0.5f;
      childState.LocalTimeScale = 0.5f;
      _stateMachine.Start();

      _stateMachine.UpdateMachine(0.4f);
      _stateMachine.FixedUpdateMachine(0.08f);

      Assert.AreEqual(0.2f, _state.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.1f, childState.DeltaTimeSinceStart, 0.0001f);
      Assert.AreEqual(0.04f, _state.FixedElapsed, 0.0001f);
      Assert.AreEqual(0.02f, childState.FixedElapsed, 0.0001f);
    }

    [Test]
    public void FixedDeltaIsNotAppliedAgainToStateEnteredDuringTick()
    {
      var next = _graph.CreateState();
      (_state > next).AfterFixed(0.1f);
      _stateMachine.Start();

      _stateMachine.FixedUpdateMachine(0.1f);

      Assert.AreSame(next, _graph.CurrentUnit);
      Assert.AreEqual(0f, next.FixedElapsed, 0.0001f);
      Assert.AreEqual(0f, next.FixedDeltaTime, 0.0001f);
    }

    [Test]
    public void AnyStateAfterFixedUsesUpdatedFixedElapsed()
    {
      var next = _graph.CreateState();
      (_graph.FromAny(next)).AfterFixed(0.1f);
      _stateMachine.Start();

      _stateMachine.FixedUpdateMachine(0.1f);

      Assert.AreSame(next, _graph.CurrentUnit);
      Assert.AreEqual(0f, next.FixedElapsed, 0.0001f);
    }

    [Test]
    public void InvalidLocalTimeScaleFailsLoudly()
    {
      Assert.Throws<ArgumentOutOfRangeException>(() => _state.LocalTimeScale = -0.1f);
      Assert.Throws<ArgumentOutOfRangeException>(() => _state.LocalTimeScale = float.NaN);
      Assert.Throws<ArgumentOutOfRangeException>(() => _state.LocalTimeScale = float.PositiveInfinity);
    }

    [Test]
    public void TurnedOffMachineDoesNotRunManualFixedOrLateTicks()
    {
      var fixedCalls = 0;
      var lateCalls = 0;
      _state.OnFixedTick = (_, __) => fixedCalls++;
      _state.OnLateUpdate = _ => lateCalls++;
      _stateMachine.Start();
      _stateMachine.SetTurnedOn(false);

      _stateMachine.FixedUpdateMachine(1f);
      _stateMachine.LateUpdateMachine();

      Assert.AreEqual(0, fixedCalls);
      Assert.AreEqual(0, lateCalls);
      Assert.AreEqual(0f, _state.FixedElapsed);
    }

    [UnityTest]
    public IEnumerator LegacyClockDoesNotIncludeTurnedOffDurationAfterResume()
    {
      _stateMachine.Start();
      _stateMachine.UpdateMachine();
      var beforePause = _state.DeltaTimeSinceStart;

      _stateMachine.SetTurnedOn(false);
      yield return null;
      yield return null;
      _stateMachine.SetTurnedOn(true);
      _stateMachine.UpdateMachine();

      var resumedDelta = _state.DeltaTimeSinceStart - beforePause;
      Assert.Less(resumedDelta, 0.05f,
        "The legacy wall-clock API must rebase instead of adding disabled time.");
    }

    [Test]
    public void ExplicitTicksDoNotAllocateAfterInitialization()
    {
      var callbackCalls = 0;
      _state.OnUpdate = _ => callbackCalls++;
      _state.OnFixedTick = (_, __) => callbackCalls++;
      _stateMachine.Start();
      _stateMachine.UpdateMachine(0.016f);
      _stateMachine.FixedUpdateMachine(0.02f);

      GC.GetAllocatedBytesForCurrentThread();
      var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 0; i < 1000; i++)
      {
        _stateMachine.UpdateMachine(0.016f);
        _stateMachine.FixedUpdateMachine(0.02f);
      }

      var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
      Assert.AreEqual(0, allocated);
      Assert.AreEqual(2002, callbackCalls);
    }
  }
}
