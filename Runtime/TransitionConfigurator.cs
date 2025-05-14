using System;
using Nopnag.EventBusLib;
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

        /// <summary>
        /// Creates a TransitionByEvent that occurs when an event of type TEvent is raised.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to listen for.</typeparam>
        public void On<TEvent>() where TEvent : BusEvent
        {
            if (FromState == null || ToState == null) { UnityEngine.Debug.LogError(ErrorMessage); return; }
            TransitionByEvent.Connect<TEvent>(FromState, ToState);
        }

        /// <summary>
        /// Creates a TransitionByEvent that occurs when an event of type TEvent is raised and the predicate returns true.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to listen for.</typeparam>
        /// <param name="predicate">The condition to check when the event is raised.</param>
        public void On<TEvent>(Func<TEvent, bool> predicate) where TEvent : BusEvent
        {
            if (FromState == null || ToState == null) { UnityEngine.Debug.LogError(ErrorMessage); return; }
            TransitionByEvent.Connect<TEvent>(FromState, ToState, predicate);
        }

        /// <summary>
        /// Creates a TransitionByEvent that occurs when an event matching the EventQuery is raised.
        /// Optionally, a further predicate can be applied.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to listen for.</typeparam>
        /// <param name="query">The EventQuery to filter events.</param>
        /// <param name="predicate">An optional additional condition to check when the queried event is raised.</param>
        public void On<TEvent>(EventQuery<TEvent> query, Func<TEvent, bool> predicate = null) where TEvent : BusEvent
        {
            if (FromState == null || ToState == null) { UnityEngine.Debug.LogError(ErrorMessage); return; }
            if (query == null) { UnityEngine.Debug.LogError("EventQuery cannot be null for TransitionByEvent."); return; }

            if (predicate == null)
            {
                TransitionByEvent.Connect<TEvent>(FromState, ToState, query);
            }
            else
            {
                TransitionByEvent.Connect<TEvent>(FromState, ToState, query, predicate);
            }
        }

        /// <summary>
        /// Creates a TransitionByAction that occurs when the specified signal Action is invoked.
        /// </summary>
        /// <typeparam name="TActionParam">The type of the parameter for the signal Action.</typeparam>
        /// <param name="signal">The signal Action to listen to (passed by reference).</param>
        public void On<TActionParam>(ref Action<TActionParam> signal)
        {
            if (FromState == null || ToState == null) { UnityEngine.Debug.LogError(ErrorMessage); return; }
            TransitionByAction.Connect<TActionParam>(FromState, ToState, ref signal);
        }
        
        // Overload for parameterless Action for TransitionByAction
        /// <summary>
        /// Creates a TransitionByAction that occurs when the specified parameterless signal Action is invoked.
        /// </summary>
        /// <param name="signal">The parameterless signal Action to listen to (passed by reference).</param>
        public void On(ref Action signal)
        {
            if (FromState == null || ToState == null) { UnityEngine.Debug.LogError(ErrorMessage); return; }
            TransitionByAction.Connect(FromState, ToState, ref signal);
        }

        private const string ErrorMessage = "TransitionConfigurator: Source and target states must be configured before defining the condition.";
    }
} 