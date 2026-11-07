namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// Authoritative time input for a machine and its graph tree. The owner advances
  /// this clock before ticking, and keeps it alive for the lifetime of the tree.
  /// Reading either property must have no side effects or managed allocations.
  /// </summary>
  public interface IStateMachineClock
  {
    /// <summary>
    /// Absolute scaled Update time in seconds. Entry, resume and load rebase to
    /// this value. Advance monotonically between those boundaries.
    /// </summary>
    double UpdateTime { get; }

    /// <summary>Scaled seconds consumed by each parameterless FixedUpdate tick.</summary>
    float FixedDeltaTime { get; }
  }
}
