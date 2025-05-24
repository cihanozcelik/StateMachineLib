# Nopnag StateMachineLib

A lightweight and flexible state machine library for C# and Unity, designed for managing game logic flow.

## Overview

StateMachineLib provides tools to structure application logic into distinct states and define transitions between them. It allows for multiple state graphs running concurrently and supports various ways to trigger transitions, including conditions, events (via `Nopnag.EventBusLib`), and direct calls.

## API Usage Quick Reference

```csharp
// --- Core Setup & Lifecycle ---
StateMachine stateMachine = new StateMachine();
StateGraph mainGraph = stateMachine.CreateGraph();
StateUnit state1 = mainGraph.CreateState(); // Preferred
// StateUnit namedState = mainGraph.CreateUnit("NamedState"); // Deprecated: If a name is needed, manage externally
mainGraph.InitialUnit = state1; // Or first created is initial by default
stateMachine.Start(); // Enters initial state of all graphs
stateMachine.UpdateMachine();       // Call in game loop (e.g., Unity's Update)
stateMachine.FixedUpdateMachine();  // Call in game loop (e.g., Unity's FixedUpdate)
stateMachine.LateUpdateMachine();   // Call in game loop (e.g., Unity's LateUpdate)
stateMachine.Exit(); // Exits all graphs and their current states

// --- Local Event System ---
// Each StateMachine has its own LocalEventBus for isolated event communication
stateMachine.LocalRaise(new MyGameEvent()); // Raise event only within this StateMachine
// Note: state.On<MyGameEvent>() and (state1 > state2).On<MyGameEvent>() automatically listen to BOTH global and local events

// --- StateUnit Logic Actions ---
state1.OnEnter = () => { /* Logic for StateOne Enter */ };
state1.OnUpdate = (timeInState) => { /* Logic for StateOne Update, timeInState is DeltaTimeSinceStart */ };
state1.OnExit = () => { /* Logic for StateOne Exit */ };
state1.OnFixedUpdate = (timeInState) => { /* Logic for StateOne FixedUpdate, timeInState is DeltaTimeSinceStart */ };
state1.OnLateUpdate = (timeInState) => { /* Logic for StateOne LateUpdate, timeInState is DeltaTimeSinceStart */ };
// state1.OnUpdateBeforeTransitionCheck = (timeInState) => { /* Optional: Logic before transitions, timeInState is DeltaTimeSinceStart */ };

// --- Time-Based Callbacks (within StateUnit) ---
state1.At(2.0f, () => { /* Action after 2 seconds in state1 */ });
state1.AtEvery(1.0f, () => { /* Action every 1 second in state1 */ });

// --- Event/Signal Listening (within StateUnit, while active) ---
// Assumes: using Nopnag.EventBusLib;
// public class MyGameEvent : BusEvent { public int Value; }
// public class MyParam : IParameter {}
state1.On<MyGameEvent>(evt => { /* Handle MyGameEvent from BOTH global and local EventBus */ });
state1.On<MyGameEvent>(EventBus<MyGameEvent>.Where<MyParam>("filterKey"), evt => { /* Handle filtered MyGameEvent */ });

// --- Subgraphs / Hierarchical States ---
StateUnit parentState = mainGraph.CreateState();
StateGraph subGraph = parentState.GetSubStateGraph(); // Creates and assigns a new subgraph with LocalEventBus support
// Alternative: Create elsewhere and attach
// StateGraph subGraph = new StateGraph(); 
// parentState.SetSubStateGraph(subGraph); // Automatically gets LocalEventBus support when attached
StateUnit childState1 = subGraph.CreateState();
subGraph.InitialUnit = childState1; // Set initial state for the subgraph

// --- Fluent Transition Creation API ---
// Assumes: StateUnit state1, state2, state3; MyGameEvent, MyParam etc. defined.
// 1. Transition by Condition or Time
(state1 > state2).When(elapsedTime => elapsedTime > 2.0f && SomeGlobalCondition());
(state2 < state1).When(elapsedTime => elapsedTime > 2.0f); // (target < source) is equivalent
(state1 > state2).After(3.5f); // After a specific duration

// 2. Transition by Event (listens to BOTH global and local events automatically)
(state1 > state2).On<MyGameEvent>();
(state1 > state2).On<MyGameEvent>(evt => evt.Value > 5); // With predicate
(state1 > state2).On(EventBus<MyGameEvent>.Where<MyParam>("filterKey")); // With query
(state1 > state2).On(EventBus<MyGameEvent>.Where<MyParam>("filterKey"), evt => evt.Value > 5); // Query + predicate

// 3. Immediate Transition
(state1 > state2).Immediately(); // Unconditional immediate transition

// 4. Transition to Indexed Target (from State to Array of States)
(state1 > new[] { state2, state3 }).When(
    elapsedTime => elapsedTime < 2.0f ? 0 : (elapsedTime < 5.0f ? 1 : -1) 
    ); // Ex: to state2 if <2s, to state3 if <5s (-1 means no change)

// 5. Transition to Dynamically Selected Target (from State to Dynamic Target Marker)
(state1 > StateGraph.DynamicTarget).When(
    elapsedTime => elapsedTime > 4.0f ? state2 : (elapsedTime > 1.0f ? state3 : null) 
    ); // Ex: to state2 if >4s, to state3 if >1s, else no change

// 6. Transitions from "Any State"
// These transitions can occur from any active state within the graph.
// They are evaluated with higher priority than regular state-to-state transitions.
// Assumes: StateUnit state3; MyGameEvent etc. defined as in previous examples.

// Define "Any State" transitions using the (StateGraph.Any > targetState) operator syntax:
(StateGraph.Any > state3).When(elapsedTime => SomeGlobalCondition());
(StateGraph.Any > state3).On<MyGameEvent>(evt => evt.Value > 10); // Listens to BOTH global and local events
// This syntax supports .When(), .After(), .On<Event>(), .Immediately() etc.,
// just like regular state-to-state transitions.
```

