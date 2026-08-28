# StateMachineLib Known Issues

This document records implementation defects, unsafe public escape hatches, lifecycle
surprises, and test gaps found during a source audit of revision `c6501e9`.

It is deliberately separate from the supported API contract:

- An item here is not automatically a release blocker for every consumer.
- Consumers should use the safe-usage guidance unless their use case reaches the item.
- When an item becomes relevant, fix it in this library with a focused regression test
  instead of adding an application-level workaround.
- Source evidence confirms the described implementation path. Where no regression test
  exists, the item is explicitly marked as a test gap rather than a tested guarantee.

## Correctness and ownership defects

### SM-001 — Cross-graph state targets are not rejected

**Severity:** Critical  
**Status:** Open; source-confirmed, missing negative tests

`InitialUnit`, `StartState`, normal transitions, dynamic-target transitions, and
indexed transitions do not consistently verify that every state belongs to the graph
performing the transition. A foreign state can become `CurrentUnit` while its
`BaseGraph` continues to point to another graph. Later transitions then act through
the wrong owner and can corrupt both graphs' state flow.

**Safe usage:** Every initial, source, and target state must come from the same
`StateGraph`. Treat cross-graph transitions as unsupported even where the runtime does
not throw.

**Evidence:**

- `Runtime/StateGraph.cs:28,304-324`
- `Runtime/Util/TransitionConfigurator.cs:18-27`
- `Runtime/Transition/BasicTransition.cs:33-75`
- `Runtime/Transition/DirectTransition.cs:23-35`
- `Runtime/Transition/ConditionalTransition.cs:27-60`
- `Runtime/Transition/ConditionalTransitionByIndex.cs:38-57`

**Missing coverage:** Foreign `InitialUnit`, `StartState`, specific, dynamic, and
indexed target rejection.

### SM-002 — Graph ownership is not exclusive and ancestor cycles are possible

**Severity:** Critical  
**Status:** Open; source-confirmed, missing topology tests

`GraphHost.AttachGraph` only checks whether the graph already exists in that one
host's list. `PoweredNode.SetParent` replaces the parent pointer without removing the
node from the old parent's child list. Consequently, one graph can remain registered
with multiple hosts. Detaching it from either host can break the other host's power
relationship.

Only direct self-parenting is rejected. Attaching an ancestor below its descendant can
create a cycle, after which recursive power refresh can overflow the stack.

**Safe usage:** A graph must have exactly one host. Detach it from the current host
before attaching it elsewhere. Keep the power/graph topology an acyclic tree and use
`AttachGraph`/`DetachGraph` rather than low-level `IPoweredNode` methods.

**Evidence:**

- `Runtime/GraphHost.cs:25-35,51-61`
- `Runtime/PoweredNode.cs:19-34,41-55`
- `Tests/PoweredNodeEdgeCaseTests.cs:38-87`

**Missing coverage:** Two-host attachment, reparent cleanup, indirect cycles, and
cycle-safe failure behavior.

### SM-003 — Immediate transition cycles can bypass the per-tick safety cap

**Severity:** Critical  
**Status:** Open; source-confirmed, missing recursion tests

State entry synchronously evaluates zero-time polling transitions. A self-targeting
`Immediately()` transition or an `A -> B -> A` immediate cycle recursively calls
`StartState` from inside `StateUnit.Start`. This path does not pass through the
`MAX_STATE_CHANGES_PER_UPDATE` loop and can cause stack overflow.

Callbacks can also synchronously raise events or call `StartState` while an outer
entry, exit, timer, or update callback is still executing. The outer callback path is
not cancelled when reentrant code changes the current state.

**Safe usage:** Immediate transition graphs must be acyclic. Do not call `StartState`
or synchronously raise state-changing events from lifecycle callbacks unless the
reentrancy has a tested, terminating design.

**Evidence:**

- `Runtime/StateGraph.cs:30,304-324,352-397`
- `Runtime/StateUnit.cs:517-601,707-716`

**Missing coverage:** Immediate self-loop, multi-state immediate cycle, and state
changes from `OnEnter`, `OnExit`, timers, and update callbacks.

### SM-004 — Start, exit, disposal, and managed setup are not exception-atomic

**Severity:** Critical  
**Status:** Open; source-confirmed, missing failure-injection tests

