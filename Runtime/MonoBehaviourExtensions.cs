using System;
using UnityEngine;

namespace Nopnag.StateMachineLib
{
    public static class MonoBehaviourExtensions
    {
        /// <summary>
        /// Creates a new StateMachine instance whose lifecycle is automatically managed.
        /// The setup callback allows you to configure the StateMachine (create graphs, states, transitions).
        /// The StateMachine will be automatically started after the callback completes, ensuring
        /// that event transitions work immediately (even in Awake).
        /// </summary>
        /// <param name="mb">The MonoBehaviour to link the StateMachine's lifecycle to.</param>
        /// <param name="setupCallback">Callback to configure the StateMachine</param>
        /// <returns>A new StateMachine instance that is lifecycle-managed and started.</returns>
        public static StateMachine CreateManagedStateMachine(this MonoBehaviour mb, Action<StateMachine> setupCallback)
        {
            return mb.CreateManagedStateMachine(StateMachineClock.Default, setupCallback);
        }

        /// <summary>
        /// Creates and starts a lifecycle-managed machine using the supplied clock.
        /// The clock is installed before setup and OnEnter callbacks execute.
        /// </summary>
        public static StateMachine CreateManagedStateMachine(this MonoBehaviour mb,
            IStateMachineClock clock, Action<StateMachine> setupCallback)
        {
            var wrapper = StateMachineWrapper.GetOrCreate(mb.gameObject);
            return wrapper.CreateStateMachineFor(mb, clock, setupCallback);
        }
    }
} 