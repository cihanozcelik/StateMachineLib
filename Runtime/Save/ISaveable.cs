namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// Something whose runtime state can be saved and loaded back exactly.
  /// <see cref="StateMachine"/> and <see cref="StateGraph"/> implement it for the
  /// library's own variables. A state's behavior also depends on the developer's
  /// fields, closure data and services, which the library cannot see: the owner of a
  /// machine implements this interface too, and its snapshot holds the machine's
  /// snapshot together with the owner's own variables.
  /// </summary>
  public interface ISaveable<TSnapshot>
  {
    /// <summary>Copies the current runtime state into a new snapshot.</summary>
    TSnapshot Save();

    /// <summary>
    /// Puts the runtime state back exactly as <see cref="Save"/> captured it. No
    /// lifecycle callback runs and nothing is evaluated during the call.
    /// </summary>
    void Load(TSnapshot snapshot);
  }
}