`StateMachine.Start` marks the machine started before entering graphs. An `OnEnter`
exception can leave a partially entered machine whose next `Start` is a no-op.
`StateMachine.Exit` turns power off only after all exit callbacks finish; an exception
can leave the machine active and started. `GraphHost.Dispose` marks itself disposed
only after graph exit and cleanup complete, so one throwing callback can prevent
remaining cleanup and unsubscription.

The managed wrapper stores a new machine before invoking the setup callback and does
not roll it back when setup throws. `RemoveStateMachineFor` wraps `Exit` and `Dispose`
in one `try`; if `Exit` throws, `Dispose` is skipped even though the registration is
removed.

**Safe usage:** Lifecycle and setup callbacks must not throw. Validate required data
before creating or starting the machine. Do not assume cleanup completed after a
logged wrapper exception.

**Evidence:**

- `Runtime/StateMachine.cs:130-186`
- `Runtime/GraphHost.cs:140-175`
- `Runtime/StateMachineWrapper.cs:27-62,90-109,140-175`

**Missing coverage:** Exceptions in setup, enter, exit, timer callbacks, graph cleanup,
and disposal of multiple attached graphs.

### SM-005 — `StateGraph`-hosted graphs do not receive automatic lifecycle driving

**Severity:** High  
**Status:** Open design/API inconsistency

`StateGraph` publicly implements `IGraphHost` and exposes graph creation, attachment,
and update methods. However, its normal `EnterGraph`, `UpdateGraph`, `ExitGraph`, and
parent-disposal paths do not automatically start, tick, exit, or dispose graphs hosted
directly by that `StateGraph`. Full hierarchical lifecycle driving exists for graphs
hosted by a `StateUnit`.

**Safe usage:** Build automatic hierarchical flows with `StateUnit.CreateGraph()` or
`StateUnit.AttachGraph()`. Treat direct `StateGraph` hosting as a manually driven,
advanced API until this inconsistency is resolved.

**Evidence:**

- `Runtime/IGraphHost.cs:6-10`
- `Runtime/StateGraph.cs:40-41,73-108,150-158,184-195,326-397,465-470`
- `Runtime/StateUnit.cs:517-528,598-624,740-761`

**Missing coverage:** Automatic and manual lifecycle expectations for graphs hosted
directly by a `StateGraph`.

### SM-006 — Filtered state listeners and transitions are unfiltered on local buses

**Severity:** High  
**Status:** Open correctness defect; source-confirmed, missing local mismatch tests

The `EventQuery<T>` overloads use the supplied query for their global subscription,
but subscribe to the base graph and state-local buses through unfiltered `On<T>()`
queries. A listener or transition configured for one route value can therefore accept
a locally raised event with a different route value.

**Safe usage:** Do not rely on `EventQuery` filtering for local state listeners or
local event transitions until the library is fixed. Use an event predicate that
checks strongly typed event data when local delivery is required.

**Evidence:**

- `Runtime/StateUnit.cs:358-389`
- `Runtime/Transition/TransitionByEvent.cs:92-139,186-230`
- Existing query tests raise globally in `Tests/StateMachineTests.cs`

**Missing coverage:** Matching and non-matching filtered events raised through every
relevant local host level.

### SM-007 — Detached subscribed graphs can be retained with no public disposal path

**Severity:** High  
**Status:** Open lifetime defect

Detach is a pause operation: it preserves the current state, timers, and event
subscriptions. State listeners and event transitions also subscribe to the static
global EventBus. Those delegates can keep a detached graph reachable. A detached
`StateGraph` has no public `Dispose` method, and it is no longer included in its former
host's later disposal.

The test named `MultipleDetachAttachCycles_NoMemoryLeak` checks duplicate callback
counts; it does not measure reachability or garbage collection.

**Safe usage:** Do not permanently abandon a detached graph. Keep an explicit owner
and reattach it before owner disposal, or structure permanent lifetimes around a
dedicated `StateMachine` that can be disposed.

**Evidence:**

- `Runtime/GraphHost.cs:51-61,140-175`
- `Runtime/StateUnit.cs:317-389,748-761`
- `Runtime/Transition/TransitionByEvent.cs:46-230`
- `Tests/GraphHostDetachAttachTests.cs:685-707,813-889`

**Missing coverage:** `WeakReference`/GC reachability after detach and a supported
permanent-removal lifecycle.

### SM-008 — A fresh graph attached after host start is not entered automatically

**Severity:** High  
**Status:** Open behavior gap; reattach behavior is tested, fresh late attach is not