## Key Features

*   **Multiple Graphs:** Manage multiple independent state machines (`StateMachine` containing `StateGraph` instances) running in parallel.
*   **State Units (`StateUnit`):** Define individual states with `Enter`, `Exit`, `Update`, `LateUpdate`, and `FixedUpdate` logic.
*   **Rich Transition System:** Multiple ways to define transitions between states using a fluent API.
*   **Local Event System:** Each `StateMachine` has its own `LocalEventBus` for isolated event communication within that state machine instance.
*   **Dual Event Listening:** State event listeners and transitions automatically subscribe to both global and local events.
*   **Time Tracking:** `StateUnit` tracks the time elapsed since it became active (`DeltaTimeSinceStart`).
*   **Sub-States / Hierarchical States:** `StateUnit` can host its own `StateGraph` for complex, nested state logic.
*   **EventBus Integration:** Trigger transitions based on events from `Nopnag.EventBusLib`.
*   **Action Integration:** Trigger transitions via standard C# `Action` delegates.
*   **Conditional Logic:** Use predicates (`Func<>`) for complex transition conditions.
*   **Time-Based Callbacks:** Schedule actions to occur once after a specific delay (`At`) or repeatedly at set intervals (`AtEvery`) within a state.

## Main Concepts

*   **`StateMachine`**: The top-level container. It holds and manages one or more `StateGraph` instances, propagating `Update`, `LateUpdate`, and `FixedUpdate` calls to them.
    ```csharp
    // In your MonoBehaviour or main logic class
    StateMachine stateMachine = new StateMachine();
    ```

*   **`StateGraph`**: Represents a single state machine graph. It manages a collection of `StateUnit` instances, keeps track of the `CurrentUnit`, and handles entering/exiting the graph and starting specific states.
    ```csharp
    StateGraph mainGraph = stateMachine.CreateGraph();
    
    // Creating states:
    StateUnit unnamedState = mainGraph.CreateState(); // Preferred: Creates a state (name will be null by default).
    // StateUnit namedState = mainGraph.CreateUnit("MyNamedState"); // Deprecated: Creates a state with a specific name.
    ```

