#nullable enable
namespace Nopnag.StateMachineLib
{
  public interface IPoweredNode
  {
    bool HasPower   { get; }
    bool IsActive   { get; }
    bool IsTurnedOn { get; }
    void AttachChild(IPoweredNode child);
    void DetachChild(IPoweredNode child);
    void RefreshPowerState();
    void SetParent(IPoweredNode? parent);
    void SetTurnedOn(bool        on);
  }
}