Attachment establishes list and power ownership but does not call `EnterGraph`.
`StateMachine.Start` is idempotent once the machine is started, so calling it again
does not enter a newly attached graph. Reattaching a previously entered graph is
different: it intentionally resumes its preserved current state without another
`OnEnter`.

**Safe usage:** Prefer constructing and attaching fresh graphs before `Start`. If a
fresh graph must be attached to an already-started active host, explicitly enter it
under one authoritative lifecycle owner. Do not manually re-enter a resumed graph.

**Evidence:**

- `Runtime/GraphHost.cs:25-35,177-185`
- `Runtime/StateMachine.cs:28-40,178-186`
- `Tests/GraphHostDetachAttachTests.cs:194-212`

**Missing coverage:** Fresh late attachment versus preserved reattachment for every
host type.

### SM-009 — Turned-off graphs still enter during host start

**Severity:** High  
**Status:** Open behavior/documentation mismatch

`StartAllGraphs` calls `EnterGraph` for every hosted graph without checking power or
`IsTurnedOn`. A graph turned off before machine or parent-state start still runs its
initial `OnEnter`, starts state-hosted child graphs, and evaluates zero-time callbacks
and polling transitions. Subsequent ticks remain inactive.

**Safe usage:** Do not depend on pre-start `SetTurnedOn(false)` to suppress initial
entry side effects. Delay attachment or make entry eligibility explicit in the owner.

**Evidence:**

- `Runtime/StateMachine.cs:178-186`
- `Runtime/GraphHost.cs:177-185`
- `Runtime/StateGraph.cs:184-188`
- `Runtime/StateUnit.cs:517-564`

**Missing coverage:** Turned-off top-level and state-hosted graph start.

### SM-010 — Disposed graphs can be partially resurrected by attachment

**Severity:** High  
**Status:** Open lifetime defect

Parent disposal marks a graph disposed and clears its subscriptions, timers, and
state-hosted graph resources. A later `AttachGraph` unconditionally clears the graph's
disposed-by-parent flag. The graph becomes accessible again even though important
internal resources were already permanently cleared or disposed.

**Safe usage:** A graph that has participated in parent disposal is terminal. Never
reattach or reuse it; create a new graph and topology.

**Evidence:**

- `Runtime/GraphHost.cs:25-35,166-175`
- `Runtime/StateGraph.cs:465-475`
- `Runtime/StateUnit.cs:748-761`

**Missing coverage:** Reattachment attempts after parent disposal.

### SM-011 — Disposed machines can still report active power state

**Severity:** High  
**Status:** Open lifecycle consistency defect

`StateMachine.Dispose` disposes its `GraphHost` but does not turn off the machine's
`PoweredNode` or detach powered children. Public `IsActive` can therefore remain true
after disposal, and `SetTurnedOn` has no disposed guard. Other methods have mixed
post-disposal behavior: some throw, while `Exit` and repeated `Dispose` return.

**Safe usage:** Treat disposal as terminal regardless of power properties. Clear the
consumer's machine reference and never call any other API afterward.

**Evidence:**

- `Runtime/StateMachine.cs:55-62,79-114,130-204`
- `Runtime/GraphHost.cs:166-175`

**Missing coverage:** Complete post-disposal API matrix, inactive status, and power
reference release.

## Managed-wrapper lifecycle issues

### SM-012 — Managed creation is synchronous and duplicate setup is ignored

**Severity:** High  
**Status:** Open documentation and failure-handling gap

For an active owner, managed creation registers the machine, invokes setup, and calls
`Start` before `CreateManagedStateMachine` returns. `OnEnter` can therefore execute
before the caller's assignment of the returned machine field completes.

Only one machine is stored per owner `MonoBehaviour`. A second call returns the
existing machine and does not execute the new setup callback. If the first setup
callback throws, the partially configured registration remains stored.

**Safe usage:** Call managed creation once per owner, normally from a controlled
initialization boundary. Callbacks must not read the caller's machine field during
synchronous entry; pass required references directly into setup instead.

**Evidence:** `Runtime/StateMachineWrapper.cs:27-62`

**Missing coverage:** Duplicate creation callback count, field-assignment reentrancy,
and setup rollback.

### SM-013 — Initially disabled owner start timing is not uniformly `OnEnable`

**Severity:** High  
**Status:** Open lifecycle timing gap

