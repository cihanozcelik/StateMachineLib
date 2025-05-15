using System;
using Nopnag.StateMachineLib.Transition;

namespace Nopnag.StateMachineLib
{
    /// <summary>
    /// Configures a transition where the target state is determined dynamically at runtime.
    /// This is typically initiated via the fluent API like: <code>sourceUnit > StateGraph.DynamicTarget</code>.
    /// </summary>
    public readonly struct DynamicTargetTransitionConfigurator
    {
        private readonly StateUnit _sourceUnit;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicTargetTransitionConfigurator"/> struct.
        /// </summary>
        /// <param name="sourceUnit">The source state unit for the transition.</param>
        public DynamicTargetTransitionConfigurator(StateUnit sourceUnit)
        {
            _sourceUnit = sourceUnit ?? throw new ArgumentNullException(nameof(sourceUnit));
        }

        /// <summary>
        /// Defines the condition that, when evaluated, returns the target <see cref="StateUnit"/> for this transition.
        /// The transition occurs if the predicate returns a non-null <see cref="StateUnit"/>.
        /// </summary>
        /// <param name="dynamicTargetPredicate">A function that takes the elapsed time in the source state (float)
        /// and returns the <see cref="StateUnit"/> to transition to, or null to not transition.</param>
        /// <returns>The created <see cref="ConditionalTransition"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="dynamicTargetPredicate"/> is null.</exception>
        public ConditionalTransition When(Func<float, StateUnit> dynamicTargetPredicate)
        {
            if (dynamicTargetPredicate == null) throw new ArgumentNullException(nameof(dynamicTargetPredicate));

            return ConditionalTransition.Connect(_sourceUnit, dynamicTargetPredicate);
        }
    }
} 