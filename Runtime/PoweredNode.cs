#nullable enable
using System;
using System.Collections.Generic;

public class PoweredNode : IPoweredNode
{
  readonly List<IPoweredNode> _children = new();
  readonly bool               _isPowerSource;
  IPoweredNode?               _parent;

  public PoweredNode(bool isPowerSource = false)
  {
    _isPowerSource = isPowerSource;
    RefreshPowerState();
  }

  public void AttachChild(IPoweredNode child)
  {
    if (child == this)
      throw new ArgumentException("Node cannot be attached as its own child");

    // Skip if the child is already in this list
    if (_children.Contains(child)) return;

    //Optional-safety: if the child already has a different parent, detach it first
    if (child is PoweredNode pn && pn._parent != null)
      pn._parent.DetachChild(child);

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
    HasPower = (_isPowerSource && IsTurnedOn) || _parent?.IsActive == true;

    foreach (var child in _children)
      child.RefreshPowerState();
  }

  public void SetParent(IPoweredNode? parent)
  {
    if (parent == this) // ← compare against the argument
      throw new ArgumentException("Node cannot be its own parent");
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