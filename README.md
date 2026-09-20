# Nopnag StateMachineLib

StateMachineLib is a Unity state-flow library with parallel graphs, state-owned
hierarchical graphs, polling and event-driven transitions, local event propagation,
independent Update/FixedUpdate clocks, and an optional `MonoBehaviour` lifecycle
wrapper.

This README documents the supported public API for StateMachineLib `2.0.0`. Open
implementation defects and unsafe advanced paths are tracked separately in
[KNOWN_ISSUES.md](KNOWN_ISSUES.md). A known issue is not a supported usage
recommendation.

## Quick navigation

- [Choose manual or managed lifecycle](#choose-the-lifecycle-model-explicitly)
- [Understand ownership](#core-model-and-ownership)
- [Callbacks and execution order](#state-callbacks-and-exact-execution-order)
- [Time and timers](#time-and-timer-api)
- [Transitions](#transition-api)
- [Events](#state-scoped-event-listeners)
- [Graph lifetime](#graph-attachment-and-detachment)
- [Public API reference](#public-api-reference)
- [Attention checklist](#attention-checklist)

## Installation

| Package | Value |
|---|---|
| Unity package name | `com.nopnag.statemachinelib` |
| Package version | `2.0.0` |
| Declared minimum Unity version | `6000.1` |
| Runtime namespace | `Nopnag.StateMachineLib` |
| Runtime assembly | `Nopnag.StateMachineLib.Runtime` |

StateMachineLib requires EventBusLib. The package manifest currently declares:

```json
{
  "dependencies": {
    "com.nopnag.eventbuslib": "1.1.0"
  }
}
```

Install the package through Unity Package Manager with the repository URL, or add it
to the consuming project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.nopnag.statemachinelib":
      "https://github.com/cihanozcelik/StateMachineLib.git#<tested-revision>"
  }
}
```

Pin an exact tested revision in production projects rather than following a moving
branch. Ensure the pinned StateMachineLib revision is compatible with the installed
EventBusLib revision.

Repository contributors can install the local push-protection hooks with:

```bash
.githooks/install.sh
```

## Choose the lifecycle model explicitly

The library supports two valid integration models. Managed auto-start is optional.

### Manual `StateMachine`

Use `new StateMachine()` when another owner already controls initialization, ticking,
pause, shutdown, or dependency-injection lifetime.

```csharp
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib;

public sealed class EncounterFlow
{
    private readonly StateMachine _machine;

    public EncounterFlow()
    {
        _machine = new StateMachine();

        StateGraph graph = _machine.CreateGraph();
        StateUnit waiting = graph.CreateState();
        StateUnit fighting = graph.CreateState();

        waiting.OnEnter = EnterWaiting;
        fighting.OnEnter = EnterFighting;
        (waiting > fighting).On<EncounterStartedEvent>();

        graph.InitialUnit = waiting;
    }

    public void Start() => _machine.Start();
    public void Tick(float deltaTime) => _machine.UpdateMachine(deltaTime);
    public void FixedTick(float fixedDeltaTime) =>
        _machine.FixedUpdateMachine(fixedDeltaTime);
    public void LateTick() => _machine.LateUpdateMachine();

    public void Shutdown()
    {
        _machine.Exit();
        _machine.Dispose();
    }

    private static void EnterWaiting() { }
    private static void EnterFighting() { }
}
```

Manual ownership rules:

- Construct the complete topology before `Start` whenever possible.
- Call one Update, FixedUpdate, and LateUpdate driver at most once per corresponding
  owner tick.
- `UpdateMachine`, `FixedUpdateMachine`, and `LateUpdateMachine` call `Start`
  automatically if the machine has not started. Call `Start` explicitly when entry
  timing matters.
- `Exit` and `Reset` retain topology and subscriptions and are restartable.
- `Dispose` is terminal. Clear the consumer's reference after disposal.

### Managed `MonoBehaviour` integration

Use `CreateManagedStateMachine` when one `MonoBehaviour` should own exactly one
machine and the library should drive Unity lifecycle automatically.

```csharp
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib;
using UnityEngine;

public sealed class EnemyBrain : MonoBehaviour
{
    private StateMachine _machine;

    private void Awake()
    {
        _machine = this.CreateManagedStateMachine(BuildMachine);
    }

    private static void BuildMachine(StateMachine machine)
    {
        StateGraph graph = machine.CreateGraph();
        StateUnit idle = graph.CreateState();
        StateUnit chasing = graph.CreateState();

        (idle > chasing).On<PlayerDetectedEvent>();
        graph.InitialUnit = idle;
    }
}
```

Managed behavior:

- `StateMachineWrapper.GetOrCreate(gameObject)` gets or adds one wrapper component to
  the GameObject.
- `GetOrCreate` performs `GetComponent` and may perform `AddComponent`; call managed
  creation only during an owned initialization boundary, never as an interactive
  runtime lookup.
- `CreateManagedStateMachine(setup)` delegates to that wrapper and creates at most one
  machine per owner `MonoBehaviour`.
- Setup runs synchronously. If the owner is enabled and active, `Start` also runs
  synchronously before `CreateManagedStateMachine` returns.
- A second call for the same owner returns the existing machine; the second setup
  callback is not invoked.
- The wrapper drives `UpdateMachine`, `FixedUpdateMachine`, and `LateUpdateMachine`.
- Disabling the owner or GameObject turns machine power off without exiting the
  current state. Re-enabling resumes the preserved state and rebases Update clocks.
- Wrapper destruction exits and disposes registered machines. Individual permanent
  removal must use `StateMachineWrapper.RemoveStateMachineFor(owner)`.
- Calling `Exit` directly on a registered machine is not a durable stop while its
  owner remains enabled; a later wrapper tick starts it again.
- Do not call `Dispose` directly on a still-registered managed machine. The wrapper
  would retain and continue ticking the disposed instance.
- In the Unity Editor, the current wrapper `OnValidate` appends a machine count to the
  GameObject name and can dirty or truncate intentional bracketed names. See
  [SM-016](KNOWN_ISSUES.md#sm-016--editor-validation-mutates-the-owning-gameobject-name).

Important managed timing constraints are recorded under
[Known issues](KNOWN_ISSUES.md#managed-wrapper-lifecycle-issues). In particular,
initially disabled owners do not have a universal “started before owner `OnEnable`”
guarantee.

## Core model and ownership

The normal hierarchy is:

```text
StateMachine
└── StateGraph                    one concurrent flow
    └── StateUnit                 exactly one current state per graph
        └── StateGraph            hierarchical flow active with its owner state
            └── StateUnit
```

A `StateMachine` can host multiple top-level graphs. They run concurrently in hosted
list order. A `StateUnit` can host multiple child graphs; those graphs start, tick,
and exit through that state's lifecycle methods.

The current child-graph power node does not incorporate the owning state's
`BaseGraph.CurrentUnit == state` condition. A child graph can therefore report active
after its parent state exits even though normal ticking stopped. Do not use child
`IsGraphActive` as a substitute for checking the parent state, and avoid graph-level
any-state global event transitions in such a child until
[SM-017](KNOWN_ISSUES.md#sm-017--state-hosted-child-graph-power-does-not-reflect-whether-the-parent-state-is-current)
is fixed.

Supported ownership invariants:

- Every graph has one authoritative host.
- The host topology is an acyclic tree.
- Every transition's source and target states belong to the same `StateGraph`.
- `InitialUnit` belongs to the graph on which it is assigned.
- Use `StateMachine` for top-level graphs and `StateUnit` for automatically driven
  hierarchical graphs.
- Although `StateGraph` also exposes `IGraphHost`, graphs attached directly to a
  `StateGraph` are not automatically driven by that graph's normal lifecycle. Treat
  this as a manual advanced API.
- Do not manipulate the power tree independently through `AttachChild`, `SetParent`,
  or `DetachChild` in ordinary state-machine code.

The current runtime does not enforce every invariant. See
[SM-001](KNOWN_ISSUES.md#sm-001--cross-graph-state-targets-are-not-rejected) and
[SM-002](KNOWN_ISSUES.md#sm-002--graph-ownership-is-not-exclusive-and-ancestor-cycles-are-possible).

## Basic construction

```csharp
StateMachine machine = new StateMachine();
StateGraph movement = machine.CreateGraph();

StateUnit idle = movement.CreateState();
StateUnit running = movement.CreateState();

idle.OnEnter = OnIdleEntered;
idle.OnUpdate = OnIdleUpdate;
idle.OnExit = OnIdleExited;

(idle > running).When(elapsed => ShouldRun());
(running > idle).When(elapsed => ShouldStop());

movement.InitialUnit = idle;
machine.Start();
```

`CreateState` automatically makes the first created state the initial state if
`InitialUnit` is null. Assigning `InitialUnit` explicitly is recommended for clarity.

`CreateUnit(string)` remains public for compatibility but is obsolete. `CreateState`
creates a state whose read-only `Name` is null; manage human-readable names externally
until the naming API is redesigned.

## State callbacks and exact execution order

### Callback properties

| API | Argument | Meaning |
|---|---|---|
| `OnEnter` | none | Called synchronously when the state starts. |
| `OnExit` | none | Called after the state's child graphs exit. |
| `OnUpdateBeforeTransitionCheck` | accumulated Update elapsed | Called after Update time advances and before timers/transitions. |
| `OnUpdate` | accumulated Update elapsed | Called only if no local Update transition fired. |
| `OnFixedUpdate` | accumulated Update elapsed | Legacy callback; its argument is not fixed time. |
| `OnFixedTick` | scaled fixed delta, accumulated fixed elapsed | Preferred physics-time callback. |
| `OnLateUpdate` | accumulated Update elapsed | Called before state-hosted child LateUpdate. |

The older public fields `EnterStateFunction`, `ExitStateFunction`,
`UpdateStateFunction`, `UpdateStateBeforeTransitionCheckFunction`,
`FixedUpdateStateFunction`, and `LateUpdateStateFunction` are obsolete aliases behind
the callback properties. Do not use both names for the same callback.

### Entry order

When `StartState` enters a state:

1. The previous current state's child graphs exit.
2. The previous state's `OnExit` runs.
3. The graph assigns the new `CurrentUnit` and resets its Update and Fixed clocks.
4. The new state's `OnEnter` runs.
5. Graphs hosted by the new `StateUnit` enter in hosted-list order.
6. One-shot and periodic callback bookkeeping resets.
7. Due Update callbacks whose target is zero or negative are evaluated.
8. Local Update polling transitions are evaluated at elapsed zero.

Entry is synchronous. `OnEnter`, zero-time callbacks, and immediate transitions can
all execute before `Start`, `StartState`, or managed creation returns.

### Update order

For an active, non-frozen graph:

1. Graph-level any-state Update transitions are checked with the current pre-tick
   elapsed value.
2. The current state consumes the tick delta once and advances scaled Update time.
3. `OnUpdateBeforeTransitionCheck` runs.
4. `At` callbacks run, followed by `AtEvery` catch-up callbacks.
5. State-local Update transitions are checked in list order.
6. If no transition fires, `OnUpdate` runs.
7. State-hosted child graphs receive the scaled delta.

If a transition changes state, the graph may continue evaluating the new state within
the same graph tick. One explicit delta is consumed by at most one state. The graph's
per-tick polling loop is capped at ten state changes and logs a warning when exceeded.
Immediate transition recursion during state entry is a separate known issue and must
be kept acyclic.

### FixedUpdate order

1. The current state's scaled fixed clock advances once.
2. Graph-level any-state fixed transitions are checked.
3. `AtFixed` callbacks run, followed by `AtEveryFixed` catch-up callbacks.
4. State-local fixed transitions are checked.
5. Legacy `OnFixedUpdate(DeltaTimeSinceStart)` runs.
6. `OnFixedTick(FixedDeltaTime, FixedElapsed)` runs.
7. State-hosted child graphs receive the scaled fixed delta.

One fixed delta is consumed by at most one state even if a transition changes state
during the fixed tick.

### LateUpdate and exit order

LateUpdate runs `OnLateUpdate` first and then state-hosted child graphs. It is skipped
for inactive or time-frozen states.

Exit runs state-hosted child graph exits first and the state's `OnExit` afterward.

## Time and timer API

Each state exposes four clocks:

| Property | Contract |
|---|---|
| `DeltaTime` | Scaled delta consumed by the latest Update tick. |
| `DeltaTimeSinceStart` | Accumulated scaled Update time since entry. |
| `FixedDeltaTime` | Scaled delta consumed by the latest FixedUpdate tick. |
| `FixedElapsed` | Accumulated scaled fixed time since entry. |

All four reset on every state entry. Update and Fixed clocks are independent.

Parameterless ticking reads Unity time:

```csharp
machine.UpdateMachine();
machine.FixedUpdateMachine();
machine.LateUpdateMachine();
```

Explicit deltas are available for an authoritative external clock or deterministic
simulation:

```csharp
machine.UpdateMachine(updateDelta);
machine.FixedUpdateMachine(fixedDelta);
```

Pass only finite values greater than or equal to zero.

### Timed callbacks

```csharp
state.At(0.20f, OpenWindow);
state.AtEvery(0.10f, EmitPulse);
state.AtFixed(0.20f, OpenPhysicsWindow);
state.AtEveryFixed(0.10f, EmitPhysicsPulse);
```

- `At` and `AtFixed` run once per state entry when their clock reaches the target.
- `AtEvery` and `AtEveryFixed` repeat and catch up once per missed interval.
- Timers reset when the state is re-entered.
- Register timers during topology construction, not while gameplay is interactive.
- Use non-null callbacks, finite non-negative one-shot targets, and finite positive
  periodic intervals.
- A large elapsed jump can cause an unbounded periodic catch-up loop. Bound external
  deltas or use a purpose-built bounded callback policy.

### Per-state local time scale

```csharp
state.LocalTimeScale = 1.0f;
state.LocalTimeScale = 0.5f;
state.LocalTimeScale = 0.0f;
```

`LocalTimeScale` must be finite and non-negative. It scales Update and Fixed clocks,
timers, polling transitions, Update/Fixed/Late execution, and deltas passed to
state-hosted child graphs. `OnEnter` and `OnExit` are lifecycle callbacks and are not
time-scaled. Nested state scales multiply through already-scaled parent deltas.

A zero scale freezes Update, FixedUpdate, LateUpdate, timers, polling transitions, and
child ticks. The state remains active for EventBus listeners and event-driven
transitions. Use `SetTurnedOn(false)` when event handling must pause as well.

The scale is configuration and persists across state re-entry; elapsed clocks reset.

## Transition API

All transition delegates and transition objects should be constructed before the
machine starts. Transition order is definition/list order; the first matching polling
transition wins.

### Conditional and delayed Update transitions

```csharp
(idle > moving).When(elapsed => input.HasMovement);
(attacking > recovery).After(0.35f);
```

`When` receives `DeltaTimeSinceStart`. `After(duration)` creates a predicate equivalent
to `elapsed >= duration`. Supply a finite duration greater than or equal to zero.

The reversed syntax `(target < source)` is supported for a single target and means the
same as `(source > target)`. The `<` variants for arrays, dynamic targets, and any-state
markers intentionally throw `NotSupportedException`; use their documented `>` forms.

### Fixed transitions

```csharp
(grounded > airborne).WhenFixed(fixedElapsed => !motor.IsGrounded);
(attack > recovery).AfterFixed(0.25f);
```

Fixed predicates receive `FixedElapsed` and are checked only during FixedUpdate.
Supply a finite non-negative value to `AfterFixed`.

### Event transitions

```csharp
using Nopnag.EventBusLib;

(waiting > active).On<EncounterStartedEvent>();
(active > failed).On<EncounterFailedEvent>(evt => evt.Fatal);

EventQuery<ItemCollectedEvent> playerItems =
    EventBus<ItemCollectedEvent>.Where<CollectorRoute>(playerRoute);

(searching > found).On(playerItems, evt => evt.IsQuestItem);
```

Event transitions are push-based and synchronous. They do not wait for the next
machine tick. A transition listens on both the global `EventBus<T>` root/query and the
source graph's local bus.

The `EventQuery` overload currently applies its route query only on the global bus; its
local subscription is type-only. Until
[SM-006](KNOWN_ISSUES.md#sm-006--filtered-state-listeners-and-transitions-are-unfiltered-on-local-buses)
is fixed, include a predicate over strongly typed event data when local filtered
delivery is possible.

There is no `.On(ref Action)`, `.On(ref Action<T>)`, or `TransitionByAction` API. Use a
typed `BusEvent` for discrete transitions.

### Immediate transitions

```csharp
(entry > ready).Immediately();
```

A state-local immediate transition is evaluated during entry at elapsed zero. Do not
create self-targeting or cyclic immediate chains.

An any-state immediate transition is checked by the graph Update polling loop, not by
`EnterGraph` itself.

### Indexed targets

```csharp
StateUnit[] choices = { attack, defend, retreat };

(decision > choices).When(elapsed => SelectChoiceIndex());
(fixedDecision > choices).WhenFixed(fixedElapsed => SelectFixedChoiceIndex());
```

The predicate returns an array index. Values outside `[0, Length)` produce no
transition. The target array must be non-null, non-empty, contain no null states, and
all targets must belong to the source graph.

### Dynamic targets

```csharp
(decision > StateGraph.DynamicTarget).When(elapsed => SelectTargetOrNull());
(physicsDecision > StateGraph.DynamicTarget)
    .WhenFixed(fixedElapsed => SelectFixedTargetOrNull());
```

Returning null means no transition. A returned state must belong to the source graph.

### Any-state transitions

```csharp
(StateGraph.Any > dead).On<DiedEvent>();
(StateGraph.Any > stunned).When(elapsed => status.IsStunned);

graph.FromAny(recovering).After(1.0f);
graph.FromAnyToDynamic().When(elapsed => SelectGlobalTargetOrNull());
```

Any-state Update transitions are checked before the active state's Update processing.
Any-state Fixed transitions are checked after the fixed clock advances but before
state-local fixed timers/transitions. Any-state targets are required to belong to the
graph; `FromAny` validates this at setup.

### Low-level transition types

The following public types back the fluent API:

- `IStateTransition`
- `BasicTransition`
- `DirectTransition`
- `ConditionalTransition`
- `ConditionalTransitionByIndex`
- static `TransitionByEvent`
- `TransitionConfigurator`
- `MultiTargetTransitionConfigurator`
- `DynamicTargetTransitionConfigurator`
- `AnyStateMarker` and `DynamicTargetMarker`

Their public `Connect` methods and transition lists remain available for compatibility
and custom tooling. Gameplay code should prefer the fluent API because it selects the
correct Update/fixed/any-state collection. Do not mutate `Transitions`,
`FixedTransitions`, target arrays, or graph transition topology after startup.

| Low-level type | Public surface |
|---|---|
| `IStateTransition` | `TargetUnit`, `TargetUnitName`, `SourceUnitName`, and `CheckTransition`. |
| `BasicTransition` | `Predicate`/target/name inspection plus `Connect` and `ConnectFixed` for state or any-state sources. |
| `DirectTransition` | Target/name inspection plus `Connect` for state or any-state sources. |
| `ConditionalTransition` | Dynamic `Predicate`, dynamic target/name inspection, and Update/fixed `Connect`; returns the created transition. |
| `ConditionalTransitionByIndex` | `Predicate`, `TargetStateInfos`, target/name inspection, and state-local Update/fixed `Connect`. |
| `TransitionByEvent` | Static `Connect` overloads for state/any-state, optional predicate, and optional query. |

`DynamicTargetTransitionConfigurator` also has a public constructor accepting one
source `StateUnit`; its any-state constructor is internal. The other configurators are
created by the fluent operators or graph methods.

## State-scoped event listeners

```csharp
state.On<DamageTakenEvent>(OnDamageTaken);
state.On(filteredDamageQuery, OnFilteredDamageTaken);
```

`StateUnit.On<T>` subscribes during setup but invokes the supplied listener only while
that state is active. It listens to:

- the global `EventBus<T>` root or supplied global query;
- the state's base graph local bus;
- the state host's own local bus.

The older `Listen` overloads are obsolete aliases.

A state entered by an event transition does not receive that same event raise. The
graph stores the event's `RaiseUniqueId` at entry and rejects another event-driven
transition or newly activated state handler for that ID.

One event raise can transition each graph at most once, but it may independently
transition multiple parallel graphs and graphs in different machines.

Configuration order matters because underlying EventBus listeners run in registration
order. If an active-state handler is registered before the transition listener, it can
handle the event before the transition. If the transition runs first, the old-state
handler later observes that it is inactive.

## Local event propagation

Every `IGraphHost` owns a separate `LocalEventBus`. Local events are forwarded
downward; buses are not shared and events do not bubble upward.

```csharp
machine.LocalRaise(reusableEncounterEvent);
activeState.LocalRaise(reusableStateLocalEvent);
```

Propagation order is:

1. Listeners on the current host's local bus.
2. Each hosted graph in hosted-list order.
3. The same host-first traversal recursively within that graph hierarchy.

`StopPropagation` stops remaining listeners on the current query path and prevents
forwarding into remaining descendants or later sibling graphs.

Each host hop performs a separate top-level `LocalEventBus.Raise` after the previous
hop returns, so `RaiseUniqueId` is reassigned at each level. Do not treat one ID as a
hierarchy-wide identifier.

Forwarding follows only that host's `HostedGraphs` list. It does not automatically
cross from a `StateGraph` into graphs owned by its current `StateUnit`. Therefore:

- `machine.LocalRaise` reaches the machine's top-level graph buses and directly hosted
  graph links, including handlers/transitions in those graphs.
- `state.LocalRaise` reaches that state's own local listeners and graphs hosted by that
  state.
- A deeper state-owned hierarchy needs an explicit raise through each intended owning
  state scope; one machine-local raise does not traverse every active state boundary.
- Event transitions listen to their source graph's local bus, not the source state's
  own local bus. Raise through the graph/its direct ancestor for those transitions.

See [SM-022](KNOWN_ISSUES.md#sm-022--local-forwarding-does-not-automatically-cross-active-state-host-boundaries).

Call local raise only through an active authoritative owner whose attached graph list
contains no inactive graph. `GraphHost` does not skip inactive children, while
`StateGraph.LocalRaise` throws on an inactive graph. A paused machine or one
individually turned-off attached graph can therefore make local forwarding fail after
the host bus has already received the event. See
[SM-018](KNOWN_ISSUES.md#sm-018--local-forwarding-throws-when-an-attached-child-graph-is-inactive).

Use event type names that make local scope visible, for example
`CharacterLocalAttackRequestedEvent`, and never publish a local-only event type on the
global bus.

## Graph attachment and detachment

### Create or attach before start

```csharp
StateGraph graph = machine.CreateGraph();

StateGraph prepared = new StateGraph();
prepared.InitialUnit = prepared.CreateState();
machine.AttachGraph(prepared);
```

`CreateGraph` creates and attaches. `AttachGraph` turns the graph on and establishes
power ownership but does not call `EnterGraph`.

Construct and attach fresh graphs before the host starts. If a fresh graph is attached
after host start, it must be entered exactly once by the authoritative lifecycle owner;
calling `StateMachine.Start` again is a no-op while the machine remains started.

### Detach and reattach

```csharp
machine.DetachGraph(graph);
// graph is paused with its current state and subscriptions preserved
machine.AttachGraph(graph);
// graph resumes the preserved state; no new OnEnter is issued
```

Detach:

- removes the graph from the host list;
- disconnects the power parent and turns the graph off;
- does not call `ExitGraph`;
- preserves current state, timers, callbacks, and subscriptions.

`StateMachine.RemoveGraph` is only an alias for detach; it is not disposal.

Do not permanently abandon a detached graph. Global EventBus subscriptions can retain
it, and there is no public `StateGraph.Dispose`. Do not reattach a graph after its
former parent has disposed it; use a new graph.

`HostedGraphs` returns an `IReadOnlyList<StateGraph>`, but the current getter allocates
a new read-only wrapper on each access. Do not poll it during gameplay.

## Power, pause, exit, reset, and disposal

`IPoweredNode` exposes:

- `HasPower`: power reaches the node from an active parent, or it is an enabled power
  source.
- `IsTurnedOn`: the node's local switch.
- `IsActive`: `HasPower && IsTurnedOn`, with `StateUnit` additionally requiring that it
  is its graph's current state.

`SetTurnedOn(false)` on a `StateMachine` or `StateGraph` is a pause. It does not exit or
reset the current state. Turning the machine or graph back on rebases Update clocks so
paused wall-clock duration is not consumed on resume.

`StateUnit.SetTurnedOn` is a low-level power-tree API, not a supported way to pause the
current state's callbacks: the owning `StateGraph` does not consult the state's power
switch before invoking its current unit. It can suppress active-state event handling
and power to child graphs while Update callbacks continue. Pause the owning graph or
machine instead.

Do not depend on turning a graph off before host start to suppress initial entry;
current `StartAllGraphs` enters every attached graph. See
[SM-009](KNOWN_ISSUES.md#sm-009--turned-off-graphs-still-enter-during-host-start).

### Machine lifecycle matrix

| Operation | Topology/subscriptions | State callbacks | Restartable | Notes |
|---|---|---|---|---|
| `Start` | retained | enters all hosted graphs once | already started: no-op | Synchronous. |
| Tick method before start | retained | calls `Start`, then ticks | yes | Lazy auto-start. |
| `SetTurnedOn(false)` | retained | no exit | yes | Pause; event handling becomes inactive. |
| `Exit` | retained | exits current states | yes | Powers off after graph exits. |
| `Reset` | retained | exits current states | yes | Powers off before graph exits. |
| `Dispose` | attached graph subscriptions/timers cleared | exits attached current states | no | Terminal and idempotent. |

Lifecycle callbacks are not exception-atomic. They must not throw. `Exit` and `Reset`
also differ in power timing during `OnExit`; do not raise state-changing events from
exit callbacks.

After `Dispose`, do not inspect power as evidence of liveness and do not call any API
other than an idempotent repeated `Dispose`. Current post-disposal guards are
inconsistent; see [SM-011](KNOWN_ISSUES.md#sm-011--disposed-machines-can-still-report-active-power-state).

## Public API reference

### `StateMachine`

| Member | Purpose |
|---|---|
| constructor | Creates an unstarted manual machine and local bus host. |
| `CreateGraph`, `AttachGraph`, `DetachGraph`, `RemoveGraph` | Manage top-level graph ownership; remove means detach. |
| `HostedGraphs` | Read-only view; currently allocates on access. |
| `Start` | Enter attached graphs once and power the machine on. |
| `UpdateMachine` / `(float)` | Lazy-start then drive Update. |
| `FixedUpdateMachine` / `(float)` | Lazy-start then drive FixedUpdate. |
| `LateUpdateMachine` | Lazy-start then drive LateUpdate. |
| `UpdateAllGraphs` / `(float)` | Drive attached graphs without machine auto-start. |
| `FixedUpdateAllGraphs` / `(float)` | Fixed-drive attached graphs without auto-start. |
| `LateUpdateAllGraphs` | Late-drive attached graphs without auto-start. |
| `LocalRaise<T>` | Raise and forward a local event downward. |
| `SetTurnedOn` | Pause/resume power without exit. |
| `Exit`, `Reset` | Restartable exit variants with different power timing. |
| `Dispose` | Terminal cleanup for attached graphs and subscriptions. |
| `HasPower`, `IsTurnedOn`, `IsActive` | Power-state inspection. |

The `*AllGraphs` methods are advanced driver APIs. Prefer the `*Machine` methods for a
normal manual machine.

### `StateGraph`

| Member | Purpose |
|---|---|
| constructor | Creates an unattached, turned-on graph. |
| `CreateState` | Creates a state and selects it as initial if none exists. |
| obsolete `CreateUnit(name)` | Legacy named-state creation. |
| `InitialUnit` | Mutable initial state; must belong to this graph. |
| `CurrentUnit` | Current state; throws after parent disposal. |
| `EnterGraph`, `ExitGraph` | Synchronous manual entry/exit. |
| `StartState` | Advanced synchronous state change; target must belong to graph. |
| `UpdateGraph` / `(float)` | Drive graph Update; explicit form validates delta. |
| `FixedUpdateGraph` / `(float)` | Drive graph FixedUpdate; explicit form validates delta. |
| `LateUpdateGraph` | Drive graph LateUpdate. |
| `FromAny`, `FromAnyToDynamic` | Configure any-state transitions. |
| `GetCurrentStateName`, `IsUnitActive` | Inspection helpers. |
| `CreateGraph`, `AttachGraph`, `DetachGraph`, `*AllGraphs` | Manual direct graph-host API; not automatically integrated with normal graph lifecycle. |
| `LocalRaise<T>` | Raise locally; requires active graph. |
| `SetTurnedOn`, `HasPower`, `IsTurnedOn`, `IsActive`, `IsGraphActive` | Pause and inspect graph power; `IsGraphActive` aliases `IsActive`. |
| static `Any`, `DynamicTarget` | Fluent transition markers. |

### `StateUnit`

| Member | Purpose |
|---|---|
| `BaseGraph`, `Name` | Read-only owning graph and optional legacy name. |
| callback properties | Entry, exit, Update, FixedUpdate, and LateUpdate behavior. |
| clock properties | Latest/accumulated scaled Update and Fixed values. |
| `LocalTimeScale` | Non-negative Update/Fixed/child-graph scale. |
| `At`, `AtEvery`, `AtFixed`, `AtEveryFixed` | Per-entry timed callbacks. |
| `On<T>` overloads | Active-state global and local event listening. |
| obsolete `Listen<T>` overloads | Compatibility aliases for `On`. |
| `CreateGraph`, `AttachGraph`, `DetachGraph` | Automatic hierarchical graph ownership. |
| `HostedGraphs`, `*AllGraphs` | Hosted graph view and advanced driving methods. |
| `LocalRaise<T>` | Raise on the state host bus and forward downward. |
| `SetTurnedOn`, `SetParent`, `AttachChild`, `DetachChild` | Low-level power API; ordinary code should use graph-host methods. |
| `Transitions`, `FixedTransitions` | Public mutable transition lists; setup/tooling only. |
| fluent comparison operators | Build supported transition configurators. |

### `IGraphHost`, `IPoweredNode`, and `PoweredNode`

`IGraphHost` is implemented by `StateMachine`, `StateGraph`, and `StateUnit`. It exposes
hosted graphs, a local bus through the interface, attach/detach/create, downward local
raise, and graph-driving methods.

`IPoweredNode` and `PoweredNode` expose the underlying parent/child power tree. They
are public for composition and testing, but StateMachineLib consumers should not build
a second ownership topology with them.

| `PoweredNode` member | Purpose |
|---|---|
| constructor `(isPowerSource = false)` | Creates a node initially turned off; power sources gain power only when turned on. |
| `AttachChild`, `DetachChild` | Mutate child power relationships; duplicate same-parent attachment is ignored. |
| `SetParent` | Replace the parent pointer and refresh descendant power. |
| `SetTurnedOn` | Change the local switch and refresh descendants. |
| `RefreshPowerState` | Recompute `HasPower` and recurse through children. |
| `HasPower`, `IsTurnedOn`, `IsActive` | Inspect power state. |

### `StateMachineWrapper` and `MonoBehaviourExtensions`

- `MonoBehaviourExtensions.CreateManagedStateMachine` is the normal managed entry
  point.
- `StateMachineWrapper.GetOrCreate` gets or adds the wrapper component.
- `StateMachineWrapper.CreateStateMachineFor` performs synchronous setup and managed
  registration.
- `StateMachineWrapper.RemoveStateMachineFor` is the permanent managed-removal path.

`StateMachineWrapper` currently lives in the global namespace; the extension method is
in `Nopnag.StateMachineLib`.

## Allocation and performance contract

StateMachineLib does not promise that arbitrary API use is allocation-free.

The existing automated allocation test covers only a warmed, manually driven,
explicit-delta Update and FixedUpdate path on a small prepared topology. It does not
cover managed wrapper ticks, first use, events, setup mutation, attach/detach, shutdown,
or user callbacks.

Allocate and prepare before interactive runtime:

- machines, graphs, states, transitions, target arrays, and callback delegates;
- timer registrations and event listener/transition subscriptions;
- local EventBus event types and route queries;
- list capacities and the managed wrapper path for the maximum owner count;
- concrete callback branches, Unity bindings, and consumer resources.

Do not during interactive runtime:

- add states, graphs, transitions, timers, listeners, or query topology;
- grow target arrays or public transition lists;
- capture new lambdas or construct new events;
- poll `HostedGraphs`;
- assume logging/error paths are allocation-free.

Static inspection cannot establish Unity runtime allocation freedom. Profile the
warmed real path in Play Mode and on the target device.

## Failure, null, and threading contract

- The library is intended for Unity main-thread ownership. Its mutable lists, graphs,
  wrapper collections, and callbacks are not thread-safe.
- Required graphs, states, predicates, queries, events, and callbacks should be
  non-null. Validation is inconsistent across low-level APIs; fail at the consumer's
  initialization boundary.
- Explicit graph deltas must be finite and non-negative.
- Every graph must have a non-null initial state before entry. A missing initial state
  currently logs a warning instead of creating a valid current state.
- `AtEvery`/`AtEveryFixed` require finite positive intervals.
- Callback and predicate exceptions normally propagate in manual use. Wrapper removal
  and destruction catch and log several lifecycle exceptions; a log does not prove
  cleanup completed.
- Do not depend on warning-and-return behavior for invalid topology. Construct and
  validate topology before any state mutation or start.

## Attention checklist

Before shipping a StateMachineLib flow, verify all of the following:

- The integration is deliberately manual or deliberately managed; auto-start was not
  assumed from another code sample.
- Each managed owner calls creation once and permanent removal goes through the wrapper.
- Every graph has one host and the topology is acyclic.
- Every initial/source/target state belongs to the same graph.
- Fresh graphs are attached before start; detached graphs are not abandoned.
- Hierarchical graphs are hosted by the owning `StateUnit`.
- Immediate transition chains terminate.
- All callbacks, predicates, timers, transitions, and listeners are constructed before
  interactive runtime.
- Local filtered listeners/transitions use an explicit event predicate until SM-006 is
  fixed.
- Local events are raised only from an active owner and their scope is visible in the
  event type name.
- State-hosted child graph activity is authorized by the owning current state, not by
  the child's `IsGraphActive` property alone.
- Lifecycle callbacks do not throw or publish reentrant state-changing events.
- Shutdown uses the correct owner: managed removal for managed machines, and
  `Exit` plus `Dispose` for terminal manual ownership.
- The actual warmed gameplay and rare paths have been checked in the Unity Profiler.

## Test coverage map

The repository tests provide evidence for:

- normal polling, immediate, dynamic, indexed, any-state, and event transitions;
- graph-local one-transition-per-event-raise isolation;
- parallel graph independence;
- state-owned subgraph lifecycle;
- detach/reattach state and subscription preservation;
- local downward propagation stop across descendants and siblings;
- power propagation and common pause/resume behavior;
- independent Update/Fixed clocks, local time scaling, and fixed callbacks;
- disposal unsubscription on covered paths;
- one narrow warmed explicit-tick allocation path.

They do not cover every known issue. Consult [KNOWN_ISSUES.md](KNOWN_ISSUES.md) before
expanding lifecycle, dynamic topology, filtered local events, or strict allocation
claims.

## Session lifetime regression coverage

SessionLifetimeTests exercises manual start/dispose, synchronous managed start,
disabled-owner pause/resume, owner-only destruction, wrapper destruction, and five
successive owner lifetimes using global/local transitions. Disposal is checked before
ClearAll so bus cleanup cannot mask leaked StateMachine subscriptions. These tests
require the companion EventBusLib ClearAll revision. StateMachine runtime code has
not changed. They do not replace five actual Editor Play/Stop cycles with scene reload
on and domain reload off, or target-device allocation profiling.
