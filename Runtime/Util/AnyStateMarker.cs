namespace Nopnag.StateMachineLib.Util
{
    /// <summary>
    /// A marker type used with StateGraph.Any to denote the source of an "Any State" transition via operator overloading.
    /// This allows for the syntax: (StateGraph.Any > targetState)
    /// </summary>
    public readonly struct AnyStateMarker { }
} 