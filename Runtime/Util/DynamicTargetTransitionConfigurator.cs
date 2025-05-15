using System;
using Nopnag.StateMachineLib.Transition;

namespace Nopnag.StateMachineLib.Util
{
    /// <summary>
    /// Configures a transition where the target state is determined dynamically at runtime.
    /// This is typically initiated via the fluent API like: <code>sourceUnit > StateGraph.DynamicTarget</code>
    /// or <code>graph.FromAnyToDynamic()</code>.
    /// </summary>
    public readonly struct DynamicTargetTransitionConfigurator
    {
        private readonly StateGraph _graphContext;
        private readonly StateUnit _sourceUnit; // Null if this is an "Any State" to dynamic target transition
        // private readonly DynamicTargetMarker _dynamicTargetMarker; // Not strictly needed to store the marker itself

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicTargetTransitionConfigurator"/> struct for a specific source state.
        /// </summary>
        /// <param name="sourceUnit">The source state unit for the transition.</param>
        public DynamicTargetTransitionConfigurator(StateUnit sourceUnit)
        {
            _sourceUnit = sourceUnit ?? throw new ArgumentNullException(nameof(sourceUnit));
            _graphContext = sourceUnit.BaseGraph; // Assumes StateUnit has BaseGraph (ParentGraph)
            if (_graphContext == null) throw new InvalidOperationException("SourceState must be part of a StateGraph.");
            // _dynamicTargetMarker = StateGraph.DynamicTarget; // Or pass it if it becomes non-static
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicTargetTransitionConfigurator"/> struct for an "Any State" transition.
        /// </summary>
        /// <param name="graphContext">The state graph context for this "Any State" transition.</param>
        /// <param name="dynamicTargetMarker">The dynamic target marker.</param> 
        internal DynamicTargetTransitionConfigurator(StateGraph graphContext, DynamicTargetMarker dynamicTargetMarker) // Added marker for signature
        {
            _graphContext = graphContext ?? throw new ArgumentNullException(nameof(graphContext));
            _sourceUnit = null; // Indicates "Any State"
            // _dynamicTargetMarker = dynamicTargetMarker;
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

            if (_sourceUnit != null) // Specific state to dynamic target
            {
                return ConditionalTransition.Connect(_sourceUnit, dynamicTargetPredicate);
            }
            else // Any state to dynamic target
            {
                return ConditionalTransition.Connect(_graphContext, dynamicTargetPredicate);
            }
        }
    }
} 