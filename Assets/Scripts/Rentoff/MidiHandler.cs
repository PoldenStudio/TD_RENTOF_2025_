/*using UnityEngine;
using System.Collections.Generic; // Для списка сообщений
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

// Добавляем интерфейсы для событий, которые хотим получать.
// IMidiNoteOnEventHandler - для сообщений Note On (нажатие ноты)
// IMidiNoteOffEventHandler - для сообщений Note Off (отпускание ноты)
// IMidiDeviceEventHandler - для отслеживания подключения/отключения MIDI-устройств
public class MidiHandler : MonoBehaviour, IMidiNoteOnEventHandler, IMidiNoteOffEventHandler, IMidiDeviceEventHandler
{
    // Список для отображения полученных сообщений (для примера)
    private List<string> receivedMidiMessages = new List<string>();
    private Vector2 scrollPosition;

    // Ссылка на твой AudioSource, которым ты хочешь управлять
    public AudioSource targetAudioSource;
    public AudioClip noteSound; // Звук, который будем проигрывать

    void Awake()
    {
        Debug.Log("MidiHandler: Awake - Initializing MIDI");

        // Регистрируем этот GameObject для получения MIDI-событий.
        // Он должен реализовывать нужные интерфейсы (IMidi...EventHandler).
        MidiManager.Instance.RegisterEventHandleObject(gameObject);

        // Инициализируем MIDI-менеджер.
        // Колбэк вызывается после завершения инициализации.
        MidiManager.Instance.InitializeMidi(() =>
        {
            Debug.Log("MidiHandler: MIDI Initialized!");

            // После инициализации можно начать сканирование Bluetooth MIDI устройств (если нужно)
            // Это актуально для Android, iOS, WebGL в runtime.
#if (UNITY_ANDROID || UNITY_IOS || UNITY_WEBGL) && !UNITY_EDITOR
            Debug.Log("MidiHandler: Starting Bluetooth MIDI device scan.");
            MidiManager.Instance.StartScanBluetoothMidiDevices(0);
#endif
        });

        // Можно запустить RTP-MIDI сервер (сетевой MIDI), если нужно
#if !UNITY_IOS && !UNITY_WEBGL
        // MidiManager.Instance.StartRtpMidiServer("MyUnityMidiSession", 5004);
#endif

        if (targetAudioSource == null)
        {
            Debug.LogError("MidiHandler: Target AudioSource не назначен в инспекторе!");
        }
        if (noteSound == null)
        {
            Debug.LogError("MidiHandler: Note Sound (AudioClip) не назначен в инспекторе!");
        }
    }

    void OnDestroy()
    {
        Debug.Log("MidiHandler: OnDestroy - Terminating MIDI");
        // Важно остановить MIDI-менеджер при завершении работы
        MidiManager.Instance.TerminateMidi();
    }

    // --- Реализация интерфейсов для MIDI-событий ---

    // Вызывается при подключении MIDI-устройства ввода
    public void OnMidiInputDeviceAttached(string deviceId)
    {
        string message = $"MIDI Input Device Attached: ID={deviceId}, Name={MidiManager.Instance.GetDeviceName(deviceId)}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // Вызывается при подключении MIDI-устройства вывода
    public void OnMidiOutputDeviceAttached(string deviceId)
    {
        string message = $"MIDI Output Device Attached: ID={deviceId}, Name={MidiManager.Instance.GetDeviceName(deviceId)}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // Вызывается при отключении MIDI-устройства ввода
    public void OnMidiInputDeviceDetached(string deviceId)
    {
        string message = $"MIDI Input Device Detached: ID={deviceId}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // Вызывается при отключении MIDI-устройства вывода
    public void OnMidiOutputDeviceDetached(string deviceId)
    {
        string message = $"MIDI Output Device Detached: ID={deviceId}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);
    }

    // Вызывается при получении сообщения Note On
    public void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
    {
        string message = $"Note ON: Device={deviceId}, Channel={channel}, Note={note}, Velocity={velocity}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);

        // --- ВОТ ЗДЕСЬ ТЫ МОЖЕШЬ УПРАВЛЯТЬ AUDIO SOURCE ---
        if (targetAudioSource != null && noteSound != null && velocity > 0)
        {
            // Преобразуем номер MIDI-ноты в высоту тона для AudioSource.
            // 60 - это средняя До (C4). Каждая единица - полутон.
            // Pitch 1.0 - нормальная высота.
            float basePitch = 1.0f;
            float semitoneOffset = note - 60;
            float pitchMultiplier = Mathf.Pow(2f, semitoneOffset / 12.0f);

            targetAudioSource.pitch = basePitch * pitchMultiplier;

            // Используем velocity для громкости (0-127 -> 0-1)
            targetAudioSource.volume = (float)velocity / 127.0f;

            // Проигрываем звук
            // targetAudioSource.PlayOneShot(noteSound); // Хорошо для коротких звуков
            targetAudioSource.clip = noteSound;
            targetAudioSource.Play(); // Если хочешь, чтобы нота играла до Note Off

            Debug.Log($"Playing sound: Note={note}, Pitch={targetAudioSource.pitch}, Volume={targetAudioSource.volume}");
        }
        else if (velocity == 0)
        {
            // Некоторые устройства шлют Note On с velocity=0 вместо Note Off
            OnMidiNoteOff(deviceId, group, channel, note, velocity);
        }
    }

    // Вызывается при получении сообщения Note Off
    public void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
    {
        string message = $"Note OFF: Device={deviceId}, Channel={channel}, Note={note}, Velocity={velocity}";
        Debug.Log(message);
        receivedMidiMessages.Add(message);

        // --- ЗДЕСЬ МОЖНО ОСТАНОВИТЬ ЗВУК ---
        if (targetAudioSource != null)
        {
            // Простой способ - остановить. Если ты играешь разные ноты одновременно,
            // нужно более сложное управление (например, полифония).
            // Этот код остановит любой звук, если нота совпадает (или просто любой, если pitch не меняли).
            // Для точного соответствия нужно сравнивать проигрываемую ноту.

            // Простейший вариант: просто остановить
            // targetAudioSource.Stop();
            // Debug.Log($"Stopping sound for Note={note}");

            // Можно добавить логику, чтобы останавливать только если играла именно эта нота,
            // но это требует хранения информации о том, какая нота сейчас звучит.
        }
    }

    // --- Другие интерфейсы для разных MIDI-событий ---
    // public void OnMidiPitchBend(string deviceId, int group, int channel, int bend) { ... }
    // public void OnMidiControlChange(string deviceId, int group, int channel, int function, int value) { ... }
    // ... и так далее, см. IMidiEventHandler.cs в плагине

    // --- Отображение сообщений для отладки ---
    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 400, 300), GUI.skin.box);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);
        GUILayout.Label("Received MIDI Messages:");
        if (receivedMidiMessages.Count > 20) // Ограничим историю
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