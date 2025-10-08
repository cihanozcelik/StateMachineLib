using System;
using System.Collections.Generic;
using Nopnag.StateMachineLib;
using UnityEngine;

[DisallowMultipleComponent]
public class StateMachineWrapper : MonoBehaviour
{
  // Track if OnEnable was called (to differentiate first creation vs re-enable)
  bool _hasBeenDisabled = false;
  // Dictionary: MonoBehaviour -> StateMachine mapping
  Dictionary<MonoBehaviour, StateMachine> _managedStateMachines = new();
  // Track which StateMachines haven't been Started yet (created while owner was disabled)
  HashSet<StateMachine> _pendingStartStateMachines = new();
  // Cached list to avoid allocations during iteration
  List<KeyValuePair<MonoBehaviour, StateMachine>> _cachedPairs = new();
  
  // Cached actions to avoid lambda allocations every frame
  Action<StateMachine> _updateAction;
  Action<StateMachine> _fixedUpdateAction;
  Action<StateMachine> _lateUpdateAction;

  /// <summary>
  /// Creates and registers a new StateMachine for the given MonoBehaviour owner.
  /// The setup callback is invoked immediately, and then the StateMachine is started automatically.
  /// </summary>
  public StateMachine CreateStateMachineFor(MonoBehaviour owner, Action<StateMachine> setupCallback)
  {
    if (owner == null)
      throw new ArgumentNullException(nameof(owner));
    
    if (setupCallback == null)
      throw new ArgumentNullException(nameof(setupCallback));

    if (_managedStateMachines.ContainsKey(owner))
    {
      Debug.LogWarning(
        $"StateMachine already exists for {owner.GetType().Name} on {owner.gameObject.name}. Returning existing instance.",
        owner);
      return _managedStateMachines[owner];
    }

    var sm = new StateMachine();
    _managedStateMachines[owner] = sm;
    
    // Allow user to set up the StateMachine
    setupCallback(sm);
    
    // Only Start the StateMachine if the owner MonoBehaviour is currently enabled
    // If disabled, Start will be deferred until OnEnable
    if (owner.enabled && owner.gameObject.activeInHierarchy)
    {
      sm.Start();
    }
    else
    {
      // Mark as pending start (will be started when owner becomes enabled)
      _pendingStartStateMachines.Add(sm);
      sm.SetTurnedOn(false);
    }
    
    return sm;
  }

  void Awake()
  {
    // Initialize cached actions once to avoid lambda allocations every frame
    _updateAction = sm => sm.UpdateMachine();
    _fixedUpdateAction = sm => sm.FixedUpdateMachine();
    _lateUpdateAction = sm => sm.LateUpdateMachine();
  }

  /// <summary>
  /// Gets or creates the StateMachineWrapper component on the given GameObject.
  /// </summary>
  public static StateMachineWrapper GetOrCreate(GameObject gameObject)
  {
    if (gameObject == null)
      throw new ArgumentNullException(nameof(gameObject));

    var wrapper                  = gameObject.GetComponent<StateMachineWrapper>();
    if (wrapper == null) wrapper = gameObject.AddComponent<StateMachineWrapper>();
    return wrapper;
  }

  /// <summary>
  /// Manually removes a StateMachine for a given owner.
  /// Calls Exit and Dispose on the StateMachine.
  /// </summary>
  public void RemoveStateMachineFor(MonoBehaviour owner)
  {
    if (owner == null) return;

    if (_managedStateMachines.TryGetValue(owner, out var sm))
    {
      try
      {
        sm?.Exit();
        sm?.Dispose();
      }
      catch (Exception ex)
      {
        Debug.LogError($"Exception while removing StateMachine for {owner.GetType().Name}: {ex}",
          owner);
      }

      _managedStateMachines.Remove(owner);
      _pendingStartStateMachines.Remove(sm); // Clean up pending start tracking
    }
  }

  void OnEnable()
  {
    // Start any StateMachines that were created while their owner was disabled
    if (_pendingStartStateMachines.Count > 0)
    {
      foreach (var sm in _pendingStartStateMachines)
      {
        sm.Start();
      }
      _pendingStartStateMachines.Clear();
    }
    
    // Only restore power if this is a re-enable (not first creation)
    // This prevents interfering with StateMachine initialization
    if (_hasBeenDisabled)
      // Turn on power for all managed state machines
      UpdateAllStateMachinesPower(true);
  }

