using System;

namespace Nopnag.StateMachineLib
{
  /// <summary>
  /// One state in a snapshot, current or not. Timer entries follow registration order.
  /// </summary>
  [Serializable]
  public sealed class SavedState
  {
    public bool    TurnedOn;
    public float   LocalTimeScale;
    public float   DeltaTime;
    public float   Elapsed;
    public float   FixedDeltaTime;
    public float   FixedElapsed;
    public int     HostedGraphCount;
    public bool[]  AtInvoked;
    public float[] AtEveryNext;
    public bool[]  AtFixedInvoked;
    public float[] AtEveryFixedNext;
  }
}
