using UnityEngine;

namespace Nopnag.StateMachineLib // Assuming StateMachine and StateMachineWrapper are in this namespace
{
    public static class MonoBehaviourExtensions
    {
        /// <summary>
        /// Creates a new StateMachine instance whose lifecycle (Start, Update, Exit) 
        /// is automatically managed and tied to this MonoBehaviour.
        /// - The StateMachine will automatically receive Update, FixedUpdate, and LateUpdate calls.
        /// - It will automatically Start when the MonoBehaviour is active and updates begin.
        /// - It will pause updates if the MonoBehaviour is disabled.
        /// - It will automatically call Exit() on the StateMachine when the MonoBehaviour is destroyed.
        /// </summary>
        /// <param name="mb">The MonoBehaviour to link the StateMachine's lifecycle to.</param>
        /// <returns>A new StateMachine instance that is lifecycle-managed.</returns>
        public static StateMachine CreateManagedStateMachine(this MonoBehaviour mb)
        {
            // StateMachineWrapper's constructor subscribes to ManualEventManager,
            // which keeps the wrapper instance alive even if this local 'wrapper' variable goes out of scope.
            // The wrapper will self-dispose when 'mb' is destroyed.
            var wrapper = new StateMachineWrapper(mb);
            return wrapper.StateMachine;
        }
    }
} 