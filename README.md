# Nopnag StateMachineLib

A lightweight and flexible state machine library for C# and Unity, designed for managing game logic flow.

## Overview

StateMachineLib provides tools to structure application logic into distinct states and define transitions between them. It allows for multiple state graphs running concurrently and supports various ways to trigger transitions, including conditions, events (via `Nopnag.EventBusLib`), and direct calls.

## Key Features

*   **Multiple Graphs:** Manage multiple independent state machines (`StateMachine` containing `StateGraph` instances) running in parallel.
*   **State Units (`StateUnit`):** Define individual states with `Enter`, `Exit`, `Update`, `LateUpdate`, and `FixedUpdate` logic.
*   **Rich Transition System:** Multiple ways to define transitions between states (`IStateTransition` implementations).
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
    idleState.OnUpdate = (deltaTime) => { /* Do idle stuff */ }; // Preferred
    
    // Older, deprecated way of assigning actions:
    // idleState.EnterStateFunction = () => Debug.Log("Idling..."); 
    // idleState.UpdateStateFunction = (deltaTime) => { /* Do idle stuff */ };
    ```

*   **`IStateTransition`**: The interface for all transition types. Defines the `CheckTransition` method, which determines if a transition should occur and outputs the target `StateUnit`.

## Listening to Events & Signals Only While a State is Active

A powerful feature of StateMachineLib is the ability to listen to `EventBusLib` events or standard C# `Action` signals only while a specific state is active. This is achieved using the `StateUnit.On` methods. The older `Listen` and `ListenSignal` methods are now deprecated.

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
    Debug.Log($"MyEvent with param received in state: {myState.Name}\");
});
```

### C# Action Signal Listening
You can also listen to a standard C# `Action<T>` signal. The provided sensor action will only execute if the state is active when the signal is invoked.
```csharp
public Action<float> PlayerScoreChanged;

// In state setup:
// myState.ListenSignal(ref PlayerScoreChanged, ...); // Deprecated
myState.On(ref PlayerScoreChanged, score => { // Preferred
    Debug.Log($"Player score changed to {score} while state {myState.Name} is active.");
    // Perform actions specific to this state based on the score change
});

// Elsewhere in your code, when the score changes:
PlayerScoreChanged?.Invoke(newScore);
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

// Create a subgraph for detailed combat logic
StateGraph combatSubgraph = new StateGraph(); 
StateUnit aimingState = combatSubgraph.CreateState();
StateUnit shootingState = combatSubgraph.CreateState();
// ... define transitions within combatSubgraph ...

// Assign the subgraph to the parent state
combatState.SetSubStateGraph(combatSubgraph);
// Or: combatState.GetSubStateGraph() returns a new graph and assigns it

// Define transition into the combat state
TransitionByEvent.Connect<EnemyDetectedEvent>(patrollingState, combatState);

// ... other setup ...

stateMachine.Start();

// When patrollingState transitions to combatState:
// 1. combatState.OnEnter runs (previously EnterStateFunction).
// 2. combatSubgraph.EnterGraph() runs, starting its initial state (e.g., aimingState).
// 3. While combatState is active, combatSubgraph is updated via combatState's Update/FixedUpdate/LateUpdate.
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

## Transition Types

Transitions define how the state machine moves from one `StateUnit` to another. `TransitionByAction` and `TransitionByEvent` directly trigger state changes, while the others are evaluated during the source state's `Update` loop.

*   **`BasicTransition`**: 
    *   **How:** Connects two states based on a predicate evaluating to true/false.
    *   **Trigger:** `predicate(elapsedTimeInState)` returns `true`.
    *   **Usage:** 
        ```csharp
        // Transition from 'charging' to 'ready' after 2 seconds
        BasicTransition.Connect(chargingState, readyState, 
            elapsedTime => elapsedTime > 2.0f);
        ```

*   **`ConditionalTransition`**:
    *   **How:** Connects a state to a dynamically chosen target state based on a predicate.
    *   **Trigger:** `predicate(elapsedTimeInState)` returns a non-null `StateUnit`.
    *   **Usage:** 
        ```csharp
        // Transition from 'patrolling' to 'chasing' or 'searching'
        ConditionalTransition.Connect(patrollingState, 
            elapsedTime => {
                if (CanSeePlayer()) return chasingState;
                if (HeardNoise()) return searchingState;
                return null; // Stay in patrolling state
            });
        ```

*   **`ConditionalTransitionByIndex`**:
    *   **How:** Connects a state to one of several target states based on an index returned by a predicate.
    *   **Trigger:** `predicate(elapsedTimeInState)` returns a valid, non-negative index into the `targetStateInfos` array.
    *   **Usage:**
        ```csharp
        // Transition from 'aiming' based on weapon type index
        StateUnit[] fireStates = { firePistolState, fireRifleState };
        ConditionalTransitionByIndex.Connect(aimingState, fireStates,
            elapsedTime => GetEquippedWeaponIndex()); // Returns 0 or 1, or -1
        ```

*   **`DirectTransition`**:
    *   **How:** Connects two states unconditionally.
    *   **Trigger:** Always triggers if checked. Use carefully, often as the last transition checked in a state.
    *   **Usage:** 
        ```csharp
        // Immediately go from 'initializing' to 'idle'
        DirectTransition.Connect(initializingState, idleState);
        ```

*   **`TransitionByAction`**:
    *   **How:** Connects two states; transition occurs when a specific `Action` is invoked.
    *   **Trigger:** The `ref Action signal` is invoked externally.
    *   **Important:** Bypasses the regular check flow. Triggers *only if* the `baseUnit` is currently active.
    *   **Usage:** 
        ```csharp
        // In setup:
        public Action PlayerJumped;
        TransitionByAction.Connect(groundedState, jumpingState, ref PlayerJumped);
        
        // Elsewhere (e.g., Input Handling):
        if (Input.GetButtonDown("Jump")) PlayerJumped?.Invoke();
        ```

*   **`TransitionByEvent`**:
    *   **How:** Connects two states; transition occurs when a specific `EventBusLib` event is raised.
    *   **Trigger:** An event of type `T` is raised via `EventBus<T>.Raise(...)`.
    *   **Optional Predicate:** Can filter events further with `Func<T, bool>`.
    *   **Important:** Bypasses the regular check flow. Triggers *only if* the `baseUnit` is active (and predicate passes).
    *   **Usage:**
        ```csharp
        // Simple event trigger
        TransitionByEvent.Connect<PlayerDiedEvent>(aliveState, gameOverState);

        // Conditional event trigger
        TransitionByEvent.Connect<EnemySpottedEvent>(patrollingState, alertState, 
            evt => evt.EnemyType == Enemy.EliteGuard);
        ```

## Practical Usage Example (Character Controller)

This example demonstrates a character controller with Idle, Moving, Jumping, and Stunned states, using various transitions.

```csharp
using Nopnag.StateMachineLib;
using Nopnag.StateMachineLib.Transition;
using Nopnag.EventBusLib; // For JumpInputEvent and DamageTakenEvent
using UnityEngine;
using System;

