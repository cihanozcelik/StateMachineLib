#nullable enable
using NUnit.Framework;

namespace Nopnag.StateMachineLib.Tests
{
  public class PoweredNodeTests
  {
    PoweredNode _childNode;
    PoweredNode _grandChildNode;
    PoweredNode _parentNode;
    PoweredNode _powerSource;

    [Test]
    public void Child_GetsPowerFromActiveParent()
    {
      // Setup: Power source is active
      _powerSource!.SetTurnedOn(true);

      // Attach child to power source
      _powerSource.AttachChild(_parentNode!);

      Assert.IsTrue(_parentNode.HasPower, "Child should have power from active parent");
      Assert.IsFalse(_parentNode.IsActive, "Child should not be active until turned on");

      // Turn on child
      _parentNode.SetTurnedOn(true);
      Assert.IsTrue(_parentNode.IsActive, "Child should be active when turned on and has power");
    }

    [Test]
    public void Child_LosesPowerWhenParentTurnsOff()
    {
      // Setup: Power source is active, child is attached and turned on
      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode!);
      _parentNode.SetTurnedOn(true);

      Assert.IsTrue(_parentNode.IsActive, "Child should be active initially");
      _powerSource.SetTurnedOn(false);

      Assert.IsFalse(_powerSource.IsActive, "Power source should not be active");
      Assert.IsFalse(_parentNode.HasPower, "Child should lose power");
      Assert.IsFalse(_parentNode.IsActive, "Child should not be active without power");
    }

    [Test]
    public void DetachChild_RemovesPowerConnection()
    {
      // Setup: Active power source with active child
      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode!);
      _parentNode.SetTurnedOn(true);

      Assert.IsTrue(_parentNode.IsActive, "Child should be active initially");

      // Detach child
      _powerSource.DetachChild(_parentNode);

