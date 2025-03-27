
using System;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using Mathf = UnityEngine.Mathf;


namespace DemolitionStudios.DemolitionMedia.Examples
{
    namespace DemolitionStudios.DemolitionMedia
    {
        [AddComponentMenu("Demolition Media/Render to IMGUI with controls")]
        public class RenderToIMGUIWithControls : MonoBehaviour
        {
            /// Target media
            public Media media;
            /// GUI skin
            public GUISkin skin;
            /// IMGUI color
            public Color color = Color.white;
            /// Whether to use the IMGUI alpha blending
            public bool alphaBlend = false;
            /// IMGUI scale mode
            public ScaleMode scaleMode = ScaleMode.ScaleToFit;
            /// IMGUI depth
            public int depth = 0;
            /// Whether to draw in fullscreen mode
            public bool fullScreen = true;
            /// Video rectangle position
            public Vector2 position = Vector2.zero;
            /// Video rectangle size
            public Vector2 size = Vector2.one;
            /// Whether to show events
            public bool showEvents = true;
            /// This camera will be rendered on top of GUI if set (use for drawing uGUI)
            public Camera OnTopCamera;
            /// Whether to draw the on top camera
            public bool DrawOnTopCamera = true;
            /// Whether to show the buttons to open other files
            public bool ShowOtherVideosButtons = true;

            /// Imgui renderer
            private RenderToIMGUI _videoIMGUI;
            /// Whether the controls are currently active
            private bool _active;
            /// Current media url
            private string _currentMediaUrl;
            /// Measure playback time independently
            private float _estimatedPlaybackTime = 0.0f;
            private float _timeOnTogRevSystem;
            //private float _timeOnTogRevPlayback;
            private bool _playbackSuspended = false;
            /// Measure loop time
            private float _loopStartTime;
            /// Events
            private List<DisplayEventEntry> _events = new List<DisplayEventEntry>();
            /// Event name presnt on screen time
            private const float _eventDisplayTime = 3.0f;
            /// Number of decode chunks threads 
            private uint _numDecodeChunksThreads = 0;
            /// GPU GDeflate decoding requested by not available
            private bool _gpuDecodingRequestedButNotAvailable = false;

            void Start()
            {
                // Note: this is a convinient way to add a component for MonoBehavior 
                _videoIMGUI = gameObject.AddComponent<RenderToIMGUI>();

                if (media == null)
                {
                    Utilities.LogError("[DemolitionMedia] RenderToIMGUIWithControls: no Media instance set");
                    return;
                }

                media.Events.AddListener(OnMediaPlayerEvent);

                _numDecodeChunksThreads = NativeDll.GetNumDecodeChunksThreads();
            }

            void Update()
            {
                // Pass params to IMGUI video renderer
                _videoIMGUI.sourceMedia = media;
                _videoIMGUI.color = color;
                _videoIMGUI.alphaBlend = alphaBlend;
                _videoIMGUI.scaleMode = scaleMode;
                _videoIMGUI.depth = depth;
                _videoIMGUI.fullScreen = fullScreen;
                _videoIMGUI.position = position;
                _videoIMGUI.size = size;
                _videoIMGUI.OnTopCamera = OnTopCamera;
                _videoIMGUI.DrawOnTopCamera = DrawOnTopCamera;

                // Screen-space mouse position
                Vector2 mousePosScreenSpace = new Vector2(
                    Input.mousePosition.x, Screen.height - Input.mousePosition.y);

                // Activate on mouse
                Rect rect = _videoIMGUI.GetDrawRect();
                if (rect.Contains(mousePosScreenSpace))
                {
                    _active = true;
                    _videoIMGUI.color = Color.white;
                }
                else
                {
                    _active = false;
                    //_videoIMGUI.color = Color.gray;
                }

                // Process events
                for (int idx = 0; idx < _events.Count; ++idx)
                {
                    var elem = _events[idx];
                    if (!elem.DecrementTimer(Time.deltaTime))
                    {
                        _events.Remove(elem);
                        idx -= 1;
                    }
                }

                UpdateEstimatedPlaybackTime();
            }