Machines created for disabled owners are placed in a pending set. That set is drained
by the wrapper's `OnEnable`, not by the individual owner's `OnEnable`. If only the
owner component becomes enabled while the wrapper remains enabled, the machine starts
lazily on the next wrapper update/fixed/late tick through `UpdateMachine`'s automatic
start. The existing test waits a frame and therefore does not distinguish these
timings.

The wrapper pending drain also starts every pending machine without rechecking the
individual owner at that moment.

**Safe usage:** Do not require the machine to have entered before an initially disabled
owner's first wrapper-driven tick. If `OnEnable` must raise events into it, establish
and test an explicit startup boundary.

**Evidence:**

- `Runtime/StateMachineWrapper.cs:112-129,225-254`
- `Runtime/StateMachine.cs:139-162,189-204`
- `Tests/StateMachineTests.cs:1582-1609`

**Missing coverage:** Owner-only enable with event raise from owner `OnEnable`, pending
owner disabled again before wrapper enable, and Fixed/Late being the first tick.

### SM-014 — Manual `Exit` is not a durable stop for a registered managed machine

**Severity:** High  
**Status:** Open documentation/API-ownership gap

`Exit` clears the started flag. If the owner remains enabled, the next wrapper tick
calls a machine tick method, which automatically calls `Start` and enters the machine
again. Directly disposing a still-registered managed machine is also unsafe because
the wrapper retains it and will call throwing tick methods every frame.

**Safe usage:** Pause through owner/wrapper power control. Permanently remove a managed
machine through `StateMachineWrapper.RemoveStateMachineFor`, not direct `Exit` or
`Dispose`.

**Evidence:**

- `Runtime/StateMachine.cs:130-162,189-204`
- `Runtime/StateMachineWrapper.cs:86-109,180-193,225-254`

**Missing coverage:** Manual exit/dispose while owner stays enabled.

### SM-015 — Destroyed-owner cleanup can retain a disposed dictionary entry

**Severity:** High  
**Status:** Open Unity fake-null cleanup risk

Per-frame cleanup detects a destroyed Unity owner and disposes its machine. It removes
the dictionary entry only under `owner != null`; Unity's destroyed-object fake-null
semantics can make this condition false, leaving the disposed machine in the mapping
and revisiting it every frame. The comment says this path skips `Exit`, but
`StateMachine.Dispose` still exits attached graphs through `GraphHost.DisposeAllGraphs`.

**Safe usage:** Do not rely on owner-component-only destruction cleanup until this path
has a regression test. Prefer removing the managed machine explicitly while the owner
is still valid.

**Evidence:**

- `Runtime/StateMachineWrapper.cs:206-223`
- `Runtime/StateMachine.cs:55-62`
- `Runtime/GraphHost.cs:166-172`
- `Tests/StateMachineTests.cs:1466-1495`

**Missing coverage:** Destroying only the owner component while leaving the GameObject
and wrapper alive.

### SM-016 — Editor validation mutates the owning GameObject name

**Severity:** Medium  
**Status:** Open editor-side-effect issue

`StateMachineWrapper.OnValidate` rewrites its GameObject name to append the managed
machine count. It splits the current name at the first `[` character, trims it, and
replaces the name. This can destroy an intentional bracketed suffix, dirty scenes or
prefabs, and create unrelated source-control changes merely by validation/inspection.

**Safe usage:** Do not use the wrapper's mutated GameObject name as an identifier.
Avoid relying on validation without scene/prefab dirtiness until the debug display is
moved to a custom inspector or made explicitly opt-in.

**Evidence:** `Runtime/StateMachineWrapper.cs:195-203`

**Missing coverage:** Editor tests for name preservation and scene/prefab dirty state.

### SM-017 — State-hosted child graph power does not reflect whether the parent state is current

**Severity:** High  
**Status:** Open power/lifecycle consistency defect

A `StateUnit` reports active only when it is its `BaseGraph.CurrentUnit`, but child
graphs are attached to the unit's composed raw `PoweredNode`. That raw node's
`IsActive` checks only inherited power and the local switch; it does not know whether
the `StateUnit` is current.

When a parent state exits, its child graphs are exited and stop receiving ticks because
only the current state is driven. Their power nodes can nevertheless remain active
while the parent graph stays active. The child graph can report `IsGraphActive == true`,
and a graph-level any-state event transition can synchronously start a child state in
response to a global event even though the owning parent state is inactive.

