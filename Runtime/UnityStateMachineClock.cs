using UnityEngine;

namespace Nopnag.StateMachineLib
{
  /// <summary>Preserves the library's original Unity time inputs.</summary>
  public sealed class UnityStateMachineClock : IStateMachineClock
  {
    public static readonly UnityStateMachineClock Instance = new();

    UnityStateMachineClock() { }

    // Keep Time.time's original float sampling for existing callers. Custom clocks
    // may retain double precision without changing the default Unity behavior.
    public double UpdateTime => Time.time;
    public float FixedDeltaTime => Time.fixedDeltaTime;
  }
}