            void OnGUI()
            {
                // Check whether the source media is set
                if (_videoIMGUI.sourceMedia == null)
                    return;

                // Get the media
                Media media = _videoIMGUI.sourceMedia;
                if (media == null)
                    return;

                // Store the initial media url
                if (_currentMediaUrl == null)
                    _currentMediaUrl = media.mediaUrl;

                // Reset the gui matrix to identity
                GUI.matrix = Matrix4x4.identity;

                // Hide the controls when the mouse is outside the video draw area
                if (!_active)
                    return;

                var gfxDevice = SystemInfo.graphicsDeviceType;


                var buttonOptions = new GUILayoutOption[] { GUILayout.ExpandWidth(false),
                                                        GUILayout.Width(100)};
                var area = _videoIMGUI.GetDrawRect();
                int guiRows = 6, guiHeight = guiRows * 34;
                GUI.skin = skin;
                GUI.depth = _videoIMGUI.depth - 1;
                string mainLabel = "Demolition Media Hap " + NativePluginVersion.GetString();
                if (NativeDll.IsProVersion() != 0)
                    mainLabel += " Pro Sync";
                if (NativeDll.IsDemoVersion() != 0)
                    mainLabel += " Demo version";
                GUILayout.Box(mainLabel);

                string sysInfoLabel = "";
#if UNITY_64 || UNITY_EDITOR_64
                sysInfoLabel += "64-bit";
#else
			    sysInfoLabel += "32-bit";
#endif

                sysInfoLabel += " @ " + _numDecodeChunksThreads + " decode threads";

                GUILayout.Box(sysInfoLabel);
                GUILayout.Box("Graphics device: " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceVersion.Replace('[', ' ').Replace(']', ' '));
                GUILayout.Box("Texture: " + media.VideoFramePresentMode.ToString().Replace("External", "Unity").Replace("Immutable", "Native"));
                if (media.Compressor != HapEnums.HapCompressor.None)
                    GUILayout.Box($"Compressor: " + media.Compressor);
                var additionalDecodeInfo = media.GetAdditionalDecodeInfo();
                if (additionalDecodeInfo.Length > 0)
                    GUILayout.Box($"Additional decode info: " + additionalDecodeInfo);

                if (ShowOtherVideosButtons)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    if (GUILayout.Button("Hap", GUILayout.ExpandWidth(false)))
                    {
                        _currentMediaUrl = "SampleVideos/test_street_Hap.mov";
#if UNITY_2022_1_OR_NEWER
                        media.GPUComputeBasedDecompression = false;
#endif
                        media.Open(_currentMediaUrl);
                    }
                    if (GUILayout.Button("Hap Alpha", GUILayout.ExpandWidth(false)))
                    {
                        _currentMediaUrl = "SampleVideos/Loop3_All_BW_HapAlpha.mov";
#if UNITY_2022_1_OR_NEWER
                        media.GPUComputeBasedDecompression = false;
#endif
                        media.Open(_currentMediaUrl);
                    }


                    GUILayout.EndHorizontal();
                }
                if (_gpuDecodingRequestedButNotAvailable)
                {
                    GUIStyle style = new GUIStyle("textField");
                    style.normal.textColor = Color.red;
                    GUILayout.TextField("For GPU decoding Direct3D12 is required. Enable it in the Project Settings -> Player -> Graphics API for Windows,", style);
                    GUILayout.TextField("or add -force-d3d12 to Unity Hub Editor command line arguments", style);
                }

                GUILayout.BeginArea(new Rect(area.x, area.y + (area.height - guiHeight),
                                             area.width, guiHeight));
                GUILayout.BeginVertical();

