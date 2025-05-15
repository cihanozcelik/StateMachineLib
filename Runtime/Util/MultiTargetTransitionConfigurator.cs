using System;
using System.Linq;
using Nopnag.StateMachineLib.Transition; // For ConditionalTransitionByIndex
using UnityEngine; // For Debug.LogError

namespace Nopnag.StateMachineLib.Util
{
    /// <summary>
    /// Configures a transition from a single source state to one of multiple potential target states,
    /// typically decided by an index-based predicate.
    /// This struct is designed to be lightweight and avoid heap allocations for the fluent API.
    /// </summary>
    public readonly struct MultiTargetTransitionConfigurator
    {
        internal readonly StateUnit FromState;
        internal readonly StateUnit[] ToStates;

        internal MultiTargetTransitionConfigurator(StateUnit fromState, StateUnit[] toStates)
        {
            FromState = fromState;
            ToStates = toStates;
        }

        /// <summary>
        /// Creates a ConditionalTransitionByIndex that occurs when the given predicate 
        /// returns a valid index into the configured target states array.
        /// The predicate takes the elapsed time in the current state as a parameter.
        /// </summary>
        /// <param name="indexPredicate">
        /// A function that takes the elapsed time in the source state and returns an integer.
        /// This integer should be an index into the target states array. 
        /// If the index is out of bounds or -1, no transition will occur.
        /// </param>
        public void When(Func<float, int> indexPredicate)
        {
            if (FromState == null)
            {
                Debug.LogError("MultiTargetTransitionConfigurator: Source state must not be null.");
                return;
            }
            if (ToStates == null || !ToStates.Any())
            {
                Debug.LogError("MultiTargetTransitionConfigurator: Target states array must not be null or empty.");
                return;
            }
            // Ensure all target states are valid (optional, but good practice)
            if (ToStates.Any(s => s == null))
            {
                 Debug.LogError("MultiTargetTransitionConfigurator: One or more target states in the array are null.");
                return;
            }

            ConditionalTransitionByIndex.Connect(this.FromState, this.ToStates, indexPredicate);
        }
    }
} 