using System;
using System.Collections.Generic;

namespace Nopnag.StateMachineLib
{
  public class StateGraph
  {
    public StateUnit InitialUnit;
    public bool IsGraphActive;
    StateUnit _currentUnit;
    readonly List<StateUnit> _units = new();

    [Obsolete("Use CreateState() instead. If a name is needed, it should be managed by the user externally or via StateUnit properties if available.", false)]
    public StateUnit CreateUnit(string name)
    {
      var stateUnit = new StateUnit(name, this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
    }

    public StateUnit CreateState()
    {
      var stateUnit = new StateUnit(this);
      _units.Add(stateUnit);
      if (InitialUnit == null) InitialUnit = stateUnit;

      return stateUnit;
    }

    public void EnterGraph()
    {
      IsGraphActive = true;
      StartState(InitialUnit);
    }

    public void ExitGraph()
    {
      _currentUnit?.Exit();
      IsGraphActive = false;
    }

    public void FixedUpdateGraph()
    {
      _currentUnit?.FixedUpdate();
    }

    public string GetCurrentStateName()
    {
      return _currentUnit?.Name;
    }

    public bool IsUnitActive(StateUnit unit)
    {
      return IsGraphActive && _currentUnit == unit;
    }

    public void LateUpdateGraph()
    {
      _currentUnit?.LateUpdate();
    }

    public void StartState(StateUnit unit)
    {
      _currentUnit?.Exit();
      _currentUnit = unit;
      _currentUnit.Start();
    }

    public void UpdateGraph()
    {
      while (_currentUnit != null && !_currentUnit.Update())
      {
      }
    }

    // public void CheckTransitions()
    // {
    //   StateUnit transitionedUnit = currentUnit?.CheckTransitions();
    //   if(transitionedUnit != null)
    //   {
    //     StartState(transitionedUnit);
    //   }
    // }
  }
}