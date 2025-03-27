// Helper class to execute actions on the main thread
using System.Collections.Concurrent;
using System;
using UnityEngine;

public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;

    private readonly ConcurrentQueue<Action> _executionQueue = new ConcurrentQueue<Action>();

    public static UnityMainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject singleton = new GameObject();
                _instance = singleton.AddComponent<UnityMainThreadDispatcher>();
                singleton.name = "MainThreadDispatcher";
                DontDestroyOnLoad(singleton);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        while (_executionQueue.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    public static void Enqueue(Action action)
    {
        if (action == null) throw new ArgumentNullException("action");
        Instance._executionQueue.Enqueue(action);
    }
}