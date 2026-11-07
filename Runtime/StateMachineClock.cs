using UnityEngine;

namespace Nopnag.StateMachineLib
{
  /// <summary>Application startup configuration for newly constructed roots.</summary>
  public static class StateMachineClock
  {
    /// <summary>
    /// Captured by parameterless StateMachine/StateGraph constructors and managed
    /// creation. Changing this does not retarget existing trees. Set it before
    /// constructing application machines, or pass a clock explicitly per root.
    /// Reset to Unity time at SubsystemRegistration, including without domain reload.
    /// </summary>
    public static IStateMachineClock Default { get; set; } = UnityStateMachineClock.Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
      Default = UnityStateMachineClock.Instance;
    }
  }
}
