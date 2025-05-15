using Nopnag.StateMachineLib;
using System;
using UnityEngine;

public class StateMachineWrapper : IDisposable
{
    public StateMachine StateMachine { get; private set; }
    MonoBehaviour _monitoredBehaviour;
    bool _isDisposed = false;

    public StateMachineWrapper(MonoBehaviour behaviourToMonitor)
    {
        if (behaviourToMonitor == null)
        {
            throw new ArgumentNullException(nameof(behaviourToMonitor), "Monitored MonoBehaviour cannot be null.");
        }

        _monitoredBehaviour = behaviourToMonitor;
        StateMachine = new StateMachine();

        if (ManualEventManager.Instance != null)
        {
            ManualEventManager.Instance.OnUpdate += HandleUnityUpdate;
            ManualEventManager.Instance.OnFixedUpdate += HandleUnityFixedUpdate;
            ManualEventManager.Instance.OnLateUpdate += HandleUnityLateUpdate;
        }
        else
        {
            Debug.LogError("StateMachineWrapper: ManualEventManager.Instance is null. Automatic updates will not occur.", _monitoredBehaviour);
        }
    }

    void HandleUnityUpdate() => ProcessStateMachineUpdates(() => StateMachine?.UpdateMachine());
    void HandleUnityFixedUpdate() => ProcessStateMachineUpdates(() => StateMachine?.FixedUpdateMachine());
    void HandleUnityLateUpdate() => ProcessStateMachineUpdates(() => StateMachine?.LateUpdateMachine());

    void ProcessStateMachineUpdates(Action stateMachineUpdateAction)
    {
        if (_isDisposed)
        {
            return;
        }

        // Check if the MonoBehaviour has been destroyed
        // A Unity object becomes "fake null" when destroyed.
        if (_monitoredBehaviour == null || !_monitoredBehaviour) 
        {
            CleanupAndDispose();
            return;
        }

        // Check if the MonoBehaviour is disabled
        if (!_monitoredBehaviour.enabled)
        {
            // If it was started, it's now effectively "paused".
            // If it was disabled before ever being started, _isStarted remains false.
            return;
        }

        // MonoBehaviour is alive and enabled
        stateMachineUpdateAction?.Invoke(); 
    }
    
    void CleanupAndDispose()
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

        StateMachine?.Exit(); 

        if (ManualEventManager.Instance != null)
        {
            ManualEventManager.Instance.OnUpdate -= HandleUnityUpdate;
            ManualEventManager.Instance.OnFixedUpdate -= HandleUnityFixedUpdate;
            ManualEventManager.Instance.OnLateUpdate -= HandleUnityLateUpdate;
        }

        StateMachine = null; 
        _monitoredBehaviour = null;
        // Debug.Log("StateMachineWrapper disposed."); // Optional: for debugging
    }

    public void Dispose()
    {
        CleanupAndDispose();
        // GC.SuppressFinalize(this); // Only if a finalizer is added, which is not currently planned.
    }
} 