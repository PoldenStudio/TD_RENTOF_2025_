/*using UnityEngine;
using System.Collections.Generic; // ��� ������ ���������
using jp.kshoji.unity.midi.sample;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
#if !UNITY_IOS && !UNITY_WEBGL
using System.Net;
using System.Net.Sockets;
#endif
using jp.kshoji.midisystem;
using UnityEngine;
using UnityEngine.Networking;

// ��������� ���������� ��� �������, ������� ����� ��������.
// IMidiNoteOnEventHandler - ��� ��������� Note On (������� ����)
// IMidiNoteOffEventHandler - ��� ��������� Note Off (���������� ����)
// IMidiDeviceEventHandler - ��� ������������ �����������/���������� MIDI-���������
public class MidiHandler : MonoBehaviour, IMidiNoteOnEventHandler, IMidiNoteOffEventHandler, IMidiDeviceEventHandler
{
    // ������ ��� ����������� ���������� ��������� (��� �������)
    private List<string> receivedMidiMessages = new List<string>();
    private Vector2 scrollPosition;

    // ������ �� ���� AudioSource, ������� �� ������ ���������
    public AudioSource targetAudioSource;
    public AudioClip noteSound; // ����, ������� ����� �����������

    void Awake()
    {
        Debug.Log("MidiHandler: Awake - Initializing MIDI");

        // ������������ ���� GameObject ��� ��������� MIDI-�������.
        // �� ������ ������������� ������ ���������� (IMidi...EventHandler).
        MidiManager.Instance.RegisterEventHandleObject(gameObject);

        // �������������� MIDI-��������.
        // ������ ���������� ����� ���������� �������������.
        MidiManager.Instance.InitializeMidi(() =>
        {
            Debug.Log("MidiHandler: MIDI Initialized!");

            // ����� ������������� ����� ������ ������������ Bluetooth MIDI ��������� (���� �����)
            // ��� ��������� ��� Android, iOS, WebGL � runtime.
#if (UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            Debug.Log("MidiHandler: Starting Bluetooth MIDI device scan.");
            MidiManager.Instance.StartScanBluetoothMidiDevices(0);
#endif
        });

        // ����� ��������� RTP-MIDI ������ (������� MIDI), ���� �����
#if !UNITY_IOS && !UNITY_WEBGL
        // MidiManager.Instance.StartRtpMidiServer("MyUnityMidiSession", 5004);
#endif

        if (targetAudioSource == null)
        {
            Debug.LogError("MidiHandler: Target AudioSource �� �������� � ����������!");
        }
        if (noteSound == null)
        {
            Debug.LogError("MidiHandler: Note Sound (AudioClip) �� �������� � ����������!");
        }
    }

    void OnDestroy()
    {
        Debug.Log("MidiHandler: OnDestroy - Terminating MIDI");
        // ����� ���������� MIDI-�������� ��� ���������� ������
        MidiManager.Instance.TerminateMidi();
    }

    // --- ���������� ����������� ��� MIDI-������� ---

    // ���������� ��� ����������� MIDI-���������� �����
    public void OnMidiInputDeviceAttached(string deviceId)
    {
        string message = $"MIDI Input Device Attached: ID={deviceId}, Name={MidiManager.Instance.GetDeviceName(deviceId)}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // ���������� ��� ����������� MIDI-���������� ������
    public void OnMidiOutputDeviceAttached(string deviceId)
    {
        string message = $"MIDI Output Device Attached: ID={deviceId}, Name={MidiManager.Instance.GetDeviceName(deviceId)}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // ���������� ��� ���������� MIDI-���������� �����
    public void OnMidiInputDeviceDetached(string deviceId)
    {
        string message = $"MIDI Input Device Detached: ID={deviceId}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // ���������� ��� ���������� MIDI-���������� ������
    public void OnMidiOutputDeviceDetached(string deviceId)
    {
        string message = $"MIDI Output Device Detached: ID={deviceId}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // ���������� ��� ��������� ��������� Note On
    public void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
    {
        string message = $"Note ON: Device={deviceId}, Channel={channel}, Note={note}, Velocity={velocity}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);

        // --- ��� ����� �� ������ ��������� AUDIO SOURCE ---
        if (targetAudioSource != null && noteSound != null && velocity > 0)
        {
            // ����������� ����� MIDI-���� � ������ ���� ��� AudioSource.
            // 60 - ��� ������� �� (C4). ������ ������� - �������.
            // Pitch 1.0 - ���������� ������.
            float basePitch = 1.0f;
            float semitoneOffset = note - 60;
            float pitchMultiplier = Mathf.Pow(2f, semitoneOffset / 12.0f);

            targetAudioSource.pitch = basePitch * pitchMultiplier;

            // ���������� velocity ��� ��������� (0-127 -> 0-1)
            targetAudioSource.volume = (float)velocity / 127.0f;

            // ����������� ����
            // targetAudioSource.PlayOneShot(noteSound); // ������ ��� �������� ������
            targetAudioSource.clip = noteSound;
            targetAudioSource.Play(); // ���� ������, ����� ���� ������ �� Note Off

            Debug.Log($"Playing sound: Note={note}, Pitch={targetAudioSource.pitch}, Volume={targetAudioSource.volume}");
        }
        else if (velocity == 0)
        {
            // ��������� ���������� ���� Note On � velocity=0 ������ Note Off
            OnMidiNoteOff(deviceId, group, channel, note, velocity);
        }
    }

    // ���������� ��� ��������� ��������� Note Off
    public void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
    {
        string message = $"Note OFF: Device={deviceId}, Channel={channel}, Note={note}, Velocity={velocity}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);

        // --- ����� ����� ���������� ���� ---
        if (targetAudioSource != null)
        {
            // ������� ������ - ����������. ���� �� ������� ������ ���� ������������,
            // ����� ����� ������� ���������� (��������, ���������).
            // ���� ��� ��������� ����� ����, ���� ���� ��������� (��� ������ �����, ���� pitch �� ������).
            // ��� ������� ������������ ����� ���������� ������������� ����.

            // ���������� �������: ������ ����������
            // targetAudioSource.Stop();
            // Debug.Log($"Stopping sound for Note={note}");

            // ����� �������� ������, ����� ������������� ������ ���� ������ ������ ��� ����,
            // �� ��� ������� �������� ���������� � ���, ����� ���� ������ ������.
        }
    }

    // --- ������ ���������� ��� ������ MIDI-������� ---
    // public void OnMidiPitchBend(string deviceId, int group, int channel, int bend) { ... }
    // public void OnMidiControlChange(string deviceId, int group, int channel, int function, int value) { ... }
    // ... � ��� �����, ��. IMidiEventHandler.cs � �������

    // --- ����������� ��������� ��� ������� ---
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 300), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("Received MIDI Messages:");
        if (receivedMidiMessages.Count > 20) // ��������� �������
        {
            receivedMidiMessages.RemoveAt(0);
        }
        foreach (string msg in receivedMidiMessages)
        {
            GUILayout.Label(msg);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}*/