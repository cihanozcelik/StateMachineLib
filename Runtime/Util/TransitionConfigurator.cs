using System;
using Nopnag.EventBusLib;
using Nopnag.StateMachineLib.Transition;

namespace Nopnag.StateMachineLib.Util
{
    /// <summary>
    /// Configures a transition.
    /// If FromState is null, it's an "Any State" transition relative to the GraphContext.
    /// This struct is designed to be lightweight and avoid heap allocations for the fluent API.
    /// </summary>
    public readonly struct TransitionConfigurator
    {
        internal readonly StateGraph GraphContext;
        internal readonly StateUnit FromState; // Null if this is an "Any State" transition
        internal readonly StateUnit ToState;

        internal TransitionConfigurator(StateUnit fromState, StateUnit toState)
        {
            if (fromState == null) throw new ArgumentNullException(nameof(fromState));
            if (toState == null) throw new ArgumentNullException(nameof(toState));
            
            FromState = fromState;
            ToState = toState;
            GraphContext = fromState.BaseGraph; // Changed ParentGraph to BaseGraph
            if (GraphContext == null) throw new InvalidOperationException("FromState must be part of a StateGraph.");
        }

        // Constructor for "Any State" transitions
        internal TransitionConfigurator(StateGraph graphContext, StateUnit toState)
        {
            if (graphContext == null) throw new ArgumentNullException(nameof(graphContext));
            if (toState == null) throw new ArgumentNullException(nameof(toState));

            FromState = null; // Indicates "Any State"
            ToState = toState;
            GraphContext = graphContext;
        }

        /// <summary>
        /// Creates a BasicTransition that occurs when the given predicate returns true.
        /// The predicate takes the elapsed time in the current state as a parameter.
        /// </summary>
        /// <param name="predicate">The condition to check. Transition occurs if it returns true.</param>
        public void When(Func<float, bool> predicate)
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null) // Specific state to specific state
            {
                BasicTransition.Connect(FromState, ToState, predicate);
            }
            else // Any state to specific state
            {
                BasicTransition.Connect(GraphContext, ToState, predicate);
            }
        }

        /// <summary>
        /// Creates a BasicTransition that occurs after a specified duration has passed.
        /// </summary>
        /// <param name="duration">The time in seconds to wait before the transition occurs.</param>
        public void After(float duration)
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            Func<float, bool> condition = elapsedTime => elapsedTime >= duration; // Explicitly typed delegate
            if (FromState != null)
            {
                BasicTransition.Connect(FromState, ToState, condition);
            }
            else
            {
                BasicTransition.Connect(GraphContext, ToState, condition);
            }
        }

        /// <summary>
        /// Creates a TransitionByEvent that occurs when an event of type TEvent is raised.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to listen for.</typeparam>
        public void On<TEvent>() where TEvent : BusEvent
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null)
            {
                TransitionByEvent.Connect<TEvent>(FromState, ToState);
            }
            else
            {
                TransitionByEvent.Connect<TEvent>(GraphContext, ToState);
            }
        }

        /// <summary>
        /// Creates a TransitionByEvent that occurs when an event of type TEvent is raised and the predicate returns true.
        /// </summary>
        /// <typeparam name="TEvent">The type of the event to listen for.</typeparam>
        /// <param name="predicate">The condition to check when the event is raised.</param>
        public void On<TEvent>(Func<TEvent, bool> predicate) where TEvent : BusEvent
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null)
            {
                TransitionByEvent.Connect<TEvent>(FromState, ToState, predicate);
            }
            else
            {
                TransitionByEvent.Connect<TEvent>(GraphContext, ToState, predicate);
            }
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
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (query == null) { UnityEngine.Debug.LogError("EventQuery cannot be null for TransitionByEvent."); return; }

            if (FromState != null)
            {
                if (predicate == null)
                    TransitionByEvent.Connect<TEvent>(FromState, ToState, query);
                else
                    TransitionByEvent.Connect<TEvent>(FromState, ToState, query, predicate);
            }
            else
            {
                if (predicate == null)
                    TransitionByEvent.Connect<TEvent>(GraphContext, ToState, query);
                else
                    TransitionByEvent.Connect<TEvent>(GraphContext, ToState, query, predicate);
            }
        }

        /// <summary>
        /// Creates a TransitionByAction that occurs when the specified signal Action is invoked.
        /// </summary>
        /// <typeparam name="TActionParam">The type of the parameter for the signal Action.</typeparam>
        /// <param name="signal">The signal Action to listen to (passed by reference).</param>
        public void On<TActionParam>(ref Action<TActionParam> signal)
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null)
            {
                TransitionByAction.Connect<TActionParam>(FromState, ToState, ref signal);
            }
            else
            {
                TransitionByAction.Connect<TActionParam>(GraphContext, ToState, ref signal);
            }
        }
        
        /// <summary>
        /// Creates a TransitionByAction that occurs when the specified parameterless signal Action is invoked.
        /// </summary>
        /// <param name="signal">The parameterless signal Action to listen to (passed by reference).</param>
        public void On(ref Action signal)
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null)
            {
                TransitionByAction.Connect(FromState, ToState, ref signal);
            }
            else
            {
                TransitionByAction.Connect(GraphContext, ToState, ref signal);
            }
        }

        /// <summary>
        /// Creates a DirectTransition that occurs unconditionally and immediately.
        /// </summary>
        public void Immediately()
        {
            if (ToState == null) { UnityEngine.Debug.LogError(ErrorMessageTargetNull); return; }
            if (FromState != null)
            {
                DirectTransition.Connect(FromState, ToState);
            }
            else
            {
                DirectTransition.Connect(GraphContext, ToState);
            }
        }

        private const string ErrorMessageTargetNull = "TransitionConfigurator: Target state must not be null.";
    }
} 