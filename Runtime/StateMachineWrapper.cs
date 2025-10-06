using Nopnag.StateMachineLib;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StateMachineWrapper : MonoBehaviour
{
    // Dictionary: MonoBehaviour -> StateMachine mapping
    private Dictionary<MonoBehaviour, StateMachine> _managedStateMachines = new();
    
    /// <summary>
    /// Gets or creates the StateMachineWrapper component on the given GameObject.
    /// </summary>
    public static StateMachineWrapper GetOrCreate(GameObject gameObject)
    {
        if (gameObject == null)
            throw new ArgumentNullException(nameof(gameObject));
            
        var wrapper = gameObject.GetComponent<StateMachineWrapper>();
        if (wrapper == null)
        {
            wrapper = gameObject.AddComponent<StateMachineWrapper>();
        }
        return wrapper;
    }
    
    /// <summary>
    /// Creates and registers a new StateMachine for the given MonoBehaviour owner.
    /// </summary>
    public StateMachine CreateStateMachineFor(MonoBehaviour owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
            
        if (_managedStateMachines.ContainsKey(owner))
        {
            Debug.LogWarning($"StateMachine already exists for {owner.GetType().Name} on {owner.gameObject.name}. Returning existing instance.", owner);
            return _managedStateMachines[owner];
        }
        
        var sm = new StateMachine();
        _managedStateMachines[owner] = sm;
        return sm;
    }
    
    void Update()
    {
        UpdateAllStateMachines(sm => sm.UpdateMachine());
    }
    
    void FixedUpdate()
    {
        UpdateAllStateMachines(sm => sm.FixedUpdateMachine());
    }
    
    void LateUpdate()
    {
        UpdateAllStateMachines(sm => sm.LateUpdateMachine());
    }
    
    void OnEnable()
    {
        // When wrapper becomes enabled (GameObject active or component enabled)
        // Turn on power for all managed state machines
        UpdateAllStateMachinesPower(true);
    }
    
    void OnDisable()
    {
        // When wrapper becomes disabled (GameObject inactive or component disabled)
        // Turn off power for all managed state machines (pause without Exit)
        UpdateAllStateMachinesPower(false);
    }
    
    void UpdateAllStateMachinesPower(bool turnOn)
    {
        foreach (var kvp in _managedStateMachines)
        {
            var owner = kvp.Key;
            var sm = kvp.Value;
            
            if (owner == null || !owner) continue;
            
            // Only control power if owner is enabled
            // (GameObject inactive affects wrapper, but individual component disable is separate)
            bool shouldBeActive = turnOn && owner.enabled;
            
            if (sm.IsTurnedOn != shouldBeActive)
            {
                Debug.Log($"[OnEnable/OnDisable] {owner.GetType().Name} power: {sm.IsTurnedOn} -> {shouldBeActive}");
                sm.SetTurnedOn(shouldBeActive);
            }
        }
    }
    
    void UpdateAllStateMachines(Action<StateMachine> updateAction)
    {
        // Iterate through all managed state machines
        // Use ToArray to avoid modification during iteration if cleanup happens
        var pairs = new List<KeyValuePair<MonoBehaviour, StateMachine>>(_managedStateMachines);
        
        foreach (var kvp in pairs)
        {
            var owner = kvp.Key;
            var sm = kvp.Value;
            
            // Check if owner MonoBehaviour still exists (not destroyed)
            if (owner == null || !owner)
            {
                // Owner destroyed, clean up this state machine
                CleanupStateMachine(owner, sm);
                continue;
            }
            
            // Check if owner is active and enabled
            bool shouldBeActive = owner.enabled && owner.gameObject.activeInHierarchy;
            
            // Debug: Log state changes
            if (sm.IsTurnedOn != shouldBeActive)
            {
                Debug.Log($"[StateMachineWrapper] {owner.GetType().Name} power changing: {sm.IsTurnedOn} -> {shouldBeActive} (enabled={owner.enabled}, activeInHierarchy={owner.gameObject.activeInHierarchy})");
            }
            
            // Control power based on owner's active state
            // This pauses the state machine (no updates, no event callbacks) without calling Exit
            if (sm.IsTurnedOn != shouldBeActive)
            {
                sm.SetTurnedOn(shouldBeActive);
            }
            
            // Only update if owner MonoBehaviour is enabled and GameObject is active
            if (shouldBeActive)
            {
                updateAction?.Invoke(sm);
            }
            // If disabled or inactive, state machine is paused (power off, no updates, no events)
        }
    }
    
    void OnDestroy()
    {
        // GameObject is being destroyed
        // First, call Exit on all state machines while GameObjects are still alive
        foreach (var kvp in _managedStateMachines)
        {
            var owner = kvp.Key;
            var sm = kvp.Value;
            
            // Only call Exit if owner still exists
            // (might have been destroyed before this wrapper's OnDestroy)
            if (owner != null && owner)
            {
                try
                {
                    sm?.Exit();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception in StateMachine.Exit() for {owner?.GetType().Name}: {ex}", this);
                }
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
        {
            _managedStateMachines.Remove(owner);
        }
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
                Debug.LogError($"Exception while removing StateMachine for {owner.GetType().Name}: {ex}", owner);
            }
            
            _managedStateMachines.Remove(owner);
        }
    }
    
#if UNITY_EDITOR
    // Debug info in Inspector
    void OnValidate()
    {
        // Show count of managed state machines in inspector
        if (_managedStateMachines != null)
        {
            gameObject.name = $"{gameObject.name.Split('[')[0].Trim()} [{_managedStateMachines.Count} SMs]";
        }
    }
#endif
}
