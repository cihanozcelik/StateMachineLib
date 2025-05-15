namespace Nopnag.StateMachineLib.Util
{
    /// <summary>
    /// A marker struct used with the fluent API to indicate that a transition's target
    /// will be determined dynamically by a predicate at runtime.
    /// </summary>
    public readonly struct DynamicTargetMarker { }
} 