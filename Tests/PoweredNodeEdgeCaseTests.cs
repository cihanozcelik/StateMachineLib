#nullable enable
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace Nopnag.StateMachineLib.Tests
{
  /// <summary>
  /// Extra “edge” tests that complement PoweredNodeTests.
  /// </summary>
  public class PoweredNodeEdgeCaseTests
  {
    PoweredNode _childNode   = null!;
    PoweredNode _grandChild  = null!;
    PoweredNode _parentNode  = null!;
    PoweredNode _powerSource = null!;

    /* ------------------------------------------------------------------ */
    /* 2. Attach a node that was already turned on.                       */
    /* ------------------------------------------------------------------ */
    [Test]
    public void AlreadyTurnedOnChild_BecomesActiveAfterAttach()
    {
      _childNode.SetTurnedOn(true); // on but no parent
      _powerSource.SetTurnedOn(true);

      Assert.IsFalse(_childNode.IsActive, "Without a parent the node cannot be active");

      _powerSource.AttachChild(_childNode); // now attach
      Assert.IsTrue(_childNode.IsActive, "Node should activate once power is supplied");
    }

    /* ------------------------------------------------------------------ */
    /* 4. Attaching the same child twice must not duplicate it in _children. */
    /* ------------------------------------------------------------------ */
    [Test]
    public void AttachChild_Twice_DoesNotDuplicateInParentList()
    {
      _powerSource.AttachChild(_childNode);
      _powerSource.AttachChild(_childNode); // duplicate call

      // Inspect the private _children list via reflection
      var field = typeof(PoweredNode)
        .GetField("_children", BindingFlags.NonPublic | BindingFlags.Instance)!;
      var list = (ICollection)field.GetValue(_powerSource)!;

      Assert.AreEqual(1, list.Count, "_children should contain exactly one entry");
    }

    /* ------------------------------------------------------------------ */
    /* 3. Reactivating a middle node should revive grandchildren.         */
    /* ------------------------------------------------------------------ */
    [Test]
    public void ReactivatingMiddleNode_RestoresGrandChild()
    {
      // PowerSource → Parent → Child → GrandChild
      _powerSource.SetTurnedOn(true);
      _powerSource.AttachChild(_parentNode);
      _parentNode.SetTurnedOn(true);

      _parentNode.AttachChild(_childNode);
      _childNode.SetTurnedOn(true);

      _childNode.AttachChild(_grandChild);
      _grandChild.SetTurnedOn(true);

      Assert.IsTrue(_grandChild.IsActive, "Grandchild should start active");

      _childNode.SetTurnedOn(false); // deactivate the middle node
      Assert.IsFalse(_grandChild.IsActive, "Grandchild should lose power");

      _childNode.SetTurnedOn(true); // reactivate
      Assert.IsTrue(_grandChild.IsActive, "Grandchild should regain power");
    }

    /* ------------------------------------------------------------------ */
    /* 5. A node cannot be its own parent (prevents cycles / recursion).  */
    /* ------------------------------------------------------------------ */
    [Test]
    public void SetParent_ToSelf_ThrowsArgumentException()
    {
      Assert.Throws<ArgumentException>(
        () => _parentNode.SetParent(_parentNode),
        "A node cannot be set as its own parent");
    }

    /* ===== common setup / teardown ===== */
    [SetUp]
    public void Setup()
    {
      _powerSource = new PoweredNode(true);
      _parentNode  = new PoweredNode();
      _childNode   = new PoweredNode();
      _grandChild  = new PoweredNode();
    }

    [TearDown]
    public void Teardown()
    {
      _powerSource = null!;
      _parentNode  = null!;
      _childNode   = null!;
      _grandChild  = null!;
    }

    /* ------------------------------------------------------------------ */
    /* 1. Toggle – turning the power source OFF then ON should reactivate */
    /*    its descendants.                                                */
    /* ------------------------------------------------------------------ */
    [Test]
    public void TogglePowerSource_RestoresChildrenActivity()
    {
      _powerSource.SetTurnedOn(true);
      _powerSource.AttachChild(_childNode);
      _childNode.SetTurnedOn(true);

      Assert.IsTrue(_childNode.IsActive, "Child should start active");

      _powerSource.SetTurnedOn(false); // power off
      Assert.IsFalse(_childNode.IsActive, "Child should become inactive");

      _powerSource.SetTurnedOn(true); // power on
      Assert.IsTrue(_childNode.IsActive, "Child should become active again");
    }
  }
}