**Safe usage:** Do not use child `IsGraphActive` alone as proof that its owning state is
active. Until power ownership is fixed, avoid graph-level any-state global event
transitions in state-hosted child graphs and raise child-scope local events only through
an explicitly verified active parent state.

**Evidence:**

- `Runtime/PoweredNode.cs:37-63`
- `Runtime/StateUnit.cs:183-255,517-528,740-746`
- `Runtime/StateGraph.cs:110-148,184-195,286-397`
- `Runtime/Transition/TransitionByEvent.cs:31-43,142-230`

**Missing coverage:** Child graph power after parent-state exit, global any-state event
transition while the parent state is inactive, and power restoration on re-entry.

### SM-018 — Local forwarding throws when an attached child graph is inactive

**Severity:** High  
**Status:** Open pause/propagation inconsistency

`GraphHost.LocalRaise` forwards to every graph in its hosted list without checking
graph activity. `StateGraph.LocalRaise` rejects an inactive graph with
`InvalidOperationException`. Therefore, a local raise on an otherwise active host can
deliver to the host bus and then fail when it encounters an attached child that was
turned off or lost parent power. Later sibling scopes are not reached.

Turning off a machine while keeping graphs attached has the same problem if code calls
`machine.LocalRaise`: the machine method does not reject its own inactive power state,
but forwarding reaches inactive child graphs and throws.

**Safe usage:** Do not call local raise through a host whose attached graph list
contains an inactive graph. Do not publish local events while a machine/graph hierarchy
is paused. Detach intentionally excluded graphs or establish an owner-level publication
gate before raising.

**Evidence:**

- `Runtime/GraphHost.cs:98-112`
- `Runtime/StateGraph.cs:123-129`
- `Runtime/StateMachine.cs:93-97,109-114`

**Missing coverage:** Local raise with a turned-off first/middle child, paused machine,
and expected sibling behavior.

## Behavioral constraints and validation gaps

### SM-019 — `Exit` and `Reset` expose different event behavior during teardown

**Severity:** Medium  
**Status:** Current source behavior; not covered by focused tests

`Exit` exits graphs before turning machine power off. An `OnExit` callback can therefore
raise an event while the graph is still active and trigger transitions during teardown.
`Reset` turns power off before exiting graphs, suppressing active-state event handling
during exit. Both retain topology and subscriptions and are restartable. Any machine
tick after either operation lazily starts the machine again.

**Safe usage:** Do not raise state-changing events from `OnExit`. Select one lifecycle
operation based on an explicit owner policy, and use `Dispose` for terminal shutdown.

**Evidence:** `Runtime/StateMachine.cs:130-204`

**Missing coverage:** Event raises from `OnExit` under `Exit`, `Reset`, and disposal.

### SM-020 — Timer and delta validation is incomplete and inconsistent

**Severity:** Medium  
**Status:** Open validation gap

Graph explicit-delta methods reject negative, NaN, and infinite values. Machine
explicit-delta methods delegate validation to graphs; with zero hosted graphs, invalid
deltas are accepted silently. `At` and `AtFixed` accept negative, NaN, infinity, and
null callbacks. `AtEvery` and `AtEveryFixed` warn and skip only values `<= 0`; NaN is
accepted but never fires. Periodic catch-up uses an unbounded loop when many intervals
have elapsed.

**Safe usage:** Provide finite non-negative tick deltas, finite non-negative one-shot
times, finite positive periodic intervals, and non-null callbacks. Bound externally
supplied elapsed jumps.

**Evidence:**

- `Runtime/StateGraph.cs:197-242,326-429`
- `Runtime/StateMachine.cs:139-155,189-205`
- `Runtime/StateUnit.cs:272-307,637-685`

**Missing coverage:** Invalid values with zero graphs, all timer invalid inputs, and
large catch-up limits.

### SM-021 — Local propagation uses separate buses and separate dispatch IDs per hop

**Severity:** Medium  
**Status:** Current behavior; previous README description was misleading

Every host owns a separate `LocalEventBus`. Local propagation raises on the current
host first, then recursively on hosted graphs in list order. It is not a shared bus.
`StopPropagation` prevents traversal into remaining descendants and later siblings.
Because each bus raise completes before the next host raise begins, the event receives
a new top-level `RaiseUniqueId` at each host hop.

**Safe usage:** Treat local delivery as ordered downward forwarding. Do not compare one
`RaiseUniqueId` across different host levels or expect upward propagation.

**Evidence:**