*   **`StateUnit`**: Represents a single state within a `StateGraph`. You assign behavior to it by setting its various `Action` properties (e.g., `OnEnter`, `OnUpdate`). The older `Action`-suffixed fields (e.g., `EnterStateFunction`) are now deprecated. It contains a list of potential `Transitions` which are checked during its `Update` loop.
    ```csharp
    // Using preferred CreateState():
    StateUnit idleState = mainGraph.CreateState(); 
    // If you need to identify the state for debugging/logging, you can manage that externally or rely on its object reference.
    // idleState.Name will be null if created with CreateState().

    // If using the deprecated CreateUnit(name) for legacy reasons or specific identification:
    // StateUnit namedIdleState = mainGraph.CreateUnit("Idle"); 
    // namedIdleState.Name will be "Idle".

    idleState.OnEnter = () => Debug.Log("Idling..."); // Preferred way to assign actions
    idleState.OnUpdate = (timeInState) => { /* Do idle stuff. timeInState is DeltaTimeSinceStart */ }; // Preferred
    
    // Older, deprecated way of assigning actions:
    // idleState.EnterStateFunction = () => Debug.Log("Idling..."); 
    // idleState.UpdateStateFunction = (timeInState) => { /* Do idle stuff. timeInState is DeltaTimeSinceStart */ };
    ```

*   **`IStateTransition`**: The interface for all transition types that are checked during the `Update` loop of a `StateUnit`. Defines the `CheckTransition` method. (Event/Action based transitions trigger more directly).

## Listening to Events & Signals Only While a State is Active

A powerful feature of StateMachineLib is the ability to listen to `EventBusLib` events only while a specific state is active. This is achieved using the `StateUnit.On` methods. The older `Listen` and `ListenSignal` methods are now deprecated.

### EventBus Event Listening (Basic Usage)
```csharp
// Listen to all MyEvent events, but only while this state is active
// myState.Listen<MyEvent>(...); // Deprecated
myState.On<MyEvent>(evt => { // Preferred
    Debug.Log($"MyEvent received in state: {myState.Name}");
});
```

### EventBus Event Listening (Parameter-Filtered with EventQuery)
```csharp
// Listen to MyEvent events with a specific parameter, only while this state is active
var query = EventBus<MyEvent>.Where<MyParam>(myValue);
// myState.Listen(query, ...); // Deprecated
myState.On(query, evt => { // Preferred
    Debug.Log($"MyEvent with param received in state: {myState.Name}");
});
```

**Why is this important?**
- The callback is only invoked if the state is currently active, so you don't need to manually unsubscribe or check state inside the handler.
- With the `EventQuery` overload for EventBus events, you can listen to only a subset of events (e.g., only those with a certain parameter value), making your state logic more precise and efficient.

## Advanced Features

### Parallel State Graphs

The `StateMachine` class can manage multiple `StateGraph` instances simultaneously. Each graph runs independently but is updated by the main `StateMachine` update calls (`UpdateMachine`, `FixedUpdateMachine`, etc.). This is useful for managing distinct aspects of an object or system concurrently.

```csharp
// In your setup
StateMachine characterStateMachine = new StateMachine();

// Graph for movement logic
StateGraph movementGraph = characterStateMachine.CreateGraph(); 
// ... define movement states and transitions ...

// Graph for combat logic
StateGraph combatGraph = characterStateMachine.CreateGraph();
// ... define combat states and transitions ...

// Start both graphs
characterStateMachine.Start();

// In Update loop, both graphs will be updated
void Update()
{
    characterStateMachine.UpdateMachine(); 
}
```

### Subgraphs / Hierarchical State Machines

A `StateUnit` can contain its own nested `StateGraph`, allowing for more complex and organized state logic. When the parent `StateUnit` is active, its subgraph is also active and updated.

