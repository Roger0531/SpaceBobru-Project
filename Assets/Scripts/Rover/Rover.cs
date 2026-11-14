using System;
using UnityEngine;

public class Rover : MonoBehaviour
{
    public static Rover Instance { get; private set; }

    public Worker Driver { get; private set; }
    public bool IsFree => Driver == null;

    public event Action<Worker> OnBoarded;
    public event Action<Worker> OnReturned;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Board(Worker worker)
    {
        Driver = worker;
        OnBoarded?.Invoke(worker);
    }

    public void Leave()
    {
        var worker = Driver;
        Driver = null;
        OnReturned?.Invoke(worker);
    }
}