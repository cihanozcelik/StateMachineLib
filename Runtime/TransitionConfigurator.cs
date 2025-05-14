using System;
using Nopnag.StateMachineLib.Transition;

namespace Nopnag.StateMachineLib
{
    /// <summary>
    /// Configures a transition between two specific states.
    /// This struct is designed to be lightweight and avoid heap allocations for the fluent API.
    /// </summary>
    public readonly struct SingleTargetTransitionConfigurator
    {
        internal readonly StateUnit FromState;
        internal readonly StateUnit ToState;

        internal SingleTargetTransitionConfigurator(StateUnit fromState, StateUnit toState)
        {
            FromState = fromState;
            ToState = toState;
        }

        /// <summary>
        /// Creates a BasicTransition that occurs when the given predicate returns true.
        /// The predicate takes the elapsed time in the current state as a parameter.
        /// </summary>
        /// <param name="predicate">The condition to check. Transition occurs if it returns true.</param>
        public void When(Func<float, bool> predicate)
        {
            if (FromState == null || ToState == null)
            {
                // Or throw new InvalidOperationException("Source and target states must not be null.");
                UnityEngine.Debug.LogError("TransitionConfigurator: Source and target states must be configured before defining the condition.");
                return;
            }
            BasicTransition.Connect(FromState, ToState, predicate);
        }

        /// <summary>
        /// Creates a BasicTransition that occurs after a specified duration has passed.
        /// </summary>
        /// <param name="duration">The time in seconds to wait before the transition occurs.</param>
        public void After(float duration)
        {
            if (FromState == null || ToState == null)
            {
                UnityEngine.Debug.LogError("TransitionConfigurator: Source and target states must be configured before defining the duration.");
                return;
            }
            if (duration < 0)
            {
                UnityEngine.Debug.LogWarning("TransitionConfigurator.After: Duration cannot be negative. Transition will likely never occur or occur immediately if duration is 0.");
                // We could choose to make it immediate if duration is <= 0, or let BasicTransition handle it.
                // For now, let BasicTransition's predicate (elapsedTime > duration) handle it. If duration is negative, it'll be true immediately.
            }
            BasicTransition.Connect(FromState, ToState, elapsedTime => elapsedTime >= duration);
        }
    }
} 