  void OnDisable()
  {
    // Mark that we've been disabled (so next OnEnable should restore power)
    _hasBeenDisabled = true;

    // Turn off power for all managed state machines (pause without Exit)
    UpdateAllStateMachinesPower(false);
  }

  void OnDestroy()
  {
    // GameObject is being destroyed
    // First, call Exit on all state machines while GameObjects are still alive
    foreach (var kvp in _managedStateMachines)
    {
      var owner = kvp.Key;
      var sm    = kvp.Value;

      // Only call Exit if owner still exists
      // (might have been destroyed before this wrapper's OnDestroy)
      if (owner != null && owner)
        try
        {
          sm?.Exit();
        }
        catch (Exception ex)
        {
          Debug.LogError($"Exception in StateMachine.Exit() for {owner?.GetType().Name}: {ex}",
            this);
        }
    }

    // Then dispose all state machines
    foreach (var kvp in _managedStateMachines)
    {
      var sm = kvp.Value;
      try
      {
        sm?.Dispose();
      }
      catch (Exception ex)
      {
        Debug.LogError($"Exception in StateMachine.Dispose(): {ex}", this);
      }
    }

    _managedStateMachines.Clear();
  }

  void Update()
  {
    UpdateAllStateMachines(_updateAction);
  }

  void FixedUpdate()
  {
    UpdateAllStateMachines(_fixedUpdateAction);
  }

  void LateUpdate()
  {
    UpdateAllStateMachines(_lateUpdateAction);
  }

#if UNITY_EDITOR
  // Debug info in Inspector
  void OnValidate()
  {
    // Show count of managed state machines in inspector
    if (_managedStateMachines != null)
      gameObject.name =
        $"{gameObject.name.Split('[')[0].Trim()} [{_managedStateMachines.Count} SMs]";
  }
#endif

  void CleanupStateMachine(MonoBehaviour owner, StateMachine sm)
  {
    // Owner MonoBehaviour was destroyed
    // We're in an Update call, so child objects might already be destroyed
    // Skip Exit to avoid exceptions, go straight to Dispose

    try
    {
      sm?.Dispose();
    }
    catch (Exception ex)
    {
      Debug.LogError($"Exception during StateMachine cleanup: {ex}", this);
    }

    if (owner != null) // Might be null if destroyed
      _managedStateMachines.Remove(owner);
  }

  void UpdateAllStateMachines(Action<StateMachine> updateAction)
  {
    // Iterate through all managed state machines
    // Use cached list to avoid allocations every frame
    _cachedPairs.Clear();
    _cachedPairs.AddRange(_managedStateMachines);

    foreach (var kvp in _cachedPairs)
    {
      var owner = kvp.Key;
      var sm    = kvp.Value;

      // Check if owner MonoBehaviour still exists (not destroyed)
      if (owner == null || !owner)
      {
        // Owner destroyed, clean up this state machine
        CleanupStateMachine(owner, sm);
        continue;
      }

      // Check if owner is active and enabled
      var shouldBeActive = owner.enabled && owner.gameObject.activeInHierarchy;

      // Control power based on owner's active state
      // This pauses the state machine (no updates, no event callbacks) without calling Exit
      if (sm.IsTurnedOn != shouldBeActive) sm.SetTurnedOn(shouldBeActive);

      // Only update if owner MonoBehaviour is enabled and GameObject is active
      if (shouldBeActive) updateAction?.Invoke(sm);
      // If disabled or inactive, state machine is paused (power off, no updates, no events)
    }
  }

  void UpdateAllStateMachinesPower(bool turnOn)
  {
    foreach (var kvp in _managedStateMachines)
    {
      var owner = kvp.Key;
      var sm    = kvp.Value;

      if (owner == null || !owner) continue;

      // Only control power if owner is enabled
      // (GameObject inactive affects wrapper, but individual component disable is separate)
      var shouldBeActive = turnOn && owner.enabled;

      if (sm.IsTurnedOn != shouldBeActive)
        sm.SetTurnedOn(shouldBeActive);
    }
  }
}