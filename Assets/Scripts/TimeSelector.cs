using Animations.AnimationDataBase;
using Animations;
using DemolitionStudios.DemolitionMedia;
using InitializationFramework;
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;

public class TimeSelector : MonoBehaviour
{
    [SerializeField]
    GlobalSettings gs;

    [SerializeField]
    AnimationCurve soundCurve;

    [SerializeField]
    IdleModeResetter resetter;

    [SerializeField]
    AudioSource src;

    [SerializeField]
    private Media _player;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private CanvasGroup canvasGroupLogo;

    [SerializeField]
    private AudioController audioController;

    [SerializeField]
    private float speed = 1f;

    [SerializeField]
    private float multiplier = 1f;

    [SerializeField]
    private float ticks_vid = 160072f;

    [SerializeField]
    private float ticks_fade = 23717f;

    [SerializeField]
    private AnimationKey[] animationComposition;

    private UnitySerialPort serial;

    private const int ticksPerSecond = 2000;

    private int? baseSerialVal;
    private int lastRawData = 0;
    private double _currentTime = 0;
    private bool inTransitionState = false;
    private bool isRecieving = false;
    private double _lastTm = 0;
    private int delta = 0;

    double lastFrameTime = 0;


    private void SetVolumeLevel(float v)
    {
        src.volume = soundCurve.Evaluate(v);
    }

    private void SendLight(float value)
    {
        if (animationComposition == null || animationComposition.Length == 0) return;

        int index = Mathf.RoundToInt(value * (animationComposition.Length - 1));
        for (int j = 0; j < animationComposition[index].data.Length; j++)
        {
            GenericDMXFixture.Instance.SendDataToLight(animationComposition[index].data[j], j);
        }
    }

    private double _CurrentTime
    {
        get => _currentTime;
        set
        {
            if (_player == null) return;

            float videoDuration = _player.DurationSeconds;
            if (videoDuration > 0)
            {
                _currentTime = (value + videoDuration) % videoDuration;

                _player.SeekToTime((float)_currentTime);

                SendLight((float)_currentTime / videoDuration);
                SetVolumeLevel((float)_currentTime / videoDuration);

                if (_currentTime > videoDuration - 0.1f && _currentTime < videoDuration)
                {
                    inTransitionState = true;
                    baseSerialVal = null;
                    src.Stop();
                    src.Play();
                }
                else
                {
                    inTransitionState = false;
                }
            }
        }
    }

    private void Start()
    {
        if (gs == null)
        {
            Debug.LogError("GlobalSettings is not assigned!");
            return;
        }

        string modeChan = (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode)
            ? gs.contentSettings.defaultModeChan
            : gs.contentSettings.spcModeChan;

        animationComposition = AnimationDB.Load(Path.Combine(Application.streamingAssetsPath, "Animations", modeChan)).Animation.ToArray();

        baseSerialVal = null;
        _CurrentTime = 0;
        serial = UnitySerialPort.Instance;
    }

