using System;
using System.Collections.Generic;
using Nopnag.StateMachineLib;
using UnityEngine;

[DisallowMultipleComponent]
public class StateMachineWrapper : MonoBehaviour
{
  sealed class ManagedStateMachineEntry
  {
    public readonly MonoBehaviour Owner;
    public readonly StateMachine StateMachine;
    public bool Removed;

    public ManagedStateMachineEntry(MonoBehaviour owner, StateMachine stateMachine)
    {
      Owner        = owner;
      StateMachine = stateMachine;
    }
  }

  // Track if OnEnable was called (to differentiate first creation vs re-enable)
  bool _hasBeenDisabled = false;
  // Dictionary: MonoBehaviour -> managed entry mapping
  Dictionary<MonoBehaviour, ManagedStateMachineEntry> _managedStateMachines = new();
  // Stable iteration order without copying the dictionary every update phase
  List<ManagedStateMachineEntry> _managedStateMachineEntries = new();
  // Track which StateMachines haven't been Started yet (created while owner was disabled)
  HashSet<StateMachine> _pendingStartStateMachines = new();
  int  _activeIterationCount;
  bool _needsCompaction;
  
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
      return _managedStateMachines[owner].StateMachine;
    }

    var sm    = new StateMachine();
    var entry = new ManagedStateMachineEntry(owner, sm);
    _managedStateMachines[owner] = entry;
    _managedStateMachineEntries.Add(entry);
    
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

    if (_managedStateMachines.TryGetValue(owner, out var entry))
    {
      UnregisterEntry(entry);

      try
      {
        var sm = entry.StateMachine;
        sm?.Exit();
        sm?.Dispose();
      }
      catch (Exception ex)
      {
        Debug.LogError($"Exception while removing StateMachine for {owner.GetType().Name}: {ex}",
          owner);
      }
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
    foreach (var entry in _managedStateMachineEntries)
    {
      if (entry.Removed) continue;

      var owner = entry.Owner;
      var sm    = entry.StateMachine;

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
    foreach (var entry in _managedStateMachineEntries)
    {
      if (entry.Removed) continue;

      var sm = entry.StateMachine;
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
    _managedStateMachineEntries.Clear();
    _pendingStartStateMachines.Clear();
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

  // Direct entry points keep the allocation regression test outside Unity's frame/test
  // runner bookkeeping while exercising the exact same warmed wrapper paths.
  internal void UpdateManagedStateMachinesForTesting()
  {
    UpdateAllStateMachines(_updateAction);
  }

  internal void FixedUpdateManagedStateMachinesForTesting()
  {
    UpdateAllStateMachines(_fixedUpdateAction);
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

  void CleanupStateMachine(ManagedStateMachineEntry entry)
  {
    // Owner MonoBehaviour was destroyed
    // We're in an Update call, so child objects might already be destroyed
    // Skip Exit to avoid exceptions, go straight to Dispose

    UnregisterEntry(entry);

    try
    {
      entry.StateMachine?.Dispose();
    }
    catch (Exception ex)
    {
      Debug.LogError($"Exception during StateMachine cleanup: {ex}", this);
    }
  }

  void UpdateAllStateMachines(Action<StateMachine> updateAction)
  {
    // Capture the count so entries created by a callback begin updating next phase,
    // matching the previous dictionary-snapshot behavior.
    var entryCount = _managedStateMachineEntries.Count;
    _activeIterationCount++;

    try
    {
      for (var i = 0; i < entryCount; i++)
      {
        var entry = _managedStateMachineEntries[i];
        if (entry.Removed) continue;

        var owner = entry.Owner;
        var sm    = entry.StateMachine;

        // Check if owner MonoBehaviour still exists (not destroyed)
        if (!owner)
        {
          // Owner destroyed, clean up this state machine
          CleanupStateMachine(entry);
          continue;
        }

        // The wrapper and owner share a GameObject. Unity only invokes this method
        // while that GameObject is active; OnDisable/OnEnable handles hierarchy changes.
        var shouldBeActive = owner.enabled;

        // Control power based on owner's active state
        // This pauses the state machine (no updates, no event callbacks) without calling Exit
        if (sm.IsTurnedOn != shouldBeActive) sm.SetTurnedOn(shouldBeActive);

        // Only update if owner MonoBehaviour is enabled
        if (shouldBeActive) updateAction?.Invoke(sm);
      }
    }
    finally
    {
      _activeIterationCount--;
      if (_activeIterationCount == 0 && _needsCompaction)
        CompactRemovedEntries();
    }
  }

  void UpdateAllStateMachinesPower(bool turnOn)
  {
    foreach (var entry in _managedStateMachineEntries)
    {
      if (entry.Removed) continue;

      var owner = entry.Owner;
      var sm    = entry.StateMachine;

      if (owner == null || !owner) continue;

      // Only control power if owner is enabled
      // (GameObject inactive affects wrapper, but individual component disable is separate)
      var shouldBeActive = turnOn && owner.enabled;

      if (sm.IsTurnedOn != shouldBeActive)
        sm.SetTurnedOn(shouldBeActive);
    }
  }

  void UnregisterEntry(ManagedStateMachineEntry entry)
  {
    if (entry.Removed) return;

    entry.Removed = true;
    _managedStateMachines.Remove(entry.Owner);
    _pendingStartStateMachines.Remove(entry.StateMachine);

    if (_activeIterationCount > 0)
    {
      _needsCompaction = true;
      return;
    }

    _managedStateMachineEntries.Remove(entry);
  }

  void CompactRemovedEntries()
  {
    var writeIndex = 0;

    for (var readIndex = 0; readIndex < _managedStateMachineEntries.Count; readIndex++)
    {
      var entry = _managedStateMachineEntries[readIndex];
      if (entry.Removed) continue;

      if (writeIndex != readIndex)
        _managedStateMachineEntries[writeIndex] = entry;

      writeIndex++;
    }

    if (writeIndex < _managedStateMachineEntries.Count)
      _managedStateMachineEntries.RemoveRange(writeIndex,
        _managedStateMachineEntries.Count - writeIndex);

    _needsCompaction = false;
  }
}
