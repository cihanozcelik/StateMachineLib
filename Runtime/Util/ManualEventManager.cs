using System;
using UnityEngine;

public class ManualEventManager : MonoBehaviour
{
  public event Action OnUpdate;
  public event Action OnLateUpdate;
  public event Action OnFixedUpdate;

  static ManualEventManager _instance;
  public static ManualEventManager Instance {
    get
    {
      if (_instance == null)
      {
        _instance = new GameObject("ManualEventManager").AddComponent<ManualEventManager>();
      }
      return _instance;
    }
  }
            
  void Awake()
  {
    DontDestroyOnLoad(this);
  }
            
  void Update()
  {
    OnUpdate?.Invoke();
  }
  void LateUpdate()
  {
    OnLateUpdate?.Invoke();
  }
  void FixedUpdate()
  {
    OnFixedUpdate?.Invoke();
  }
}