```csharp
// In your setup
StateGraph mainGraph = stateMachine.CreateGraph();
StateUnit combatState = mainGraph.CreateState();
StateUnit patrollingState = mainGraph.CreateState();

// Method 1: Create subgraph directly (Recommended for simple cases)
StateGraph combatSubgraph = combatState.GetSubStateGraph(); // Automatically gets LocalEventBus support

// Method 2: Create subgraph elsewhere and attach (Flexible approach)
StateGraph detailedCombatGraph = new StateGraph(); // Created independently
StateUnit aimingState = detailedCombatGraph.CreateState();
StateUnit shootingState = detailedCombatGraph.CreateState();
// ... define transitions within detailedCombatGraph ...
combatState.SetSubStateGraph(detailedCombatGraph); // Automatically gets LocalEventBus support when attached

// Method 3: Manual StateMachine reference (Advanced usage)
StateGraph manualGraph = new StateGraph();
manualGraph.SetParentStateMachine(stateMachine); // Explicit LocalEventBus support
// ... setup states and transitions ...

// Define transition into the combat state using the fluent API
// (patrollingState > combatState).On<EnemyDetectedEvent>(); // Example using Fluent API

// ... other setup ...

stateMachine.Start();

// When patrollingState transitions to combatState:
// 1. combatState.OnEnter runs.
// 2. combatSubgraph.EnterGraph() runs, starting its initial state (e.g., aimingState).
// 3. While combatState is active, combatSubgraph is updated via combatState's Update/FixedUpdate/LateUpdate.
// 4. All subgraphs have access to the same LocalEventBus as the parent StateMachine.
```

### Time-Based Callbacks within States

`StateUnit` provides convenient methods to schedule actions based on the time elapsed since the state became active (`DeltaTimeSinceStart`). These callbacks are automatically managed and reset if the state is re-entered.

#### `At(float targetTime, Action callback)`

Schedules an `Action` to be invoked once when `DeltaTimeSinceStart` reaches or exceeds `targetTime`. If the state is re-entered, the timer is reset, and the action can be invoked again after the specified `targetTime`.

**Usage:**

```csharp
StateUnit preparingState = mainGraph.CreateState();

preparingState.OnEnter = () => Debug.Log("Preparing action..."); // Preferred

// After 3 seconds in PreparingState, log a message
preparingState.At(3.0f, () => {
    Debug.Log("Preparation complete after 3 seconds!");
});
```

#### `AtEvery(float intervalTime, Action callback)`

Schedules an `Action` to be invoked repeatedly at specified `intervalTime` periods while the state is active. The first invocation occurs when `DeltaTimeSinceStart` reaches or exceeds the first `intervalTime`. If the state is re-entered, the interval timing is reset.

**Usage:**

```csharp
StateUnit activeState = mainGraph.CreateState();

activeState.OnEnter = () => Debug.Log("ActiveState started. Monitoring..."); // Preferred

// Every 5 seconds, print a monitoring message
activeState.AtEvery(5.0f, () => {
    Debug.Log($"Monitoring... (Time in state: {activeState.DeltaTimeSinceStart}s)");
});
```
Both `At` and `AtEvery` ensure that the callback is only invoked if the state is currently active. The provided `Action` delegate should be parameterless. If you need to access state-specific data like `DeltaTimeSinceStart` or the `StateUnit` instance itself within the callback, you can do so via a lambda closure:

```csharp
myState.At(2.5f, () => {
    Debug.Log($"Action at {myState.DeltaTimeSinceStart} seconds in state {myState.Name}");
});
```

## Simplified MonoBehaviour Integration (Recommended)

For the easiest and most robust way to use StateMachineLib with Unity's `MonoBehaviour` lifecycle, use the `CreateManagedStateMachine()` extension method. This method handles all the necessary setup for automatic updates and lifecycle management, tied directly to your `MonoBehaviour`.

**How to Use:**
Simply call `CreateManagedStateMachine()` from your `MonoBehaviour` (typically in `Awake()`) to get a fully managed `StateMachine` instance.

**Key Benefits:**
*   **Automatic Lifecycle Management:** 
    *   The `StateMachine` starts automatically when the `MonoBehaviour` is active.
    *   It processes `Update`, `FixedUpdate`, and `LateUpdate` calls automatically.
    *   It pauses if the `MonoBehaviour` is disabled.
    *   It correctly calls `Exit()` on the `StateMachine` when the `MonoBehaviour` is destroyed, ensuring proper cleanup.
*   **Simplified Code:** Eliminates the need to manually call `Start()`, `UpdateMachine()`, or `Exit()` from your `MonoBehaviour`.
*   **Focus on Logic:** Allows you to focus on defining your states and transitions rather than lifecycle boilerplate.

**Usage Example:**

