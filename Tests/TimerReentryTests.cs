using System;
using Nopnag.EventBusLib;
using NUnit.Framework;

namespace Nopnag.StateMachineLib.Tests
{
  public sealed class TimerReentryTests
  {
    public enum TimerKind
    {
      Scheduled,
      Periodic,
      FixedScheduled,
      FixedPeriodic
    }

    sealed class Restart : BusEvent { }

    StateMachine _machine;
    StateGraph _graph;
    StateUnit _state;

    [SetUp]
    public void SetUp()
    {
      _machine = new StateMachine();
      _graph = _machine.CreateGraph();
      _state = _graph.CreateState();
    }

    [TearDown]
    public void TearDown()
    {
      _machine.Dispose();
    }

    [Test]
    public void ReentryPreservesNewDeadlineAndFixedDispatch(
      [Values] TimerKind kind, [Values] bool visitAnotherState)
    {
      var other = _graph.CreateState();
      var calls = 0;
      var callbackReturns = 0;
      Schedule(_state, kind, 0.25f, () =>
      {
        calls++;
        if (calls == 1)
        {
          if (visitAnotherState) _graph.StartState(other);
          _graph.StartState(_state);
        }
        callbackReturns++;
      });
      _machine.Start();

      // Four periods are due, but the first callback starts a fresh lifetime.
      Tick(kind, 1f);

      Assert.AreSame(_state, _graph.CurrentUnit);
      Assert.AreEqual(1, calls, "The old periodic catch-up must stop at re-entry.");
      Assert.AreEqual(1, callbackReturns, "Re-entry must not unwind user callback code.");
      Assert.AreEqual(0f, _state.DeltaTimeSinceStart);
      Assert.AreEqual(0f, _state.FixedElapsed);
      if (IsFixed(kind)) Assert.IsTrue(_state.RequiresFixedTickDispatch);

      Tick(kind, 0.125f);
      Assert.AreEqual(1, calls, "The new timer must wait for its own deadline.");
      Tick(kind, 0.125f);
      Assert.AreEqual(2, calls, "Old bookkeeping must not consume or postpone the new timer.");
      if (kind == TimerKind.FixedScheduled)
        Assert.IsFalse(_state.RequiresFixedTickDispatch,
          "The re-armed one-shot must leave the fast path eligible after it fires.");

      Tick(kind, 0.25f);
      Assert.AreEqual(IsPeriodic(kind) ? 3 : 2, calls);
    }

    [Test]
    public void StateChangeStopsRemainingTimersTransitionsAndHostedTicks(
      [Values] TimerKind kind, [Values] bool reenterSameState)
    {
      var target = reenterSameState ? _state : _graph.CreateState();
      var unreachable = _graph.CreateState();
      var remainingTimers = 0;
      var transitionChecks = 0;
      var stateTicks = 0;
      var childTicks = 0;
      var targetTicks = 0;
      var child = _state.CreateGraph().CreateState();
      _state.OnUpdate = _ => stateTicks++;
      _state.OnFixedTick = (_, __) => stateTicks++;
      child.OnUpdate = _ => childTicks++;
      child.OnFixedTick = (_, __) => childTicks++;
      if (!reenterSameState)
      {
        target.OnUpdate = _ => targetTicks++;
        target.OnFixedTick = (_, __) => targetTicks++;
      }
      if (IsFixed(kind))
        (_state > unreachable).WhenFixed(_ => { transitionChecks++; return false; });
      else
        (_state > unreachable).When(_ => { transitionChecks++; return false; });
      Schedule(_state, kind, 0.25f, () => _graph.StartState(target));
      Schedule(_state, kind, 0.25f, () => remainingTimers++);
      // One-shots run before periodic timers; this also covers crossing timer lists.
      if (!IsPeriodic(kind))
        Schedule(_state, IsFixed(kind) ? TimerKind.FixedPeriodic : TimerKind.Periodic,
          0.25f, () => remainingTimers++);
      _machine.Start();
      transitionChecks = 0;

      Tick(kind, 1f);

      Assert.AreSame(target, _graph.CurrentUnit);
      Assert.AreEqual(0, remainingTimers);
      Assert.AreEqual(reenterSameState && !IsFixed(kind) ? 1 : 0, transitionChecks,
        "Only the new synchronous entry may evaluate its Update transitions.");
      Assert.AreEqual(0, stateTicks);
      Assert.AreEqual(0, childTicks);
      Assert.AreEqual(0, targetTicks, "The new state must not consume the old tick's delta.");
      Assert.AreEqual(0f, target.DeltaTimeSinceStart);
      Assert.AreEqual(0f, target.FixedElapsed);
    }