    [Obsolete]
    private void Awake()
    {
        // Расширенные проверки
        if (_player == null)
        {
            _player = GetComponent<Media>();
        }

        if (_player == null)
        {
            Debug.LogError("No Media player found!");
            enabled = false;
            return;
        }

        if (gs == null)
        {
            gs = FindObjectOfType<GlobalSettings>();
        }

        if (gs == null)
        {
            Debug.LogError("No GlobalSettings found!");
            enabled = false;
            return;
        }

        Cursor.visible = false;

        _player.openOnStart = false;
        _player.playOnOpen = false;
        _player.Loops = 0;
        _player.PlaybackSpeed = 0;

        try
        {
            string movPath = (gs.generalSettings.activeMode == GlobalSettings.GeneralSettings.WorkingModes.DefaultMode)
                ? gs.contentSettings.defaultModeMovieName
                : gs.contentSettings.spcModeMovieName;

            string fullPath = Path.Combine(Application.streamingAssetsPath, movPath);

            if (!File.Exists(fullPath))
            {
                Debug.LogError($"Media file does not exist: {fullPath}");
                enabled = false;
                return;
            }

            void MediaHandler(Media mediaPlayer, MediaEvent.Type eventType, MediaError errorCode)
            {
                if (eventType == MediaEvent.Type.Opened)
                {
                    Debug.Log("Media opened successfully");

                    // Принудительно устанавливаем первый кадр
                    mediaPlayer.SeekToFrame(0);

                    // Если нужно зафиксировать первый кадр
                    mediaPlayer.Pause();
                }
                else if (eventType == MediaEvent.Type.OpenFailed)
                {
                    Debug.LogError($"Failed to open media. Error: {errorCode}");
                    enabled = false;
                }
            }

            _player.Events.AddListener(MediaHandler);
            _player.Open(fullPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error opening media: {ex.Message}");
            Debug.LogException(ex);
            enabled = false;
        }

        isRecieving = true;
    }
    private void FixedUpdate()
    {
        if (!isRecieving || serial == null) return;

        if (string.IsNullOrEmpty(serial.RawData))
        {
            Debug.Log("EMPTY");
            return;
        }

        try
        {
            baseSerialVal ??= Convert.ToInt32(serial.RawData.Split('\t')[0]);

            var d_ticks_fade = ticks_fade * multiplier;
            var d_ticks_vid = ticks_vid * multiplier;
            var d_ticksPerSecond = (int)(ticksPerSecond * multiplier);

            int rawData = baseSerialVal != null
                ? Convert.ToInt32(serial.RawData.Split('\t')[0])
                : lastRawData;

            delta = baseSerialVal != null
                ? (rawData - baseSerialVal.Value)
                : 0;

            int accelVal = baseSerialVal != null
                ? Convert.ToInt32(serial.RawData.Split('\t')[2])
                : 0;

            double tm = delta / (double)d_ticksPerSecond;
            double ptchD = (double)accelVal / (double)d_ticksPerSecond;

            float lerpFactor = Mathf.Lerp(0.25f, 0.75f, Mathf.Clamp01(Mathf.Abs((float)ptchD)));

            float lerpSpeed = 0.25f;
            _CurrentTime = Mathf.Lerp((float)_CurrentTime, (float)tm, lerpSpeed * Time.deltaTime);

            float minPitch = 0.5f;
            float maxPitch = 2.0f;
            src.pitch = Mathf.Clamp((float)ptchD, minPitch, maxPitch);

            int dataDelta = Math.Abs(rawData - lastRawData);

            if (dataDelta != 0 && resetter != null)
            {
                resetter.lastActivityTime = Time.time;
            }

            _lastTm = tm;
            lastRawData = rawData;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in FixedUpdate: {ex.Message}");
        }
    }

    public void ResetValuesAndStopRecieving()
    {
        Debug.Log("ResetValuesAndStopRecieving:!!!");
        isRecieving = false;
        baseSerialVal = null;
        delta = 0;
    }



    double Lerp(double a, double b, double parm)
    {
        double nParm = (parm < 0.0) ? 0.0 : parm;

        nParm = (parm > 1.0) ? 1.0 : parm;

        return a * nParm + b * (1.0 - nParm);
    }


    /*IEnumerator SampleSetCurves() // Удалить
    {
        double duration = _player.Info.GetDuration();


        while (isRecieving)
        {

            Action<SACNAnimation> sendData = (anm) =>
            {
                float cr = anm.crv.Evaluate((float)(_CurrentTime / duration));

                ushort tgVal = (ushort)Mathf.Lerp(0, ushort.MaxValue, cr);

                byte bl = BitConverter.GetBytes(tgVal)[0];
                byte bb = BitConverter.GetBytes(tgVal)[1];


                sACN.data[anm.sACN_index] = bb;
                sACN.data[anm.sACN_index+1] = bl;

            };

            activeSet.entries.ForEach(sendData);

            yield return transmittionDelay;
        }
    }*/





    //    private void LateUpdate()
    //    {
    //        var d_ticks_fade = ticks_fade * multiplier;
    //        var d_ticks_vid = ticks_vid * multiplier;
    //        var d_ticksPerSecond = (int)(ticksPerSecond * multiplier);


    //        /*
    //        if (delta > d_ticks_vid + d_ticks_fade * 2 || delta < -(d_ticks_vid + d_ticks_fade * 2))
    //        {
    //            baseSerialVal = null;
    //        }
    //        */
    //    //Debug.Log("RAW: " + serial.RawData );

    //    //

    //            int rawData = Convert.ToInt32(serial.RawData.Split('\t')[0]);


    //            delta = (baseSerialVal != null) ? (rawData - baseSerialVal.Value) : 0;
    //            int accelVal = Convert.ToInt32(serial.RawData.Split('\t')[2]);

    //        print("ACCEL VAL/: " + accelVal);


    //            double tm = delta / (double)d_ticksPerSecond;

    //        //      

    //        //delta > 0 ? ((double)delta - d_ticks_fade) / (double)d_ticksPerSecond :
    //        //((double)delta + d_ticks_fade) / (double)d_ticksPerSecond;
    //        /*
    //        if (delta < d_ticks_fade && delta >= 0)
    //        {
    //            var value = delta / d_ticks_fade;
    //            DoFadeGroups(value);
    //            _player.Control.Seek(0);
    //            src.time = 0;
    //            src.pitch = 0;
    //            return;
    //        }

    //        else if(delta > d_ticks_vid + d_ticks_fade && delta > 0)
    //        {
    //            var value = ((d_ticks_vid + d_ticks_fade * 2) - delta) / d_ticks_fade;
    //            DoFadeGroups(value);
    //            Debug.Log($"Value is {value}");
    //            src.time = 0;
    //            src.pitch = 0;
    //            return;
    //        }

    //        else if (delta > -d_ticks_fade && delta < 0)
    //        {
    //            var value = delta / -d_ticks_fade;
    //            _player.Control.Seek(_player.Info.GetDuration() - 0.09d);
    //            DoFadeGroups(Mathf.Abs((float)value));
    //            src.time = 0;
    //            src.pitch = 0;
    //            return;
    //        }

    //        else if (delta < -(d_ticks_vid + d_ticks_fade) && delta < 0)
    //        {
    //            var value = (-(d_ticks_vid + d_ticks_fade * 2) + Mathf.Abs(delta)) / d_ticks_fade;
    //            DoFadeGroups(Mathf.Abs((float)value));
    //            Debug.Log($"Value is {value}");
    //            src.time = 0;
    //            src.pitch = 0;
    //            return;
    //        }
    //        */

    //        //160072 + 23717 : 0 - 23717
    //        //fade delta 23717



    //        //
    //        double ptchD = (double)accelVal / (double)d_ticksPerSecond;//((double)1975 * multiplier);

    //        Debug.Log($"Tps {ticksPerSecond}");

    //        //print(ptchD);

    //        _CurrentTime = tm;//*5.0f;

    //        src.pitch = (float)ptchD;//Mathf.Clamp((float)(ptchD), -1.0f, 1.0f);
    ////

    //    }



    /*

  private void OnGUI()
  {
      try
      {
          using (new GUILayout.HorizontalScope())
          {
              GUILayout.Box("Delta Scale Change");
              _deltaChange = double.Parse(GUILayout.TextField(_deltaChange.ToString()));
          }

          using (new GUILayout.HorizontalScope())
          {
              GUILayout.Box("Scale");
              _Scale = double.Parse(GUILayout.TextField(_Scale.ToString()));
          }

          using (new GUILayout.HorizontalScope())
          {
              GUILayout.Box("CurrentTime");
              GUILayout.Box(_CurrentTime.ToString());
          }
      }
      catch
      {
          // ignored
      }
  }

  */





}