```csharp
using UnityEngine;
using Nopnag.StateMachineLib; // Required for StateMachine and the CreateManagedStateMachine() extension method

public class EnemyAI : MonoBehaviour
{
    // Optional: Store as a class field if other methods need to access the StateMachine.
    // private StateMachine aiStateMachine;

    void Awake()
    {
        // 1. Create a lifecycle-managed StateMachine instance.
        StateMachine aiStateMachine = CreateManagedStateMachine(); // Called without 'this.'
        
        // If storing as a class field:
        // this.aiStateMachine = CreateManagedStateMachine();

        // 2. Configure the StateMachine as usual.
        StateGraph brain = aiStateMachine.CreateGraph();
        
        StateUnit patrolState = brain.CreateState();
        patrolState.OnEnter = () => Debug.Log("Enemy: Starting patrol.");
        // ... add patrol logic and transitions ...
        
        StateUnit chaseState = brain.CreateState();
        chaseState.OnEnter = () => Debug.Log("Enemy: Chasing player!");
        // ... add chase logic and transitions ...

        brain.InitialUnit = patrolState;
        
        // All lifecycle calls (Start, Update, Exit, etc.) are handled automatically.
    }
}
```
This extension method provides the most straightforward and recommended way to use `StateMachineLib` within Unity projects.