    [Test]
    public void ExitOrDisposalStopsTimerDispatch(
      [Values] TimerKind kind, [Values] bool dispose)
    {
      var calls = 0;
      var remainingTimers = 0;
      var ticks = 0;
      _state.OnUpdate = _ => ticks++;
      _state.OnFixedTick = (_, __) => ticks++;
      Schedule(_state, kind, 0.25f, () =>
      {
        calls++;
        if (dispose) _machine.Dispose();
        else _graph.ExitGraph();
      });
      Schedule(_state, kind, 0.25f, () => remainingTimers++);
      _machine.Start();

      Assert.DoesNotThrow(() => Tick(kind, 1f),
        "A timer may exit/dispose its owner without touching cleared timer storage afterward.");

      Assert.AreEqual(1, calls);
      Assert.AreEqual(0, remainingTimers);
      Assert.AreEqual(0, ticks);
      if (dispose)
        Assert.Throws<ObjectDisposedException>(() => Tick(kind, 0.25f));
      else
      {
        Assert.IsNull(_graph.CurrentUnit);
        Tick(kind, 0.25f);
        Assert.AreEqual(1, calls);
      }
    }

    [Test]
    public void LocalEventReentryPreservesTimer([Values] TimerKind kind)
    {
      var calls = 0;
      var entries = 0;
      var restart = new Restart();
      _state.OnEnter = () => entries++;
      (_state > _state).On<Restart>();
      Schedule(_state, kind, 0.25f, () =>
      {
        calls++;
        if (calls == 1) _graph.LocalRaise(restart);
      });
      _machine.Start();

      Tick(kind, 1f);
      Assert.AreEqual(2, entries, "The event must synchronously re-enter the state.");
      Assert.AreEqual(1, calls);
      Tick(kind, 0.25f);
      Assert.AreEqual(2, calls);
    }

    [Test]
    public void ZeroTimeEntryReentryStopsOuterEntryButPreservesNestedEntry()
    {
      var calls = 0;
      var remainingCalls = 0;
      var transitionChecks = 0;
      var target = _graph.CreateState();
      _state.At(0f, () =>
      {
        calls++;
        if (calls == 1) _graph.StartState(_state);
      });
      _state.At(0f, () => remainingCalls++);
      (_state > target).When(_ => { transitionChecks++; return false; });

      _machine.Start();

      Assert.AreEqual(2, calls, "The new entry's zero-time callback runs synchronously.");
      Assert.AreEqual(1, remainingCalls);
      Assert.AreEqual(1, transitionChecks, "The superseded outer entry must stop.");
      Assert.AreSame(_state, _graph.CurrentUnit);
    }

    [TestCase(TimerKind.Periodic)]
    [TestCase(TimerKind.FixedPeriodic)]
    public void UninterruptedPeriodicTimerStillCatchesUp(TimerKind kind)
    {
      var calls = 0;
      Schedule(_state, kind, 0.25f, () => calls++);
      _machine.Start();

      Tick(kind, 1f);
      Assert.AreEqual(4, calls);
      Tick(kind, 0.125f);
      Assert.AreEqual(4, calls);
      Tick(kind, 0.125f);
      Assert.AreEqual(5, calls);
    }

    static bool IsFixed(TimerKind kind) =>
      kind == TimerKind.FixedScheduled || kind == TimerKind.FixedPeriodic;

    static bool IsPeriodic(TimerKind kind) =>
      kind == TimerKind.Periodic || kind == TimerKind.FixedPeriodic;

    static void Schedule(StateUnit state, TimerKind kind, float delay, Action callback)
    {
      switch (kind)
      {
        case TimerKind.Scheduled: state.At(delay, callback); break;
        case TimerKind.Periodic: state.AtEvery(delay, callback); break;
        case TimerKind.FixedScheduled: state.AtFixed(delay, callback); break;
        case TimerKind.FixedPeriodic: state.AtEveryFixed(delay, callback); break;
        default: throw new ArgumentOutOfRangeException(nameof(kind));
      }
    }

    void Tick(TimerKind kind, float delta)
    {
      if (IsFixed(kind)) _machine.FixedUpdateMachine(delta);
      else _machine.UpdateMachine(delta);
    }
  }
}
