using System;
using UnityEngine;

public abstract class InputReader : MonoBehaviour
{
    public event Action<bool[]> InputReceived;

    public abstract bool IsConnected();
    protected abstract void ReadInput();

    public float SampleRate { get; set; } = 0.02f;

    protected void OnInputReceived(bool[] panelStates)
    {
        InputReceived?.Invoke(panelStates);
    }

    protected virtual void FixedUpdate()
    {
        ReadInput();
    }
}