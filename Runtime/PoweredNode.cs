#nullable enable
using System;
using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
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
      if (child == null) return;
      if (child == this)
        throw new ArgumentException("Node cannot be attached as its own child");
      if (_children.Contains(child)) return;

      _children.Add(child);
      child.SetParent(this);
    }

    public void DetachChild(IPoweredNode child)
    {
      if (!_children.Remove(child)) return;
      child.SetParent(null);
    }

    // IPoweredNode implementation
    public bool HasPower   { get; private set; }
    public bool IsActive   => HasPower && IsTurnedOn;
    public bool IsTurnedOn { get; private set; }

    public void RefreshPowerState()
    {
      HasPower = (_isPowerSource && IsTurnedOn) || _parent?.IsActive == true;

      foreach (var child in _children) child.RefreshPowerState();
    }

    public void SetParent(IPoweredNode? parent)
    {
      if (parent == this)
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
}