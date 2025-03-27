using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace DemolitionStudios.DemolitionMedia
{
    using ClockType = System.Double;

    public struct ClockUpdateResult
    {
        public ClockUpdateResult(ClockType position_, ClockType speed_, bool paused_)
        {
            position = position_;
            speed = speed_;
            paused = paused_;
        }

        public ClockType position;
        public ClockType speed;
        public bool paused;
    };

    public class LFOClockBase
    {
        /// Target media
        public Media TargetMedia;

        [SerializeField] ClockType _position = 0.0f;
        [SerializeField, Range(-10, 10)] public ClockType _speed = 1;
        [SerializeField] public bool _pause = false;
        [SerializeField] public bool _loop = true;

        [SerializeField] public bool _frameQueueDrained = false;
        [SerializeField] public bool _useCorrection = false;
        [SerializeField] public bool _waitOnDrainedQueue = false;

        /// GUI skin
        public GUISkin skin;

        public bool Active
        {
            get { return _active; }
            set { _active = value; }
        }
        bool _active = false;
        ClockType _storedPosition = 0.0f;
        ClockType _clockTimeMonotonous = 0;
        ClockType _storedSpeed = 1e10f;
        int _clockFrame = 0;
        int _newFrameMonotonous = 0;
        int _storedFrameMonotonous = 0;
        int _storedFrame = 0;
        int _frameQueueRemaining = 0;
        int _diff = 0;
        int _correction = 0;
        int _seekTarget = 0;

        bool _makeSeekCorrection = false;

        public Rect DrawRect { get { return _drawRect; } set { _drawRect = value; } }
        private Rect _drawRect = new Rect(0, 0, 1920, 400);


        private DateTime tp1;
        private DateTime tp2;
        private bool deltaTimeInitDone = false;

        bool _shownFirstFrame = false;

#region Helper functions
        int ModWrap(int x, int y)
        {
            if (0 == y)
                return x;

            int m = x - y * (int)Mathf.Floor(x / y);

            // handle boundary cases resulted from floating-point cut off:
            if (y > 0)              // modulo range: [0..y)
            {
                if (m >= y)           // Mod(-1e-16             , 360.    ): m= 360.
                    return 0;

                if (m < 0)
                {
                    if (y + m == y)
                        return 0; // just in case...
                    else
                        return y + m; // Mod(106.81415022205296 , _TWO_PI ): m= -1.421e-14 
                }
            }
            else                    // modulo range: (y..0]
            {
                if (m <= y)           // Mod(1e-16              , -360.   ): m= -360.
                    return 0;

                if (m > 0)
                {
                    if (y + m == y)
                        return 0; // just in case...
                    else
                        return y + m; // Mod(-106.81415022205296, -_TWO_PI): m= 1.421e-14 
                }
            }

            return m;
        }
#endregion

#region media events handling
        // Callback function to handle events
        public void OnMediaPlayerEvent(Media source, MediaEvent.Type type, MediaError error)
        {
            if (error == MediaError.NoError)
            {
                Utilities.Log("[LFOClockBase] Event: " + type.ToString());

                if (type == MediaEvent.Type.ClosingStarted)
                {
                    _active = false;
                }
                else if (type == MediaEvent.Type.Opened)
                {
                    _active = true;
                    _clockTimeMonotonous = 0;
                }
            }
        }
        #endregion

        Stopwatch stopwatch;
        long et1, et2;
        ClockType GetDeltaTime()
        {
            if (!deltaTimeInitDone)
            {
                stopwatch = Stopwatch.StartNew();
                et1 = et2 = stopwatch.ElapsedTicks;
                deltaTimeInitDone = true;
                return 0;
            }

            et2 = stopwatch.ElapsedTicks;
            ClockType deltaTime = (ClockType)(et2 - et1) / Stopwatch.Frequency;
            et1 = et2;
            return deltaTime;
        }

        ClockType GetDeltaTimeDateTime()
        {
            if (!deltaTimeInitDone)
            {
                tp1 = DateTime.Now;
                tp2 = DateTime.Now;
                deltaTimeInitDone = true;
                return 0;
            }

            // Driftsync C# client also uses DateTime.Now.Ticks, so should be reliable/monotonic
            tp2 = DateTime.Now;
            ClockType deltaTime = (ClockType)((tp2.Ticks - tp1.Ticks) / 10000000.0);
            tp1 = tp2;
            return deltaTime;
        }

        public void ResetDeltaTime()
        {
            GetDeltaTime();
        }

        #region MonoBehaviour implementation
        public void Awake()
        {

            if (NativeDll.IsProVersion() == 0)
            {
                Utilities.LogError("LFOClock will only correctly work with PRO version of Demolition Media Hap, please upgrade on the Asset Store");
            }
        }

        public void Start(Media media)
        {
            if (media == null)
            {
                Utilities.LogError("[DemolitionMedia] LFOClock: no Media instance set");
                return;
            }

            if (TargetMedia)
            {
                TargetMedia.Events.RemoveListener(OnMediaPlayerEvent);
            }
            TargetMedia = media;
            TargetMedia.Events.AddListener(OnMediaPlayerEvent);
        }

        public void Update(bool updateClock, bool seekToFrame, bool updateTextures = false)
        {
            bool texturesChanged = false;

            if (TargetMedia == null || !_active || !TargetMedia.IsOpened)
                return;

            int remaining, previous, current, next;
            NativeDll.DebugGetFrameQueueParams(TargetMedia.MediaId, out remaining, out previous, out current, out next);
            _frameQueueDrained = remaining < 1/* && _storedFrame != 0*/;
            _frameQueueRemaining = remaining;

            if (_frameQueueDrained && _waitOnDrainedQueue)
            {
                //Utilities.Log("Frame queue drained, time = " + _storedPosition.ToString());
                return;
            }

            const int Threshold = 7;
            if (seekToFrame && ComputeDifference(TargetMedia.VideoCurrentFrame, _storedFrame) > Threshold ||
                (Pause && TargetMedia.VideoCurrentFrame != _storedFrame))
            {
                //Utilities.LogWarning("Stored frame: " + _storedFrame + "; VideoCurrentFrame: " + TargetMedia.VideoCurrentFrame);
                TargetMedia.SeekToFrame(_storedFrame);
            }

            bool forceUpdate = false;
            if (!Utilities.ApproximatelyEqual(_position, _storedPosition))
            {
                // Set clock to new position
                _clockTimeMonotonous = _position;
                forceUpdate = true;
                //Utilities.Log("Position: " + _position + "; stored: " + _storedPosition);
            }

            // TODO: replace to (int)Math.Round or just Convert.ToInt32 later
            int newFrameMonotonous = Convert.ToInt32(Math.Round(_clockTimeMonotonous * TargetMedia.VideoFramerate,
                                                     MidpointRounding.AwayFromZero));
            int newFrame = ModWrap(newFrameMonotonous, TargetMedia.VideoNumFrames);
            _newFrameMonotonous = newFrameMonotonous;

            int direction = _speed > 0.0 ? 1 : -1;
            // Note: even if playback is paused, sometimes we need to seek to be aligned with clock (i.e. manual clock change)
            if (/*!_pause && */newFrame != _storedFrame || forceUpdate)
            {
                bool switchedDirection = _speed * _storedSpeed < 0.0;
                _storedSpeed = _speed;

                bool update = (_speed > 0.0 && newFrameMonotonous > _storedFrameMonotonous + _correction)
                           || (_speed < 0.0 && newFrameMonotonous < _storedFrameMonotonous + _correction);
                if (update || switchedDirection || forceUpdate)
                {
                    if (_useCorrection && /*_frameQueueDrained && */switchedDirection)
                    {
                        ClockType factor = TargetMedia.VideoFramerate / 60 *
                                       TargetMedia.VideoWidth * TargetMedia.VideoHeight / (1920 * 1080 * 6);
                        _correction = (int)(7 * direction * Math.Abs(_speed) * factor);
                    }
                    else if (_makeSeekCorrection && _frameQueueDrained)
                    {
                        _correction = (int)(3 * direction * Math.Abs(_speed));
                        _makeSeekCorrection = false;

                        //Utilities.Log("Seek correction " + _correction);
                    }
                    else
                    {
                        _correction = 0;
                    }


                    _seekTarget = ModWrap(newFrame + _correction, TargetMedia.VideoNumFrames);
                    if (seekToFrame)
                        TargetMedia.SeekToFrame(_seekTarget);


                    _storedFrame = _seekTarget;
                    _storedFrameMonotonous = newFrameMonotonous;
                    _position = _seekTarget / TargetMedia.VideoFramerate;
                    _storedPosition = _position;

                    texturesChanged = true;

                    if (_correction != 0)
                        Utilities.Log("Using correction " + _correction +
                                  "; Seek to frame " + _seekTarget +
                                  "; Clock value " + newFrame +
                                  "; Speed " + _speed);

                }
            }

            // Note: only should be called from the main Unity thread
            if (updateTextures && (texturesChanged || !_shownFirstFrame))
            {
                UpdateTextures();
                _shownFirstFrame = true;
            }

            // Always update delta time
            ClockType dt = GetDeltaTime();

            if (updateClock && !_pause)
            {
                _clockTimeMonotonous += dt * _speed;
            }
            _clockFrame = newFrame;
        }

        public ClockUpdateResult UpdateAndGetResult(bool updateClock, bool seekToFrame)
        {
            Update(updateClock: updateClock, seekToFrame: seekToFrame);
            return new ClockUpdateResult(Position, Speed, Pause);
        }

        public ClockUpdateResult GetResult()
        {
            return new ClockUpdateResult(Position, Speed, Pause);
        }

        public void UpdateTextures()
        {
            TargetMedia.UpdateTextures();
        }

        public int ComputeDifference(int frameA, int frameB)
        {
            if (TargetMedia.VideoNumFrames == 0)
                return 0;
            return Math.Min(Math.Abs(frameA - frameB),
                            Math.Abs((Math.Abs(frameA - frameB) - TargetMedia.VideoNumFrames) % TargetMedia.VideoNumFrames));
        }

        public ClockType ComputeDifference(ClockType posA, ClockType posB)
        {
            if (Utilities.ApproximatelyEqual(TargetMedia.DurationSeconds, 0f))
                return 0;
            return Math.Min(Math.Abs(posA - posB),
                            Math.Abs((Math.Abs(posA - posB) - TargetMedia.DurationSeconds) % TargetMedia.DurationSeconds));
        }

        public int GetIMGUIFontSize()
        {
            return 25;
        }
        
        public void OnGUI(Action guiCallback = null)
        {
            if (TargetMedia == null)
                return;

            // Reset the gui matrix to identity
            GUI.matrix = Matrix4x4.identity;

            var buttonOptions = new GUILayoutOption[] { GUILayout.ExpandWidth(false),
                                                        GUILayout.Width(100)};
            int fontSize = GetIMGUIFontSize();
            var area = DrawRect;
            int guiRows = guiCallback != null ? 4 : 3, guiHeight = guiRows * 200;
            GUI.skin = skin;
            GUI.skin.textField.fontSize = fontSize;
            GUI.skin.label.fontSize = fontSize;
            GUI.skin.toggle.fontSize = fontSize;
            GUI.depth = -1;


            GUILayout.BeginArea(new Rect(area.x, area.y/* + (area.height - guiHeight)*/,
                                         area.width, guiHeight));
            GUILayout.BeginVertical();

            // Row 0 (optional)
            guiCallback?.Invoke();

            // Row 1
            GUILayout.BeginHorizontal();
            GUILayout.Label(TargetMedia.VideoWidth.ToString("D") + "x" +
                            TargetMedia.VideoHeight.ToString("D") + "@" +
                            Convert.ToInt32(TargetMedia.VideoFramerate).ToString("D") + "fps",
                            GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            GUILayout.Label("Speed: " + _speed.ToString("F"),
                            GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
            GUILayout.Label("Pause: " + _pause.ToString(),
                            GUILayout.ExpandWidth(false));
            GUILayout.EndHorizontal();

            // Row 2
            GUILayout.BeginHorizontal();
            int currentFrame = TargetMedia.VideoCurrentFrame;
            int direction = TargetMedia.PlaybackSpeed > 0.0 ? 1 : -1;
            _diff = ComputeDifference(_clockFrame, currentFrame);
            GUILayout.Label("Clock: " + _clockFrame.ToString("D"),
                            GUILayout.Width(200));
            GUILayout.Label("Shown: " + currentFrame.ToString("D"),
                            GUILayout.Width(200));
            GUILayout.Label("Diff: " + _diff.ToString("D"),
                            GUILayout.Width(120));
            //if (_storedFrame != currentFrame)
            //    GUILayout.TextField("Not equal!",
            //                    GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            // Row 3

            // Row 4
            GUILayout.BeginHorizontal();
            GUILayout.Label("Remaining: " + _frameQueueRemaining.ToString("D"),
                            GUILayout.Width(210));
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        public ClockType Speed 
        { 
            get { return _speed; }
            set 
            {
                if (value == _speed)
                    return;
                _speed = value;
                // Set playback speed hint for frames preloading
                /// TODO: use double?
                TargetMedia.PlaybackSpeed = (float)_speed;
            }
        }

        public ClockType Position 
        {
            get { return _position; } 
            set { _position = value; } 
        }
        
        public void SetFrame(int value)
        {
            ClockType position = value / TargetMedia.VideoFramerate;
            _position = position;
        }

        public int GetFrame()
        {
            return _clockFrame;
        }

        public bool Pause 
        { 
            get 
            { 
                return _pause; 
            } 
            set 
            { 
                _pause = value; 
            } 
        }

        public void SetMakeSeekCorrection(bool value)
        {
            _makeSeekCorrection = value;
        }

        public int GetSeekTarget()
        {
            return _seekTarget;
        }

        #endregion
    }
}