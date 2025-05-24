#nullable enable
using System.Collections.Generic;

public class PoweredNode : IPoweredNode
{
  readonly List<IPoweredNode> _children = new();
  readonly bool               _isPowerSource;
  IPoweredNode?               _parent;

  public PoweredNode(bool isPowerSource = false)
  {
    _isPowerSource = isPowerSource;
  }

  public void AttachChild(IPoweredNode child)
  {
    _children.Add(child);
    child.SetParent(this);
  }

  public void DetachChild(IPoweredNode child)
  {
    _children.Remove(child);
    child.SetParent(null);
  }

  public bool HasPower { get; private set; } = false;
  public bool IsActive => IsTurnedOn && HasPower;

  public bool IsTurnedOn { get; private set; } = false;

  public void RefreshPowerState()
  {
    var shouldHavePower = _parent?.IsActive ?? _isPowerSource;

    if (HasPower == shouldHavePower)
      return;

    HasPower = shouldHavePower;

    foreach (var child in _children)
      child.RefreshPowerState();
  }

  public void SetParent(IPoweredNode? parent)
  {
    if (_parent == parent) return;
    _parent = parent;
    RefreshPowerState();
  }

  public void SetTurnedOn(bool on)
  {
    if (IsTurnedOn == on) return;

    IsTurnedOn = on;
    RefreshPowerState();
  }
}