                // Row 1
                GUILayout.BeginHorizontal();
                //GUILayout.TextField("Time: " + _estimatedPlaybackTime.ToString("F2"),
                //                    GUILayout.Width(100));
                if (media.IsPlaying && GUILayout.Button("Pause", buttonOptions))
                    media.Pause();
                else if (!media.IsPlaying && GUILayout.Button("Play", buttonOptions))
                    media.Play();
                if (GUILayout.Button("Prev frame", GUILayout.ExpandWidth(false)))
                    media.StepBackward();
                if (GUILayout.Button("Next frame", GUILayout.ExpandWidth(false)))
                    media.StepForward();
                if (GUILayout.Button("Random", GUILayout.ExpandWidth(false)))
                    media.SeekToFrame(Random.Range(0, media.VideoNumFrames));
                // Loop
                bool looping = media.IsLooping;
                bool loopingNew = GUILayout.Toggle(looping, "Loop", GUILayout.ExpandWidth(false));
                if (loopingNew != looping)
                {
                    if (loopingNew)
                        media.Loops = -1;
                    else
                        media.Loops = 1;
                }
                // Loops
                int loopsOld = media.Loops, loopsNew = loopsOld;
                GUILayout.TextField("Loops: " + loopsOld.ToString("D"),
                                    GUILayout.ExpandWidth(false));
                bool incLoops = GUILayout.Button("+", GUILayout.ExpandWidth(false));
                bool decLoops = GUILayout.Button("-", GUILayout.ExpandWidth(false));
                if (incLoops)
                    loopsNew += 1;
                else if (decLoops)
                    loopsNew = Mathf.Max(loopsNew - 1, -1);
                if (loopsNew != loopsOld)
                    media.Loops = loopsNew;
                // Mute
                bool muted = media.Muted;
                bool mutedNew = GUILayout.Toggle(muted, "Mute", GUILayout.ExpandWidth(false));
                if (mutedNew != muted)
                    media.SetMuted(mutedNew);
                // Framedrop
                bool framedrop = media.FramedropEnabled;
                bool framedropNew = GUILayout.Toggle(framedrop, "Framedrop", GUILayout.ExpandWidth(false));
                if (framedropNew != framedrop)
                    media.FramedropEnabled = framedropNew;
                // Playback rate
                GUILayout.Space(media.AudioEnabled ? 10 : 100);
                float oldPlaybackSpeed = media.PlaybackSpeed;
                string prefix = "";
                if (media.AudioEnabled)
                    prefix = " (DISABLE AUDIO TO CHANGE)";
                GUILayout.TextField($"Speed{prefix}: " + oldPlaybackSpeed.ToString("F1"),
                                    GUILayout.ExpandWidth(false));
                float minSpeed = -5.0f;
                float maxSpeed = 15.0f;
                float newPlaybackSpeed = GUILayout.HorizontalSlider(
                    oldPlaybackSpeed, minSpeed, maxSpeed, GUILayout.Width(media.AudioEnabled ? 10 : 220));
                bool incSpeed = GUILayout.Button("+", GUILayout.ExpandWidth(false));
                bool decSpeed = GUILayout.Button("-", GUILayout.ExpandWidth(false));
                if (incSpeed)
                    newPlaybackSpeed += 1;
                else if (decSpeed)
                    newPlaybackSpeed = Mathf.Max(newPlaybackSpeed - 1, 0);
                if (Math.Abs(oldPlaybackSpeed - newPlaybackSpeed) > 0.01f)
                    media.PlaybackSpeed = (float)Math.Round(newPlaybackSpeed, 1);
                bool reverse = GUILayout.Button("Tog Rev");
                if (reverse)
                {
                    _estimatedPlaybackTime = media.CurrentTime;

                    if (media.PlaybackSpeed > 0.0f)
                        media.PlaybackSpeed = -1.0f;
                    else
                        media.PlaybackSpeed = 1.0f;
                }
                GUILayout.EndHorizontal();

                // Get the active segment
                var currentStartFrame = media.StartFrame;
                var currentEndFrame = media.EndFrame;