- `Runtime/GraphHost.cs:13-23,98-112`
- `Tests/LocalEventPropagationTests.cs:30-71`
- EventBus dispatch in `../EventBusLib/Runtime/EventBus.cs:184-224`

**Missing coverage:** Raise ID observations across multiple local host levels.

### SM-022 — Local forwarding does not automatically cross active-state host boundaries

**Severity:** High  
**Status:** Current topology gap; missing hierarchical local-event tests

A graph owned by a `StateUnit` is stored in that state's private `GraphHost`; it is not
also present in the parent `StateGraph` host list. `StateMachine.LocalRaise` forwards
to its top-level graphs, and each graph forwards only to graphs attached directly to
that graph. It does not call `LocalRaise` on the graph's current `StateUnit`.

Consequently, a machine- or parent-graph-local event reaches listeners/transitions in
the parent graph but does not automatically reach a child graph hosted by the current
state. The owner must explicitly raise through that `StateUnit` to enter its hosted
graph scope. The same boundary repeats at every deeper state-owned level.

Event transitions subscribe to their source graph's local bus, not the source state's
own local bus. Calling `sourceState.LocalRaise` therefore does not trigger a transition
defined on that source state, although `sourceState.On<T>` listeners can receive it.

**Safe usage:** Choose the exact local host deliberately. Raise through a graph or an
ancestor that directly hosts that graph for transitions in that graph. Raise through
the owning `StateUnit` to notify its state-local listeners and graphs hosted by that
state. Use separate explicit raises for deeper state-owned scopes; do not assume one
machine-local raise traverses the active-state hierarchy.

**Evidence:**

- `Runtime/GraphHost.cs:13-23,98-112`
- `Runtime/StateGraph.cs:40-41,123-129`
- `Runtime/StateUnit.cs:64-76,188-238,317-416`
- `Runtime/Transition/TransitionByEvent.cs:46-230`
- Existing local propagation tests cover direct host-to-graph links only in
  `Tests/LocalEventPropagationTests.cs`

**Missing coverage:** Machine-to-state-owned-subgraph local delivery, explicit
`StateUnit.LocalRaise`, deeper state-owned hierarchies, and state-local event
transitions.

## Allocation and test-coverage limits

### SM-023 — The existing allocation test covers only a narrow manual tick path

**Severity:** Medium  
**Status:** Test-claim limitation

The allocation test warms and measures explicit Update and FixedUpdate ticks on a
small manually driven topology. It does not prove allocation freedom for managed
wrapper ticks, event subscriptions or raises, timers, transition setup, local bus
first use, attach/detach, teardown, or user callbacks.

Known allocation points include graph/state/transition/listener/timer construction,
collection growth, and the managed wrapper's cached-pair list growing on first use or
owner-count changes.

**Safe usage:** Complete all topology and callback construction during initialization,
prewarm the real execution path, and verify representative runtime paths in the Unity
Profiler. Do not generalize the existing unit test beyond its measured path.

**Evidence:**

- `Tests/FixedTimeAndLocalScaleTests.cs:287-307`
- `Runtime/StateMachineWrapper.cs:225-230`

### SM-024 — `HostedGraphs` allocates a wrapper on every property access

**Severity:** Medium  
**Status:** Open allocation defect

`GraphHost.HostedGraphs` calls `List<T>.AsReadOnly()` on every getter invocation,
creating a new `ReadOnlyCollection<T>` wrapper. Reading this property in an interactive
runtime path violates a zero-allocation policy even when the graph list is unchanged.

**Safe usage:** Do not poll `HostedGraphs` during gameplay. Cache required topology at
initialization or fix the library to cache the read-only view.

**Evidence:** `Runtime/GraphHost.cs:79`

**Missing coverage:** Allocation assertion for repeated `HostedGraphs` access.

## Documentation defects found in the audited revision

The audited README also described APIs or semantics that do not exist in source:

- `StateMachineMB`; the actual component is `StateMachineWrapper`.
- `GetSubStateGraph`, `SetSubStateGraph`, and `SetParentStateMachine`.
- `TransitionByAction` and `.On(ref Action)`/`.On(ref Action<T>)`.
- Cross-graph transition examples.
- A shared LocalEventBus across all subgraphs.
- Unqualified garbage-collection eligibility for detached subscribed graphs.

These documentation defects should be removed from the usage guide. They are listed
here so future documentation changes do not reintroduce them.
