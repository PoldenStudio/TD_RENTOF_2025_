using UnityEngine;
using jp.kshoji.unity.midi;

public class MidiGenerator : MonoBehaviour
{
    public string targetDeviceId = "M65535-P65535-I1"; // ��� ID ������ ������������ ����� (���� �����)
    [Range(0, 15)]
    public int midiChannel = 0;
    public int pitchCcFunction = 1;
    public float pitchValue = 0.5f; // ������ �������� 0-1
    public int cutoffCcFunction = 74;
    public float cutoffValue = 1f;   // ������ �������� 0-1
    public float valueChangeSpeed = 1f;

    void Update()
    {
        // ������: ������ ������ Pitch � Cutoff ��������
        pitchValue = Mathf.Clamp01(pitchValue + Mathf.Sin(Time.time) * Time.deltaTime * valueChangeSpeed);
        cutoffValue = Mathf.Clamp01(cutoffValue - Mathf.Cos(Time.time * 0.5f) * Time.deltaTime * valueChangeSpeed * 0.5f);

        // ���������� MIDI Control Change ��������� �� SoundManager (����� MIDI ������)
        MidiManager.Instance.SendMidiControlChange(targetDeviceId, 0, midiChannel, pitchCcFunction, Mathf.RoundToInt(pitchValue * 127f));
        MidiManager.Instance.SendMidiControlChange(targetDeviceId, 0, midiChannel, cutoffCcFunction, Mathf.RoundToInt(cutoffValue * 127f));
    }
}