                // Row 2
                GUILayout.BeginHorizontal();
                GUILayout.TextField("Time: " + media.CurrentTime.ToString("F2") +
                                    " / " + media.DurationSeconds.ToString("F2"),
                                    GUILayout.Width(130 + GetLabelWidth(media.DurationSeconds, 2)));
                GUILayout.TextField("Frame: " + media.VideoCurrentFrame.ToString("F0") +
                                    " / " + media.VideoNumFrames.ToString("F0"),
                                    GUILayout.Width(130 + GetLabelWidth(media.VideoNumFrames)));
                GUILayout.TextField($"{media.VideoWidth}x{media.VideoHeight}@{media.VideoFramerate}fps",
                                    GUILayout.ExpandWidth(false));
                GUILayout.TextField("Decode fps: " + media.VideoDecodeFramerate.ToString("F1"),
                                    GUILayout.ExpandWidth(false));
                int earlyDrops, lateDrops;
                media.GetFramedropCount(out earlyDrops, out lateDrops);
                GUILayout.TextField("Drops: " + earlyDrops + "/" + lateDrops,
                                    GUILayout.ExpandWidth(false));
                GUILayout.TextField("Start frame: " + currentStartFrame,
                                    GUILayout.ExpandWidth(false));
                GUILayout.TextField("End frame: " + Math.Max(currentEndFrame - 1, 0),
                                    GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();

                // Row 1.5
                var maxFrame = media.VideoNumFrames + 1;
                var epsFrame = 1.0f;
                float newStartFrame = GUILayout.HorizontalScrollbar(
                    currentStartFrame, 0, 0.0f, maxFrame, GUILayout.ExpandWidth(true));
                if (Math.Abs(currentStartFrame - newStartFrame) > epsFrame)
                {
                    media.StartFrame = (int)newStartFrame;
                }
                float newEndFrame = GUILayout.HorizontalScrollbar(
                    currentEndFrame, 0, 0.0f, maxFrame, GUILayout.ExpandWidth(true));
                if (Math.Abs(currentEndFrame - newEndFrame) > epsFrame && currentEndFrame <= maxFrame)
                {
                    media.EndFrame = (int)newEndFrame;
                }

                // Row 3
                var epsTime = media.VideoWidth > 4600
                    ? 5.0f / media.VideoFramerate // For large videos skip every 5 frames when seeking
                    : 1.0f / media.VideoFramerate;
                float currentTime = media.CurrentTime;
                float newTime = GUILayout.HorizontalSlider(
                    currentTime, 0.0f, media.DurationSeconds, GUILayout.ExpandWidth(true));
                float currentFrame = media.VideoCurrentFrame;
                int newFrame = (int)Math.Round(newTime * media.VideoFramerate, 0);
                if (newFrame != currentFrame)
                {
                    //Utilities.Log("currentFrame: " + currentFrame.ToString() + "; newFrame: " + newFrame.ToString());
                    media.SeekToFrame(newFrame);
                    _estimatedPlaybackTime = media.CurrentTime;
                }

                // Row 4
                GUILayout.BeginHorizontal();
                GUILayout.Label("Url: ", "box", GUILayout.Width(60));
                _currentMediaUrl = GUILayout.TextField(_currentMediaUrl, 256, GUILayout.MinWidth(200));
#if UNITY_64 || UNITY_EDITOR_64
                media.preloadToMemory = GUILayout.Toggle(media.preloadToMemory, "Preload to memory");
#endif
                media.playOnOpen = GUILayout.Toggle(media.playOnOpen, "Play on open");
                media.openWithAudio = GUILayout.Toggle(media.openWithAudio, "Open with audio");
#if UNITY_2022_1_OR_NEWER
            if (gfxDevice == GraphicsDeviceType.Direct3D12)
                media.GPUComputeBasedDecompression = GUILayout.Toggle(media.GPUComputeBasedDecompression, "Use GPU");
#endif
                //media.urlType = GUILayout.SelectionGrid(media.urlType, ); // TODO
                if (GUILayout.Button("Open", GUILayout.Width(100)))
                {
                    media.Open(_currentMediaUrl);
                }
                if (GUILayout.Button("Close", GUILayout.Width(100)))
                {
                    media.Close();
                }
                GUILayout.EndHorizontal();

                // Row 5 (optional)
                //if (media.IsPlaying && media.VideoDecodeFramerate < 0.8f * media.VideoFramerate) {
                //	GUIStyle style = new GUIStyle("textField");
                //	style.normal.textColor = Color.red;
                //	//GUILayout.ExpandWidth(false)

                //    GUILayout.TextField(
                //        media.preloadToMemory
                //            ? "Your PC seems to be too slow for this video! Try on a faster one or use a video with smaller resolution and/or framerate"
                //            : "Slow disk? Try preloading to memory or wait for the next loop. A faster harddisk/SSD/RAID is another option",
                //        style);
                //}
                if (media.AudioEnabled && !Mathf.Approximately(Math.Abs(newPlaybackSpeed), 1.0f))
                {
                    GUIStyle style = new GUIStyle("textField");
                    style.normal.textColor = Color.red;
                    //GUILayout.ExpandWidth(false)

                    GUILayout.TextField(
                        "If you want to change the playback speed, it's recommended to disable the audio (uncheck \"Open with audio\" above and press \"Open\")",
                        style);
                }

                GUILayout.EndVertical();
                GUILayout.EndArea();


                // State/events area
                if (showEvents)
                {
                    var eventsAreaRelativePosX = 0.7f;
                    GUILayout.BeginArea(new Rect(area.x + area.width * eventsAreaRelativePosX,
                                                 area.y,
                                                 area.width * (1.0f - eventsAreaRelativePosX),
                                                 area.width * 0.5f));
                    GUILayout.BeginVertical("box");
                    // 1. State
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("State: " + media.State.ToString());
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                    // 2. Events
                    GUILayout.Label("Events: ", "box");
                    if (_events.Count > 0)
                    {
                        GUILayout.BeginVertical("box");
                        foreach (DisplayEventEntry elem in _events)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            GUI.color = new Color(1.0f, 1.0f, 1.0f, elem.Timer);
                            GUILayout.Label(elem.EventName/*, GUILayout.ExpandWidth(false)*/);
                            GUILayout.FlexibleSpace();
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndArea();
                }
            }

            // Callback function to handle events
            public void OnMediaPlayerEvent(Media source, MediaEvent.Type type, MediaError error)
            {
                if (error == MediaError.NoError)
                {
                    //Utilities.Log("[RenderToIMGUIWithControls] Event: " + type.ToString());
                    _events.Add(new DisplayEventEntry(type.ToString(), _eventDisplayTime));

                    if (type == MediaEvent.Type.PlaybackStarted)
                    {
                        _estimatedPlaybackTime = media.CurrentTime;
                        _timeOnTogRevSystem = Time.realtimeSinceStartup;
                        _playbackSuspended = false;
                        _loopStartTime = Time.realtimeSinceStartup;
                    }
                    else if (type == MediaEvent.Type.PlaybackResumed)
                    {
                        _timeOnTogRevSystem = Time.realtimeSinceStartup;
                        _playbackSuspended = false;
                    }
                    else if (type == MediaEvent.Type.PlaybackSuspended)
                    {
                        _playbackSuspended = true;
                    }
                    else if (type == MediaEvent.Type.PlaybackNewLoop)
                    {
                        float now = Time.realtimeSinceStartup;
                        float loopTime = (float)Math.Round(now - _loopStartTime, 2);
                        _loopStartTime = now;
                        string str = $"Loop time: {loopTime}";
                        _events.Add(new DisplayEventEntry(str, _eventDisplayTime));
                        //Utilities.Log(str);
                    }
                    else if (type == MediaEvent.Type.GPUComputeBasedDecompressionNotAvailable)
                    {
                        _gpuDecodingRequestedButNotAvailable = true;
                    }
                    else if (type == MediaEvent.Type.Closed || type == MediaEvent.Type.OpeningStarted)
                    {
                        _gpuDecodingRequestedButNotAvailable = false;
                    }
                }
                else
                {
                    //Utilities.LogError("[RenderToIMGUIWithControls] Error: " + error.ToString());
                    _events.Add(new DisplayEventEntry(type.ToString() + ": " + error.ToString(), _eventDisplayTime));
                }
            }

            private void UpdateEstimatedPlaybackTime()
            {
                if (_playbackSuspended)
                    return;

                float now = Time.realtimeSinceStartup;
                float delta = now - _timeOnTogRevSystem;
                _timeOnTogRevSystem = now;
                _estimatedPlaybackTime = (_estimatedPlaybackTime + media.PlaybackSpeed * delta)
                    % media.DurationSeconds;
                if (_estimatedPlaybackTime < 0.0f)
                    _estimatedPlaybackTime += media.DurationSeconds;
            }

            private static int NumberLength(float number)
            {
                if (number < 0)
                    throw new ArgumentOutOfRangeException();
                if (number == 0)
                    return 1;
                return (int)Math.Floor(Math.Log10(number)) + 1;
            }

            private static int GetLabelWidth(float number, int decimals = 0)
            {
                const int numberWidth = 20;
                var result = (NumberLength(number) + decimals) * numberWidth * 2;
                //Utilities.Log("number: " + number + "; width: " + result);
                return result;
            }
        }
    }
}