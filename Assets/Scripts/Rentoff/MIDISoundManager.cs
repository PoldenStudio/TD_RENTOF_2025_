using UnityEngine;
using jp.kshoji.unity.midi;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
[DisallowMultipleComponent]
public class MIDISoundManager : MonoBehaviour
{
    public enum NotePatternType { Random, Ascending, Descending }

    [Header("MIDI Settings")]
    public string loopMidiDeviceId;
    [Range(0, 15)] public int midiChannel = 0;

    [Header("RTP-MIDI Settings")]
    public int rtpMidiPort = 5004;

    [Header("Speed Parameters")]
    public float neutralSpeed = 1f;
    public float activationThreshold = 0.05f;
    public bool muteWhenNeutral = true;

    [Header("Note Parameters")]
    public int baseNote = 60;
    public int noteRange = 24;
    public float noteDuration = 0.3f;
    public float minInterval = 0.05f;
    public float maxInterval = 0.5f;
    public int maxSimultaneousNotes = 3;
    public NotePatternType notePattern = NotePatternType.Random;

    [Header("Velocity")]
    public int minVelocity = 30;
    public int maxVelocity = 127;
    public bool useVelocityCurve = true;
    public AnimationCurve velocityCurve = AnimationCurve.Linear(0, 0.3f, 5, 1f);

    [Header("Pitch Mapping")]
    public bool useNoteCurve = false;
    public AnimationCurve noteCurve = AnimationCurve.Linear(0, 0f, 5, 1f);

    [Header("CC Control")]
    public bool sendCC = false;
    public int ccNumber = 74;
    public float ccSendInterval = 0.5f;

    private float currentSpeed = 1f;
    private float lastNoteTime = 0f;
    private float lastCCSendTime = 0f;
    private int ascendingNoteCounter = 0;
    private int descendingNoteCounter = 0;
    private bool isMidiReady = false;

    private Queue<int> activeNotes = new();





    private void Awake()
    {
        MidiManager.Instance.RegisterEventHandleObject(gameObject);

        foreach (var midiPlugin in MidiManager.Instance.midiPlugins)
        {
            if (midiPlugin is RtpMidiPlugin rtpMidiPlugin)
            {
                Debug.Log($"[MIDI] Setting RtpMidiPlugin port to: {rtpMidiPort}");
                rtpMidiPlugin.SetPort(rtpMidiPort);
            }
        }

        MidiManager.Instance.InitializeMidi(() =>
        {
            Debug.Log("[MIDI] Initialized.");
            isMidiReady = true;

            Debug.Log("[MIDI] Available Devices:");
            foreach (var device in MidiManager.Instance.DeviceIdSet)
            {
                Debug.Log($"[MIDI] Found Device: {device}");
            }

            if (string.IsNullOrEmpty(loopMidiDeviceId))
            {
                Debug.LogError("[MIDI] loopMidiDeviceId is not set. Please assign a valid MIDI device.");
                return;
            }

            if (!MidiManager.Instance.DeviceIdSet.Contains(loopMidiDeviceId))
            {
                Debug.LogError($"[MIDI] Device '{loopMidiDeviceId}' not found in available devices!");
            }
            else
            {
                Debug.Log($"[MIDI] Successfully detected device: {loopMidiDeviceId}");
            }
        });
    }

    private void OnDestroy()
    {
        MidiManager.Instance.TerminateMidi();
    }


    private void FixedUpdate()
    {
        if (!isMidiReady || string.IsNullOrEmpty(loopMidiDeviceId)) return;

        float deltaTime = Time.fixedDeltaTime;
        float interval = Mathf.Lerp(minInterval, maxInterval, 1f / Mathf.Clamp(currentSpeed, 0.5f, 5f));
        bool isActive = Mathf.Abs(currentSpeed - neutralSpeed) > activationThreshold;

        if (!isActive && muteWhenNeutral) return;

        lastNoteTime += deltaTime;
        lastCCSendTime += deltaTime;

        if (isActive && lastNoteTime >= interval)
        {
            GenerateNote();
            lastNoteTime = 0f;
        }

        if (sendCC && lastCCSendTime >= ccSendInterval)
        {
            SendCC();
            lastCCSendTime = 0f;
        }
    }

    public void UpdateSynthParameters(float newSpeed)
    {
        currentSpeed = newSpeed;
    }

    private void GenerateNote()
    {
        if (string.IsNullOrEmpty(loopMidiDeviceId))
        {
            Debug.LogError("[MIDI] loopMidiDeviceId is empty or null!");
            return;
        }

        int note = SelectNote();
        int velocity = GetVelocityFromSpeed();

        Debug.Log($"[MIDI] Sending NoteOn: Device={loopMidiDeviceId}, Channel={midiChannel}, Note={note}, Velocity={velocity}");
        MidiManager.Instance.SendMidiNoteOn(loopMidiDeviceId, 0, midiChannel, note, velocity);
        activeNotes.Enqueue(note);

        if (activeNotes.Count > maxSimultaneousNotes)
        {
            int oldestNote = activeNotes.Dequeue();
            Debug.Log($"[MIDI] Sending NoteOff: Device={loopMidiDeviceId}, Channel={midiChannel}, Note={oldestNote}");
            MidiManager.Instance.SendMidiNoteOff(loopMidiDeviceId, 0, midiChannel, oldestNote, 0);
        }

        StartCoroutine(SendNoteOffDelayed(note, noteDuration));
    }

    private IEnumerator SendNoteOffDelayed(int note, float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log($"[MIDI] Sending NoteOff (Delayed): Device={loopMidiDeviceId}, Channel={midiChannel}, Note={note}");
        MidiManager.Instance.SendMidiNoteOff(loopMidiDeviceId, 0, midiChannel, note, 0);
    }

    private int SelectNote()
    {
        int range = Mathf.Clamp((int)(currentSpeed * noteRange), 4, noteRange);
        int result = baseNote;

        switch (notePattern)
        {
            case NotePatternType.Random:
                result += Random.Range(-range / 2, range / 2);
                break;
            case NotePatternType.Ascending:
                result += (ascendingNoteCounter++ % range);
                break;
            case NotePatternType.Descending:
                result += range - (descendingNoteCounter++ % range);
                break;
        }

        if (useNoteCurve)
        {
            float t = Mathf.Clamp01(noteCurve.Evaluate(currentSpeed));
            result = baseNote + Mathf.RoundToInt(t * range);
        }

        return Mathf.Clamp(result, 0, 127);
    }

    private int GetVelocityFromSpeed()
    {
        if (useVelocityCurve)
        {
            float t = Mathf.Clamp01(velocityCurve.Evaluate(currentSpeed));
            return Mathf.Clamp(Mathf.RoundToInt(t * maxVelocity), minVelocity, maxVelocity);
        }

        return Mathf.Clamp((int)(currentSpeed * maxVelocity), minVelocity, maxVelocity);
    }

    private void SendCC()
    {
        if (string.IsNullOrEmpty(loopMidiDeviceId))
        {
            Debug.LogError("[MIDI] loopMidiDeviceId is empty or null!");
            return;
        }

        int ccValue = Mathf.Clamp((int)(currentSpeed * 127f), 0, 127);
        Debug.Log($"[MIDI] Sending CC: Device={loopMidiDeviceId}, CC={ccNumber}, Value={ccValue}");
        MidiManager.Instance.SendMidiControlChange(loopMidiDeviceId, 0, midiChannel, ccNumber, ccValue);
    }
}