      Assert.IsFalse(_parentNode.HasPower, "Detached child should lose power");
      Assert.IsFalse(_parentNode.IsActive, "Detached child should not be active");
    }

    [Test]
    public void GrandChild_GetsPowerThroughHierarchy()
    {
      // Setup hierarchy: PowerSource -> Parent -> Child -> GrandChild
      _powerSource.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode);
      _parentNode.SetTurnedOn(true);
      _parentNode.AttachChild(_childNode);
      _childNode.SetTurnedOn(true);
      _childNode.AttachChild(_grandChildNode);
      _grandChildNode.SetTurnedOn(true);

      Assert.IsTrue(_grandChildNode.IsActive, "Grand child should be active through hierarchy");

      // Turn off middle node
      _childNode.SetTurnedOn(false);

      Assert.IsFalse(_childNode.IsActive, "Child should not be active");
      Assert.IsFalse(_grandChildNode.HasPower, "Grand child should lose power");
      Assert.IsFalse(_grandChildNode.IsActive, "Grand child should not be active");
    }

    [Test]
    public void IsActive_RequiresBothPowerAndTurnedOn()
    {
      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode!);

      // Has power but not turned on
      Assert.IsTrue(_parentNode.HasPower, "Child should have power");
      Assert.IsFalse(_parentNode.IsTurnedOn, "Child should not be turned on");
      Assert.IsFalse(_parentNode.IsActive, "Child should not be active without being turned on");

      // Turned on and has power
      _parentNode.SetTurnedOn(true);
      Assert.IsTrue(_parentNode.IsActive,
        "Child should be active when both turned on and has power");

      // Turned on but no power
      _powerSource.SetTurnedOn(false);
      Assert.IsTrue(_parentNode.IsTurnedOn, "Child should still be turned on");
      Assert.IsFalse(_parentNode.HasPower, "Child should not have power");
      Assert.IsFalse(_parentNode.IsActive, "Child should not be active without power");
    }

    [Test]
    public void MultipleChildren_AllReceivePower()
    {
      var child1 = new PoweredNode();
      var child2 = new PoweredNode();
      var child3 = new PoweredNode();

      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(child1);
      _powerSource.AttachChild(child2);
      _powerSource.AttachChild(child3);

      child1.SetTurnedOn(true);
      child2.SetTurnedOn(true);
      child3.SetTurnedOn(true);

      Assert.IsTrue(child1.IsActive, "First child should be active");
      Assert.IsTrue(child2.IsActive, "Second child should be active");
      Assert.IsTrue(child3.IsActive, "Third child should be active");

      // Turn off power source
      _powerSource.SetTurnedOn(false);

      Assert.IsFalse(child1.IsActive, "First child should lose power");
      Assert.IsFalse(child2.IsActive, "Second child should lose power");
      Assert.IsFalse(child3.IsActive, "Third child should lose power");
    }

    [Test]
    public void Node_IsTurnedOffByDefault()
    {
      Assert.IsFalse(_powerSource!.IsTurnedOn, "Node should be turned off by default");
      Assert.IsFalse(_parentNode!.IsTurnedOn, "Node should be turned off by default");
    }

    [Test]
    public void PowerSource_DoesntHavePowerByDefault()
    {
      Assert.IsFalse(_powerSource.HasPower, "Power source shouldn't have power by default");
    }

    [Test]
    public void PowerSource_IsActiveWhenTurnedOn()
    {
      _powerSource!.SetTurnedOn(true);

      Assert.IsTrue(_powerSource.IsTurnedOn, "Power source should be turned on");
      Assert.IsTrue(_powerSource.HasPower, "Power source should have power");
      Assert.IsTrue(_powerSource.IsActive, "Power source should be active when turned on");
    }

    [Test]
    public void RefreshPowerState_PropagatesDownHierarchy()
    {
      // Setup deep hierarchy
      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode!);
      _parentNode.SetTurnedOn(true);
      _parentNode.AttachChild(_childNode!);
      _childNode.SetTurnedOn(true);
      _childNode.AttachChild(_grandChildNode!);
      _grandChildNode.SetTurnedOn(true);

      Assert.IsTrue(_grandChildNode.IsActive, "Grand child should be active");

      // Manually call RefreshPowerState on power source after turning it off
      _powerSource.SetTurnedOn(false);

      Assert.IsFalse(_powerSource.IsActive, "Power source should not be active");
      Assert.IsFalse(_parentNode.HasPower, "Parent should lose power");
      Assert.IsFalse(_childNode.HasPower, "Child should lose power");
      Assert.IsFalse(_grandChildNode.HasPower, "Grand child should lose power");
      Assert.IsFalse(_grandChildNode.IsActive, "Grand child should not be active");
    }

    [Test]
    public void RegularNode_HasNoPowerByDefault()
    {
      Assert.IsFalse(_parentNode!.HasPower, "Regular node should not have power by default");
      Assert.IsFalse(_parentNode.IsActive, "Regular node should not be active by default");
    }

    [Test]
    public void RegularNode_IsNotActiveWithoutParent()
    {
      _parentNode!.SetTurnedOn(true);

      Assert.IsTrue(_parentNode.IsTurnedOn, "Node should be turned on");
      Assert.IsFalse(_parentNode.HasPower, "Node should not have power without parent");
      Assert.IsFalse(_parentNode.IsActive, "Node should not be active without power");
    }

    [Test]
    public void SetParent_ToNull_RemovesPower()
    {
      // Setup: Active child
      _powerSource!.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode!);
      _parentNode.SetTurnedOn(true);

      Assert.IsTrue(_parentNode.IsActive, "Child should be active initially");

      // Remove parent
      _parentNode.SetParent(null);

      Assert.IsFalse(_parentNode.HasPower, "Child should lose power when parent is removed");
      Assert.IsFalse(_parentNode.IsActive, "Child should not be active without parent");
    }

    [Test]
    public void SetParent_UpdatesPowerState()
    {
      // Setup: Two power sources
      var powerSource2 = new PoweredNode(true);
      powerSource2.SetTurnedOn(true);

      _powerSource!.SetTurnedOn(false); // First power source is off
      _parentNode!.SetTurnedOn(true);

      // Set parent to inactive power source
      _parentNode.SetParent(_powerSource);
      Assert.IsFalse(_parentNode.HasPower, "Child should not have power from inactive parent");

      // Change parent to active power source
      _parentNode.SetParent(powerSource2);
      Assert.IsTrue(_parentNode.HasPower, "Child should have power from active parent");
      Assert.IsTrue(_parentNode.IsActive, "Child should be active");
    }

    [SetUp]
    public void Setup()
    {
      _powerSource    = new PoweredNode(true);
      _parentNode     = new PoweredNode();
      _childNode      = new PoweredNode();
      _grandChildNode = new PoweredNode();
    }

    [TearDown]
    public void Teardown()
    {
      _powerSource    = null;
      _parentNode     = null;
      _childNode      = null;
      _grandChildNode = null;
    }
  }
}