(Internally, `CreateManagedStateMachine()` utilizes a helper component that links to a global event manager to achieve this. This underlying mechanism ensures the described automatic behaviors, but you typically don't need to interact with these components directly when using the extension method.)

## Fluent Transition API

A fluent syntax is available for defining transitions directly from `StateUnit` instances. This API uses operator overloading (`>` and `<`) and chained method calls.

This fluent approach is designed with structs to minimize garbage generation during transition setup.

**Initiating a Transition:**

You can start defining a transition using the `>` operator between a source and a target state, or the `<` operator between a target and a source state. Both achieve the same result of setting up a transition from the source to the target.

```csharp
// These are equivalent ways to start defining a transition from stateA to stateB:
var transitionAB = (stateA > stateB); 
var transitionAlsoAB = (stateB < stateA); // (target < source) also configures source -> target
```

This returns a configurator struct. You then chain one of the following methods to define the transition logic:

### `(fromState > toState).When(predicate)`:

This defines a `BasicTransition` that triggers when the provided predicate returns `true`. The predicate receives the elapsed time in the source state.

```csharp
StateUnit stateA = myGraph.CreateState();
StateUnit stateB = myGraph.CreateState();

// Transition from stateA to stateB when health is low after 1 second
(stateA > stateB).When(elapsedTime => player.Health < 10 && elapsedTime > 1.0f);

// Equivalent using the < operator
(stateB < stateA).When(elapsedTime => player.Health < 10 && elapsedTime > 1.0f);
```

### `(fromState > toState).After(duration)`:

This defines a `BasicTransition` that triggers after a specific `duration` (in seconds) has passed since the source state was entered.

```csharp
StateUnit loadingState = myGraph.CreateState();
StateUnit readyState = myGraph.CreateState();

// Transition from loadingState to readyState after 2.5 seconds
(loadingState > readyState).After(2.5f);
```

### `(fromState > toState).On<TEvent>(...) (for EventBus Events)`:

This defines a `TransitionByEvent`. It has several overloads:

*   **`On<TEvent>()`**: Triggers when any event of `TEvent` is raised.
    ```csharp
    (stateA > stateB).On<PlayerDiedEvent>();
    ```
*   **`On<TEvent>(Func<TEvent, bool> predicate)`**: Triggers if `TEvent` is raised AND the predicate returns `true`.
    ```csharp
    (stateA > stateB).On<EnemySpottedEvent>(evt => evt.IsHighPriority);
    ```
*   **`On<TEvent>(EventQuery<TEvent> query, Func<TEvent, bool> predicate = null)`**: Triggers if `TEvent` matching the `query` is raised. If a `predicate` is also provided, it must also return `true`.
    ```csharp
    // Define a marker IParameter type for your specific query.
    public class ItemTag : IParameter { }

    // Event publishing (example of how the event would be set up elsewhere):
    // var collectedEvent = new ItemCollectedEvent();
    // collectedEvent.Set<ItemTag>("KeyCard"); // Set the string value with ItemTag as type key
    // EventBus.Raise(collectedEvent);

    // Fluent transition setup:
    (stateA > stateB).On(
        EventBus<ItemCollectedEvent>.Where<ItemTag>("KeyCard"), // Filter by the string value "KeyCard"
        evt => evt.Collector.IsPlayer // Optional additional predicate on the event object
    );
    ```

### `(fromState > toState).On(ref signal) (for C# Actions)`:

This defines a `TransitionByAction` that triggers when the provided C# `Action` or `Action<T>` delegate (signal) is invoked. The signal must be passed with the `ref` keyword. (The heading shows the parameterless version; `Action<TActionParam>` is also supported).

```csharp
public Action PlayerJumped;
public Action<int> PlayerScoredPoints;

// ... in setup ...
(groundedState > jumpingState).On(ref PlayerJumped);
(anyState > scoreCelebrationState).On(ref PlayerScoredPoints);

// ... elsewhere ...
PlayerJumped?.Invoke();
PlayerScoredPoints?.Invoke(100);
```

### `(fromState > toState).Immediately()`:

This defines a `DirectTransition` that occurs unconditionally as soon as the source state is entered or updated, causing an immediate transition to the target state. It's useful for states that are purely transitional or serve as entry points that should immediately redirect.

```csharp
StateUnit entryPointState = myGraph.CreateState();
StateUnit actualStartState = myGraph.CreateState();

// From entryPointState, immediately go to actualStartState
(entryPointState > actualStartState).Immediately();
```

### `(fromState > targetStates).When(indexPredicate)`:

You can define transitions from a single state to one of several possible target states based on an index returned by a condition function. This is useful for decision points where the next state depends on dynamic criteria.

The `When` method, when used with an array of target `StateUnit`s, expects its predicate to return an integer.
- If the integer is a valid index into the array of target states (0 to N-1), a transition to the state at that index occurs.
- If the integer is -1 (or any out-of-bounds negative number), no transition occurs.

```csharp
StateUnit decisionState = myGraph.CreateState();
StateUnit optionAState = myGraph.CreateState();
StateUnit optionBState = myGraph.CreateState();
StateUnit optionCState = myGraph.CreateState();

// From decisionState, transition to one of the new[] { optionAState, ... } based on index
(decisionState > new[] { optionAState, optionBState, optionCState }).When(elapsedTime => {
    // Assuming 'player' and 'PlayerChoices' are defined elsewhere
    // and 'elapsedTime' is the time since 'decisionState' became active.
    if (player.Choice == PlayerChoices.A) return 0;       // Transition to optionAState
    if (player.Choice == PlayerChoices.B) return 1;       // Transition to optionBState
    if (elapsedTime > 10.0f && player.IsIdle) return 2; // Transition to optionCState
    return -1;                                          // No transition
});
```

### `(fromState > StateGraph.DynamicTarget).When(dynamicTargetPredicate)`:

This defines a `ConditionalTransition` where the target state is determined at runtime by the `dynamicTargetPredicate`. 

You initiate this by transitioning from a state to the special `StateGraph.DynamicTarget` marker. The subsequent `.When()` method then takes a predicate of type `Func<float, StateUnit>`.

-   **`dynamicTargetPredicate`**: A function that receives the elapsed time in the source state and should return:
    -   A non-null `StateUnit` to transition to that state.
    -   `null` to indicate that no transition should occur at this time.

```csharp
StateUnit patrollingState = myGraph.CreateState();
StateUnit chasingState = myGraph.CreateState();
StateUnit investigatingState = myGraph.CreateState();

// From patrollingState, transition to a dynamically chosen state
(patrollingState > StateGraph.DynamicTarget).When(elapsedTime => {
    if (CanSeePlayer()) return chasingState;
    if (HeardNoise()) return investigatingState;
    return null; // Stay in patrolling state
});
```

Future methods (like for conditional transitions to a dynamically chosen single state) will be added to this fluent API.


## Practical Usage Example (Character Controller)

This example demonstrates a character controller with Idle, Moving, Jumping, and Stunned states, using various transitions and the recommended `CreateManagedStateMachine()` for easy integration.

```csharp
using Nopnag.StateMachineLib;
using Nopnag.EventBusLib; // For MyEvent, DamageTakenEvent etc.
using UnityEngine;
using System;

// --- Define Events used for Transitions (if not already globally defined) ---
// public class DamageTakenEvent : BusEvent { } 
// public class JumpInputEvent : BusEvent { } // Example if using event for jump

public class CharacterController : MonoBehaviour
{
    // Note: stateMachine, movementGraph, and individual states (idleState, etc.) 
    // are declared as local variables within Awake() in this example because all setup
    // and usage occurs there. If other methods in CharacterController needed to 
    // access them (e.g., to trigger events, query states), they would be declared 
    // as class fields instead.
    // Example of class field declaration if needed elsewhere:
    // private StateMachine stateMachine;

    private Rigidbody rb;
    private float jumpForce = 5f;
    private float _stunDuration = 0.5f; 

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        // 1. Create a lifecycle-managed StateMachine instance.
        StateMachine stateMachine = CreateManagedStateMachine(); // Ensure no 'this.'

        // --- Initialize States and Transitions --- 
        StateGraph movementGraph = stateMachine.CreateGraph();

        // Define States
        StateUnit idleState = movementGraph.CreateState();
        StateUnit movingState = movementGraph.CreateState();
        StateUnit jumpingState = movementGraph.CreateState();
        StateUnit stunnedState = movementGraph.CreateState();

        // Assign State Logic
        idleState.OnEnter = () => { 
            Debug.Log("Entering Idle State"); 
        };
        idleState.OnUpdate = (timeInState) => { /* Maybe play idle animation. */ };
        
        movingState.OnEnter = () => Debug.Log("Entering Moving State");
        movingState.OnUpdate = (timeInState) => 
        { 
            Vector3 moveDir = GetMovementInput(); 
            rb.AddForce(moveDir * 10f * Time.deltaTime);
        };

        jumpingState.OnEnter = () => 
        {
            Debug.Log("Entering Jumping State");
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        };
        
        stunnedState.OnEnter = () => Debug.Log("Entering Stunned State");
        stunnedState.OnUpdate = (timeInState) => { /* Maybe play stunned animation. */ };

        // Define Transitions (using Fluent API)
        (idleState > stunnedState).On<DamageTakenEvent>();
        (movingState > stunnedState).On<DamageTakenEvent>();
        (jumpingState > stunnedState).On<DamageTakenEvent>();
        (stunnedState > idleState).After(_stunDuration);
        (idleState > movingState).When(elapsedTime => GetMovementInput().magnitude > 0.1f);
        (movingState > idleState).When(elapsedTime => GetMovementInput().magnitude <= 0.1f);
        (idleState > jumpingState).When(elapsedTime => Input.GetButtonDown("Jump"));
        (movingState > jumpingState).When(elapsedTime => Input.GetButtonDown("Jump"));
        (jumpingState > idleState).After(1.0f); 

        // Set Initial State
        movementGraph.InitialUnit = idleState;
        
        // Lifecycle (Start, Update, Exit) is handled automatically by CreateManagedStateMachine().
    }

    Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        return new Vector3(horizontal, 0, vertical).normalized;
    }
}
```

## Installation

**Important:** This package depends on `Nopnag.EventBusLib`. You need to install both packages for StateMachineLib to work correctly.

You can install these packages using the Unity Package Manager:

1.  Open the Package Manager (`Window` > `Package Manager`).
2.  Click the `+` button in the top-left corner and select `Add package from git URL...`.
3.  Enter the repository URL for EventBusLib: `https://github.com/cihanozcelik/EventBusLib.git` and click `Add`.
4.  Repeat step 2.
5.  Enter the repository URL for StateMachineLib: `https://github.com/cihanozcelik/StateMachineLib.git` and click `Add`.


Alternatively, you can add both directly to your `Packages/manifest.json` file:
```json
{
  "dependencies": {
    "com.nopnag.eventbuslib": "https://github.com/cihanozcelik/EventBusLib.git", 
    "com.nopnag.statemachinelib": "https://github.com/cihanozcelik/StateMachineLib.git",
    // ... other dependencies
  }
}
```