// --- Define Events used for Transitions ---
public class DamageTakenEvent : BusEvent 
{
}

public class CharacterController : MonoBehaviour
{
    private StateMachine stateMachine;
    private StateGraph movementGraph;
    private StateUnit idleState, movingState, jumpingState, stunnedState; 

    private Rigidbody rb;
    private float jumpForce = 5f;
    private float _stunDuration = 0.5f; // How long stun lasts

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>(); // Ensure Rigidbody
    }

    void Start()
    {
        stateMachine = new StateMachine();
        movementGraph = stateMachine.CreateGraph();

        // --- 1. Define States ---
        idleState = movementGraph.CreateState();
        movingState = movementGraph.CreateState();
        jumpingState = movementGraph.CreateState();
        stunnedState = movementGraph.CreateState(); 

        // --- 2. Assign State Logic ---
        idleState.OnEnter = () => { 
            Debug.Log("Entering Idle State"); 
        };
        idleState.OnUpdate = (dt) => { /* Maybe play idle animation */ };
        
        movingState.OnEnter = () => Debug.Log("Entering Moving State");
        movingState.OnUpdate = (dt) => 
        { 
            // Apply movement force based on input (simplified)
            Vector3 moveDir = GetMovementInput(); 
            rb.AddForce(moveDir * 10f);
        };

        jumpingState.OnEnter = () => 
        {
            Debug.Log("Entering Jumping State");
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        };
        
        stunnedState.OnEnter = () => Debug.Log("Entering Stunned State");
        stunnedState.OnUpdate = (dt) => { /* Maybe play stunned animation */ };

        // --- 3. Define Transitions --- 

        // --- Transitions TO Stunned (Triggered directly by DamageTakenEvent) ---
        TransitionByEvent.Connect<DamageTakenEvent>(idleState, stunnedState);
        TransitionByEvent.Connect<DamageTakenEvent>(movingState, stunnedState);
        TransitionByEvent.Connect<DamageTakenEvent>(jumpingState, stunnedState);

        // --- Transition FROM Stunned ---
        // Go back to Idle after the stun duration
        BasicTransition.Connect(stunnedState, idleState, 
            elapsedTime => elapsedTime > _stunDuration);

        // --- Normal Movement Transitions ---
        // Idle -> Moving
        BasicTransition.Connect(idleState, movingState,
            elapsedTime => GetMovementInput().magnitude > 0.1f);

        // Moving -> Idle
        BasicTransition.Connect(movingState, idleState,
            elapsedTime => GetMovementInput().magnitude <= 0.1f);

        // Idle -> Jumping (Using BasicTransition with direct input check)
        // TransitionByEvent.Connect<JumpInputEvent>(idleState, jumpingState);
        BasicTransition.Connect(idleState, jumpingState,
            elapsedTime => Input.GetButtonDown("Jump"));

        // Moving -> Jumping (Using BasicTransition with direct input check)
        // TransitionByEvent.Connect<JumpInputEvent>(movingState, jumpingState);
        BasicTransition.Connect(movingState, jumpingState,
            elapsedTime => Input.GetButtonDown("Jump"));

        BasicTransition.Connect(jumpingState, idleState, 
            elapsedTime => elapsedTime > 1.0f); 

        // --- 4. Start the State Machine ---
        stateMachine.Start();

    }

    void Update()
    {
        stateMachine.UpdateMachine();
    }

    void OnDestroy()
    {
        stateMachine?.Exit(); // Clean up graph states
    }

    Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        return new Vector3(horizontal, 0, vertical).normalized;
    }
}

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