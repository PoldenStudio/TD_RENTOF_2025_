using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;
#else
using System.Threading;
#endif

namespace jp.kshoji.midisystem
{
    /// <summary>
    /// <see cref="ISequencer"/> implementation
    /// </summary>
    public class SequencerImpl : ISequencer
    {
        public const int LoopContinuously = -1;
        private static readonly SyncMode[] MasterSyncModes = { SyncMode.InternalClock };
        private static readonly SyncMode[] SlaveSyncModes = { SyncMode.NoSync };

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly HashSet<IControllerEventListener>[] controllerEventListenerMap =
            new HashSet<IControllerEventListener>[128];

        private readonly HashSet<IMetaEventListener> metaEventListeners = new HashSet<IMetaEventListener>();
        private readonly List<IReceiver> receivers = new List<IReceiver>();
        private readonly Dictionary<Track, HashSet<int>> recordEnable = new Dictionary<Track, HashSet<int>>();
        private readonly Dictionary<int, bool> trackMute = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> trackSolo = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> channelMute = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> channelSolo = new Dictionary<int, bool>();

        private readonly List<ITransmitter> transmitters = new List<ITransmitter>();

        private volatile bool isOpen;
        private int loopCount;
        private long loopEndPoint = -1;
        private long loopStartPoint;
        private bool playIntroOnFirstLoop = false;
        private SyncMode masterSyncMode = SyncMode.InternalClock;
        private bool needRefreshPlayingTrack;

        // playing
        private Track playingTrack;

        // recording
        private double recordingStartedTime;
        private Track recordingTrack;
        private long recordStartedTick;
        private double runningStoppedTime;
        private Sequence sequence;

        private SequencerThread sequencerThread;
        private SyncMode slaveSyncMode = SyncMode.NoSync;
        private volatile float tempoFactor = 1.0f;
        private float tempoInBpm = 120.0f;
#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
        private Thread thread;
        private System.ComponentModel.AsyncOperation asyncOperation;
#endif
        private double tickPositionSetTime;
        private readonly IReceiver midiEventRecordingReceiver;
        private readonly Action onOpened;

        private static HashSet<SequencerImpl> sequencers = new HashSet<SequencerImpl>();

        public delegate void SequenceFinishedHandler();

        /// <summary>
        /// Event callback for sequencer playback finished
        /// </summary>
        public event SequenceFinishedHandler OnSequenceFinished;

        public delegate void SequencePausedHandler();

        /// <summary>
        /// Event callback for sequencer playback paused
        /// </summary>
        public event SequencePausedHandler OnSequencePaused;

        public delegate void SequenceStartedHandler();

        /// <summary>
        /// Event callback for sequencer playback started/resumed
        /// </summary>
        public event SequenceStartedHandler OnSequenceStarted;

        public delegate void SequenceTimeUpdatedHandler(float seconds);

        /// <summary>
        /// Event callback for sequencer playback started/resumed
        /// </summary>
        public event SequenceTimeUpdatedHandler OnSequenceTimeUpdated;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="onOpened">Action called on opened the sequencer</param>
        public SequencerImpl(Action onOpened)
        {
            midiEventRecordingReceiver = new MidiEventRecordingReceiver(this);
            this.onOpened = onOpened;
        }

        /// <inheritdoc cref="IMidiDevice.GetDeviceInfo"/>
        public Info GetDeviceInfo()
        {
            return new Info("sequencer", "vendor", "description", "version");
        }

        /// <summary>
        /// Update MIDI device connections to sequencer
        /// </summary>
        /// <param name="receiverFilter">receivers set to connect to sequencer<br/>
        /// - null: the all receivers will be connected.<br/>
        /// - empty set: don't connect any receivers.<br/>
        /// - non-empty set: the specified receivers will be connected.</param>
        /// <param name="transmitterFilter">transmitters set to connect to sequencer<br/>
        /// - null: the all transmitters will be connected.<br/>
        /// - empty set: don't connect any transmitters.<br/>
        /// - non-empty set: the specified transmitters will be connected.</param>
        public void UpdateDeviceConnections(HashSet<IReceiver> receiverFilter = null, HashSet<ITransmitter> transmitterFilter = null)
        {
            lock (receivers)
            {
                receivers.Clear();
                if (receiverFilter != null)
                {
                    var receiverList = MidiSystem.GetReceivers();
                    receivers.AddRange(receiverList.Where(receiverFilter.Contains));
                }
                else
                {
                    receivers.AddRange(MidiSystem.GetReceivers());
                }
            }

            lock (transmitters)
            {
                transmitters.Clear();
                if (transmitterFilter != null)
                {
                    var transmitterList = MidiSystem.GetTransmitters();
                    transmitters.AddRange(transmitterList.Where(transmitterFilter.Contains));
                }
                else
                {
                    transmitters.AddRange(MidiSystem.GetTransmitters());
                }

                foreach (var transmitter in transmitters)
                {
                    // receive from all transmitters
                    transmitter.SetReceiver(midiEventRecordingReceiver);
                }
            }
        }

#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
        /// <summary>
        /// Opens sequencer using unity coroutine
        /// </summary>
        /// <returns>coroutine</returns>
        public IEnumerator OpenCoroutine()
        {
            // open devices
            UpdateDeviceConnections();

            sequencerThread = new SequencerThread();
            return sequencerThread.StartSequencerThread(this, () =>
            {
                isOpen = true;
                onOpened();
            });
        }
#endif

        /// <summary>
        /// Opens sequencer using thread
        /// </summary>
        public void Open()
        {
#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
            asyncOperation = System.ComponentModel.AsyncOperationManager.CreateOperation(null);
            if (thread == null)
            {
                sequencerThread = new SequencerThread();
                lock (sequencers)
                {
                    sequencers.Add(this);
                }
                thread = new Thread(() => sequencerThread.StartSequencerThread(this, () =>
                {
                    isOpen = true;
                    onOpened();
                }));
                thread.Name = "MidiSequencer_" + thread.ManagedThreadId;
                try
                {
                    thread.Start();
                }
                catch (ThreadStateException)
                {
                    // maybe already started
                }
            }
            else
            {
                onOpened();
            }

            lock (thread)
            {
                Monitor.PulseAll(thread);
            }
#endif
        }

        /// <summary>
        /// Closes all sequencers
        /// </summary>
        public static void CloseAllSequencers()
        {
            lock (sequencers)
            {
                foreach (var sequencer in sequencers)
                {
                    sequencer.Close();
                }
            }
        }
        
        /// <summary>
        /// Closes sequencer
        /// </summary>
        public void Close()
        {
            lock (receivers)
            {
                receivers.Clear();
            }

            lock (transmitters)
            {
                transmitters.Clear();
            }

            if (sequencerThread != null)
            {
                sequencerThread.StopPlaying();
                isOpen = false;
#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
                lock (thread)
                {
                    Monitor.PulseAll(thread);
                }
#endif
                sequencerThread = null;
#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
                thread = null;
#endif
            }

            lock (metaEventListeners)
            {
                metaEventListeners.Clear();
            }

            lock (controllerEventListenerMap)
            {
                foreach (var controllerEventListeners in controllerEventListenerMap)
                {
                    controllerEventListeners?.Clear();
                }
            }
        }

        /// <summary>
        /// Checks the sequencer is open
        /// </summary>
        /// <returns>true: the sequencer is open</returns>
        public bool GetIsOpen()
        {
            return isOpen;
        }

        /// <summary>
        /// Get the count of receivers
        /// </summary>
        /// <returns>count of receivers</returns>
        public int GetMaxReceivers()
        {
            lock (receivers)
            {
                return receivers.Count;
            }
        }

        /// <summary>
        /// Get the count of transmitters
        /// </summary>
        /// <returns>count of transmitters</returns>
        public int GetMaxTransmitters()
        {
            lock (transmitters)
            {
                return transmitters.Count;
            }
        }

        /// <summary>
        /// Get the first receiver
        /// </summary>
        /// <returns>the receiver</returns>
        /// <exception cref="MidiUnavailableException">receiver not found</exception>
        public IReceiver GetReceiver()
        {
            lock (receivers)
            {
                if (receivers.Count == 0)
                {
                    throw new MidiUnavailableException("Receiver not found");
                }

                return receivers[0];
            }
        }

        /// <summary>
        /// Get the all receivers
        /// </summary>
        /// <returns>list of receivers</returns>
        public List<IReceiver> GetReceivers()
        {
            lock (receivers)
            {
                return receivers.ToList();
            }
        }

        /// <summary>
        /// Get the first transmitter
        /// </summary>
        /// <returns>the transmitter</returns>
        /// <exception cref="MidiUnavailableException">transmitter not found</exception>
        public ITransmitter GetTransmitter()
        {
            lock (transmitters)
            {
                if (transmitters.Count == 0)
                {
                    throw new MidiUnavailableException("Transmitter not found");
                }

                return transmitters[0];
            }
        }

        /// <summary>
        /// Get the all transmitters
        /// </summary>
        /// <returns>list of transmitters</returns>
        public List<ITransmitter> GetTransmitters()
        {
            lock (transmitters)
            {
                return transmitters.ToList();
            }
        }

        /// <summary>
        /// Add EventListener for <see cref="ShortMessage.ControlChange"/>
        /// </summary>
        /// <param name="listener">event listener</param>
        /// <param name="controllers">controller codes</param>
        /// <returns>registered controllers for the specified listener</returns>
        public int[] AddControllerEventListener(IControllerEventListener listener, int[] controllers)
        {
            lock (controllerEventListenerMap)
            {
                foreach (var controllerId in controllers)
                {
                    var listeners = controllerEventListenerMap[controllerId];
                    if (listeners == null)
                    {
                        listeners = new HashSet<IControllerEventListener>();
                    }

                    listeners.Add(listener);
                    controllerEventListenerMap[controllerId] = listeners;
                }

                return controllers;
            }
        }

        /// <summary>
        /// Remove EventListener for <see cref="ShortMessage.ControlChange"/>
        /// </summary>
        /// <param name="listener">event listener</param>
        /// <param name="controllers">controller codes</param>
        /// <returns>registered controllers for the specified listener</returns>
        public int[] RemoveControllerEventListener(IControllerEventListener listener, int[] controllers)
        {
            lock (controllerEventListenerMap)
            {
                var resultList = new List<int>();
                foreach (var controllerId in controllers)
                {
                    var listeners = controllerEventListenerMap[controllerId];
                    if (listeners != null && listeners.Contains(listener))
                    {
                        listeners.Remove(listener);
                    }
                    else
                    {
                        // remaining controller id
                        resultList.Add(controllerId);
                    }

                    controllerEventListenerMap[controllerId] = listeners;
                }

                // returns currently registered controller ids for the argument specified listener
                var resultPrimitiveArray = new int[resultList.Count];
                for (var i = 0; i < resultPrimitiveArray.Length; i++)
                {
                    resultPrimitiveArray[i] = resultList[i];
                }

                return resultPrimitiveArray;
            }
        }

        /// <summary>
        /// Add EventListener for <see cref="MetaMessage"/>
        /// </summary>
        /// <param name="listener">event listener</param>
        /// <returns>true if registered successfully</returns>
        public bool AddMetaEventListener(IMetaEventListener listener)
        {
            // return true if registered successfully
            lock (metaEventListeners)
            {
                return metaEventListeners.Add(listener);
            }
        }

        /// <summary>
        /// Remove EventListener for <see cref="MetaMessage"/>
        /// </summary>
        /// <param name="listener">event listener</param>
        public void RemoveMetaEventListener(IMetaEventListener listener)
        {
            lock (metaEventListeners)
            {
                metaEventListeners.Remove(listener);
            }
        }

        /// <summary>
        /// Get the count of loop.
        /// </summary>
        /// <returns>the count of loop
        /// <ul>
        ///     <li><see cref="SequencerImpl.LoopContinuously"/>: play loops eternally</li>
        ///     <li>0: play once(no loop)</li>
        ///     <li>1: play twice(loop once)</li>
        /// </ul>
        /// </returns>
        public int GetLoopCount()
        {
            return loopCount;
        }

        /// <summary>
        /// Set count of loop.
        /// </summary>
        /// <param name="count">
        /// <ul>
        ///     <li><see cref="SequencerImpl.LoopContinuously"/>: play loops eternally</li>
        ///     <li>0: play once(no loop)</li>
        ///     <li>1: play twice(loop once)</li>
        /// </ul>
        /// </param>
        public void SetLoopCount(int count)
        {
            if (count != LoopContinuously && count < 0)
            {
                throw new ArgumentException($"Invalid loop count value: {count}");
            }

            loopCount = count;
        }

        /// <summary>
        /// Get the setting of: plays `intro` part before loop start point
        /// </summary>
        /// <returns></returns>
        public bool GetPlayIntroOnFirstLoop()
        {
            return playIntroOnFirstLoop;
        }

        /// <summary>
        /// Plays `intro` part before loop start point
        /// </summary>
        /// <param name="playIntro">true: plays intro before loop start point</param>
        public void SetPlayIntroOnFirstLoop(bool playIntro)
        {
            playIntroOnFirstLoop = playIntro;
        }

        /// <summary>
        /// Get start point(ticks) of loop.
        /// </summary>
        /// <returns>ticks</returns>
        public long GetLoopStartPoint()
        {
            return loopStartPoint;
        }

        /// <summary>
        /// Set loop-playing start point in `tick`
        /// </summary>
        /// <param name="tick">start position of the sequence in tick</param>
        /// <exception cref="ArgumentException">the tick is out of range</exception>
        public void SetLoopStartPoint(long tick)
        {
            if (tick > GetTickLength() || loopEndPoint != -1 && tick > loopEndPoint || tick < 0)
            {
                throw new ArgumentException($"Invalid loop start point value: {tick}");
            }

            loopStartPoint = tick;
        }

        /// <summary>
        /// Get the end point(ticks) of loop.
        /// </summary>
        /// <returns>the end point(ticks) of loop</returns>
        public long GetLoopEndPoint()
        {
            return loopEndPoint;
        }

        /// <summary>
        /// Set loop-playing end point in `tick`
        /// </summary>
        /// <param name="tick">end position of the sequence in tick</param>
        /// <exception cref="ArgumentException">the tick is out of range</exception>
        public void SetLoopEndPoint(long tick)
        {
            if (tick > GetTickLength() || tick != -1 && loopStartPoint > tick || tick < -1)
            {
                throw new ArgumentException($"Invalid loop end point value: {tick}");
            }

            loopEndPoint = tick;
        }

        /// <summary>
        /// Get loop-playing start point in `microseconds`
        /// </summary>
        /// <returns>microseconds</returns>
        public long GetLoopStartPointMicroseconds()
        {
            return ConvertTickToMicroseconds(loopStartPoint);
        }

        /// <summary>
        /// Set loop-playing start point in `microseconds`
        /// </summary>
        /// <param name="microseconds">position of the sequence in microseconds</param>
        /// <exception cref="ArgumentException">the microseconds is out of range</exception>
        public void SetLoopStartPointMicroseconds(long microseconds)
        {
            SetLoopStartPoint(ConvertMicrosecondsToTick(microseconds));
        }

        /// <summary>
        /// Get loop-playing end point in `microseconds`
        /// </summary>
        /// <returns>microseconds</returns>
        public long GetLoopEndPointMicroseconds()
        {
            return ConvertTickToMicroseconds(loopEndPoint);
        }

        /// <summary>
        /// Set loop-playing end point in `microseconds`
        /// </summary>
        /// <param name="microseconds">position of the sequence in microseconds</param>
        /// <exception cref="ArgumentException">the microseconds is out of range</exception>
        public void SetLoopEndPointMicroseconds(long microseconds)
        {
            SetLoopEndPoint(ConvertMicrosecondsToTick(microseconds));
        }

        /// <summary>
        /// Get the <see cref="SyncMode"/> for master.
        /// </summary>
        /// <returns>the <see cref="SyncMode"/> for master.</returns>
        public SyncMode GetMasterSyncMode()
        {
            return masterSyncMode;
        }

        /// <summary>
        /// Set the <see cref="SyncMode"/> for master.
        /// </summary>
        /// <param name="sync">the <see cref="SyncMode"/> for master.</param>
        public void SetMasterSyncMode(SyncMode sync)
        {
            foreach (var availableMode in GetMasterSyncModes())
            {
                if (Equals(availableMode, sync))
                {
                    masterSyncMode = sync;
                }
            }
        }

        /// <summary>
        /// Get the available <see cref="SyncMode"/> for master.
        /// </summary>
        /// <returns>the available <see cref="SyncMode"/> for master.</returns>
        public SyncMode[] GetMasterSyncModes()
        {
            return MasterSyncModes;
        }

        internal long ConvertTickToMicroseconds(long currentTickPosition)
        {
            if (playingTrack == null)
            {
                return 0L;
            }

            var tickPosition = 0L;
            var ticksPerMicrosecond = 120f;
            var trackLength = 0.0;
            for (var i = 0; i < playingTrack.Size(); i++)
            {
                var midiEvent = playingTrack.Get(i);
                if (sequence.TryGetTicksPerMicrosecond(midiEvent, out var ticks))
                {
                    ticksPerMicrosecond = ticks;
                }

                if (midiEvent.GetTick() == tickPosition)
                {
                    continue;
                }

                var currentTickDuration = 1.0 / ticksPerMicrosecond * (midiEvent.GetTick() - tickPosition);
                if (midiEvent.GetTick() > currentTickPosition)
                {
                    var tickRate = ((double)currentTickPosition - tickPosition) / (midiEvent.GetTick() - tickPosition);
                    return (long)(trackLength + currentTickDuration * tickRate);
                }

                trackLength += currentTickDuration;
                tickPosition = midiEvent.GetTick();
            }

            return (long)trackLength;
        }

        /// <summary>
        /// Get the current microsecond position.
        /// </summary>
        /// <returns>the current tick position</returns>
        public long GetMicrosecondPosition()
        {
            return ConvertTickToMicroseconds(GetTickPosition());
        }

        internal long ConvertMicrosecondsToTick(long microseconds)
        {
            if (playingTrack == null)
            {
                return 0L;
            }

            if (microseconds < 0L)
            {
                return 0L;
            }

            if (microseconds >= GetMicrosecondLength())
            {
                return GetTickLength();
            }

            var tickPosition = 0L;
            var ticksPerMicrosecond = 120f;
            var trackLength = 0.0;
            for (var i = 0; i < playingTrack.Size(); i++)
            {
                var midiEvent = playingTrack.Get(i);
                if (sequence.TryGetTicksPerMicrosecond(midiEvent, out var ticks))
                {
                    ticksPerMicrosecond = ticks;
                }

                if (midiEvent.GetTick() == tickPosition)
                {
                    continue;
                }

                var currentTickDuration = 1.0 / ticksPerMicrosecond * (midiEvent.GetTick() - tickPosition);
                if (trackLength + currentTickDuration > microseconds)
                {
                    var tickRate = (microseconds - trackLength) / currentTickDuration;
                    return (long)((midiEvent.GetTick() - tickPosition) * tickRate) + tickPosition;
                }
                trackLength += currentTickDuration;
                tickPosition = midiEvent.GetTick();
            }

            return tickPosition;
        }

        /// <summary>
        /// Set the current microsecond position.
        /// </summary>
        /// <param name="microseconds">the current microsecond position</param>
        public void SetMicrosecondPosition(long microseconds)
        {
            if (playingTrack == null)
            {
                return;
            }

            if (microseconds < 0L)
            {
                SetTickPosition(0L);
                return;
            }

            if (microseconds >= GetMicrosecondLength())
            {
                SetTickPosition(GetTickLength());
                return;
            }

            var tickPosition = 0L;
            var ticksPerMicrosecond = 120f;
            var trackLength = 0.0;
            for (var i = 0; i < playingTrack.Size(); i++)
            {
                var midiEvent = playingTrack.Get(i);
                if (sequence.TryGetTicksPerMicrosecond(midiEvent, out var ticks))
                {
                    ticksPerMicrosecond = ticks;
                }

                if (midiEvent.GetTick() == tickPosition)
                {
                    continue;
                }

                var currentTickDuration = 1.0 / ticksPerMicrosecond * (midiEvent.GetTick() - tickPosition);
                if (trackLength + currentTickDuration > microseconds)
                {
                    var tickRate = (microseconds - trackLength) / currentTickDuration;
                    SetTickPosition((long)((midiEvent.GetTick() - tickPosition) * tickRate) + tickPosition);
                    break;
                }
                trackLength += currentTickDuration;
                tickPosition = midiEvent.GetTick();
            }
        }

        /// <summary>
        /// Get the <see cref="Sequence"/> length in microseconds.
        /// </summary>
        /// <returns>the <see cref="Sequence"/> length in microseconds</returns>
        public long GetMicrosecondLength()
        {
            return sequence.GetMicrosecondLength();
        }

        /// <summary>
        /// Get the <see cref="Sequence"/>
        /// </summary>
        /// <returns>the <see cref="Sequence"/></returns>
        public Sequence GetSequence()
        {
            return sequence;
        }

        /// <summary>
        /// Load a <see cref="Sequence"/> from stream.
        /// </summary>
        /// <param name="stream">sequence source</param>
        public void SetSequence(Stream stream)
        {
            SetSequence(MidiSystem.ReadSequence(stream));
        }

        /// <summary>
        /// Load a <see cref="Sequence"/> from stream, with ignoring SMF errors
        /// </summary>
        /// <param name="stream">sequence source</param>
        /// <param name="ignoreErrors">true if ignore SMF errors</param>
        public void SetSequence(Stream stream, bool ignoreErrors)
        {
            SetSequence(MidiSystem.ReadSequence(stream, ignoreErrors));
        }

        /// <summary>
        /// Set the <see cref="Sequence"/> for the <see cref="ISequencer"/>
        /// </summary>
        /// <param name="sourceSequence">the <see cref="Sequence"/></param>
        public void SetSequence(Sequence sourceSequence)
        {
            sequence = sourceSequence;

            if (sourceSequence != null)
            {
                needRefreshPlayingTrack = true;
                sequencerThread.RefreshPlayingTrack();
                SetTickPosition(0);
            }
        }

        /// <summary>
        /// Get the <see cref="SyncMode"/> for slave.
        /// </summary>
        /// <returns>the <see cref="SyncMode"/> for slave.</returns>
        public SyncMode GetSlaveSyncMode()
        {
            return slaveSyncMode;
        }

        /// <summary>
        /// Set the <see cref="SyncMode"/> for slave.
        /// </summary>
        /// <param name="sync">sync the <see cref="SyncMode"/> for slave.</param>
        public void SetSlaveSyncMode(SyncMode sync)
        {
            foreach (var availableMode in GetSlaveSyncModes())
            {
                if (Equals(availableMode, sync))
                {
                    slaveSyncMode = sync;
                }
            }
        }

        /// <summary>
        /// Get the available <see cref="SyncMode"/> for slave.
        /// </summary>
        /// <returns>the available <see cref="SyncMode"/> for slave.</returns>
        public SyncMode[] GetSlaveSyncModes()
        {
            return SlaveSyncModes;
        }

        /// <summary>
        /// Get the tempo factor.
        /// </summary>
        /// <returns>the tempo factor</returns>
        public float GetTempoFactor()
        {
            return tempoFactor;
        }

        /// <summary>
        /// Set the tempo factor. This method don't change <see cref="Sequence"/>'s tempo.
        /// </summary>
        /// <param name="factor">
        /// <ul>
        ///     <li>1.0f : the normal tempo</li>
        ///     <li>0.5f : half slow tempo</li>
        ///     <li>2.0f : 2x fast tempo</li>
        /// </ul>
        /// </param>
        public void SetTempoFactor(float factor)
        {
            if (factor <= 0.0f)
            {
                throw new ArgumentException("The tempo factor must be larger than 0f.");
            }

            tempoFactor = factor;
        }

        /// <summary>
        /// Get the tempo in the Beats per minute.
        /// </summary>
        /// <returns>the tempo in the Beats per minute.</returns>
        public float GetTempoInBpm()
        {
            return tempoInBpm;
        }

        /// <summary>
        /// Set the tempo in the Beats per minute.
        /// </summary>
        /// <param name="bpm">the tempo in the Beats per minute</param>
        public void SetTempoInBpm(float bpm)
        {
            tempoInBpm = bpm;
        }

        /// <summary>
        /// Get the tempos in the microseconds per quarter note.
        /// </summary>
        /// <returns>the tempos in the microseconds per quarter note</returns>
        public float GetTempoInMpq()
        {
            return 60000000.0f / tempoInBpm;
        }

        /// <summary>
        /// Set the tempos in the microseconds per quarter note.
        /// </summary>
        /// <param name="mpq">the tempos in the microseconds per quarter note</param>
        public void SetTempoInMpq(float mpq)
        {
            tempoInBpm = 60000000.0f / mpq;
        }

        /// <summary>
        /// Get the <see cref="Sequence"/> length in ticks.
        /// </summary>
        /// <returns>the <see cref="Sequence"/> length in ticks</returns>
        public long GetTickLength()
        {
            if (sequence == null)
            {
                return 0;
            }

            return sequence.GetTickLength();
        }

        /// <summary>
        /// Get the current tick position.
        /// </summary>
        /// <returns>the current tick position</returns>
        public long GetTickPosition()
        {
            if (sequencerThread == null)
            {
                return 0;
            }

            return sequencerThread.GetTickPosition();
        }

        /// <summary>
        /// Set the current tick position.
        /// </summary>
        /// <param name="tick">tick the current tick position</param>
        public void SetTickPosition(long tick)
        {
            if (sequencerThread != null)
            {
                sequencerThread.SetTickPosition(tick);
            }
        }

        /// <summary>
        /// Get if the track is mute on the playback.
        /// </summary>
        /// <param name="track">the track number</param>
        /// <returns>true if the track is mute on the playback</returns>
        public bool GetTrackMute(int track)
        {
            return trackMute.ContainsKey(track) && trackMute[track];
        }

        /// <summary>
        /// Set the track to mute on the playback.
        /// </summary>
        /// <param name="track">the track number</param>
        /// <param name="mute">true to set mute the track</param>
        public void SetTrackMute(int track, bool mute)
        {
            trackMute[track] = mute;
        }

        /// <summary>
        /// Get if the track is solo on the playback.
        /// </summary>
        /// <param name="track">the track number</param>
        /// <returns>true if the track is solo on the playback.</returns>
        public bool GetTrackSolo(int track)
        {
            return trackSolo.ContainsKey(track) && trackSolo[track];
        }

        /// <summary>
        /// Set track to solo on the playback.
        /// </summary>
        /// <param name="track">the track number</param>
        /// <param name="solo">true to set solo the track</param>
        public void SetTrackSolo(int track, bool solo)
        {
            trackSolo[track] = solo;
        }

        /// <summary>
        /// Get the playback channel mute status
        /// </summary>
        /// <param name="channel">the channel(0-15)</param>
        /// <returns>true: the channel is muted</returns>
        public bool GetChannelMute(int channel)
        {
            return channelMute.ContainsKey(channel) && channelMute[channel];
        }

        /// <summary>
        /// Set the playback channel mute status
        /// </summary>
        /// <param name="channel">the channel(0-15)</param>
        /// <param name="mute">true: the channel is muted</param>
        public void SetChannelMute(int channel, bool mute)
        {
            channelMute[channel] = mute;
        }

        internal bool HasChannelSolo()
        {
            return channelSolo.ContainsValue(true);
        }

        /// <summary>
        /// Get the playback channel solo status
        /// </summary>
        /// <param name="channel">the channel(0-15)</param>
        /// <returns>true: the channel is solo</returns>
        public bool GetChannelSolo(int channel)
        {
            return channelSolo.ContainsKey(channel) && channelSolo[channel];
        }

        /// <summary>
        /// Set the playback channel solo status
        /// </summary>
        /// <param name="channel">the channel(0-15)</param>
        /// <param name="solo">true: the channel is solo</param>
        public void SetChannelSolo(int channel, bool solo)
        {
            channelSolo[channel] = solo;
        }

        /// <summary>
        /// Set the <see cref="Track"/> to disable recording
        /// </summary>
        /// <param name="track">track the <see cref="Track"/> to disable recording</param>
        public void RecordDisable(Track track)
        {
            if (track == null)
            {
                // disable all track
                recordEnable.Clear();
            }
            else
            {
                // disable specified track
                var trackRecordEnable = recordEnable[track];
                if (trackRecordEnable != null)
                {
                    recordEnable.Remove(track);
                }
            }
        }

        /// <summary>
        /// Set the <see cref="Track"/> to enable recording on the specified channel.
        /// </summary>
        /// <param name="track">the <see cref="Track"/></param>
        /// <param name="channel">the channel, 0-15</param>
        public void SetRecordEnable(Track track, int channel)
        {
            var trackRecordEnable = recordEnable.ContainsKey(track) ? recordEnable[track] : new HashSet<int>();
 
            if (channel == -1)
            {
                // record to the all channels
                for (var i = 0; i < 16; i++)
                {
                    trackRecordEnable.Add(i);
                }
            }
            else if (channel >= 0 && channel < 16)
            {
                trackRecordEnable.Add(channel);
            }

            recordEnable[track] = trackRecordEnable;
        }

        /// <summary>
        /// Start recording (starting at current sequencer position)
        /// Current <see cref="Sequence"/>'s events are sent to the all <see cref="ITransmitter"/>s.
        /// Received events are also sent to the all <see cref="ITransmitter"/>s.
        /// </summary>
        public void StartRecording()
        {
            if (sequencerThread != null)
            {
                sequencerThread.StartRecording();
                sequencerThread.StartPlaying();
            }
        }

        /// <summary>
        /// Get if the <see cref="ISequencer"/> is recording.
        /// </summary>
        /// <returns>true if the <see cref="ISequencer"/> is recording</returns>
        public bool GetIsRecording()
        {
            if (sequencerThread == null)
            {
                return false;
            }

            return sequencerThread.IsRecording;
        }

        /// <summary>
        /// Stop recording. Playing continues.
        /// </summary>
        public void StopRecording()
        {
            // stop recording
            if (sequencerThread != null)
            {
                sequencerThread.StopRecording();
            }
        }

        /// <summary>
        /// Start playing (starting at current sequencer position)
        /// </summary>
        public void Start()
        {
            // start playing
            if (sequencerThread != null)
            {
                sequencerThread.StartPlaying();
            }
        }

        /// <summary>
        /// Get if the <see cref="ISequencer"/> is playing OR recording.
        /// </summary>
        /// <returns>true if the <see cref="ISequencer"/> is playing OR recording</returns>
        public bool GetIsRunning()
        {
            if (sequencerThread == null)
            {
                return false;
            }

            return sequencerThread.IsRunning;
        }

        /// <summary>
        /// Stop playing AND recording.
        /// </summary>
        public void Stop()
        {
            // stop playing AND recording
            if (sequencerThread != null)
            {
                sequencerThread.StopRecording();
                sequencerThread.StopPlaying();
            }
        }

        private static double CurrentTimeMillis()
        {
            return (DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        /// <summary>
        /// convert parameter from microseconds to tick
        /// </summary>
        /// <returns>ticks per microsecond, NaN: sequence is null</returns>
        private float GetTicksPerMicrosecond()
        {
            if (sequence == null)
            {
                return float.NaN;
            }

            float ticksPerMicrosecond;
            if (Sequence.DivisionTypeEquals(sequence.GetDivisionType(), Sequence.Ppq))
            {
                // PPQ : tempoInBPM / 60f * resolution / 1000000 ticks per microsecond
                ticksPerMicrosecond = tempoInBpm / 60.0f * sequence.GetResolution() / 1000000.0f;
            }
            else
            {
                // SMPTE : divisionType * resolution / 1000000 ticks per microsecond
                ticksPerMicrosecond = sequence.GetDivisionType() * sequence.GetResolution() / 1000000.0f;
            }

            return ticksPerMicrosecond;
        }

        private class MidiEventRecordingReceiver : IReceiver
        {
            private readonly SequencerImpl sequencer;

            internal MidiEventRecordingReceiver(SequencerImpl sequencer)
            {
                this.sequencer = sequencer;
            }

            public void Send(MidiMessage message, long timeStamp)
            {
                if (sequencer == null || sequencer.sequencerThread == null)
                {
                    // sequencerThread already closed
                    return;
                }

                if (sequencer.sequencerThread.IsRecording)
                {
                    sequencer.recordingTrack.Add(
                        new MidiEvent(
                            message,
                            (long)(sequencer.recordStartedTick +
                                   (CurrentTimeMillis() - sequencer.recordingStartedTime) * 1000.0f *
                                   sequencer.GetTicksPerMicrosecond())));
                }

                sequencer.sequencerThread.FireEventListeners(message);
            }

            public void Close()
            {
                // do nothing
            }
        }

        private class SequencerThread
        {
            internal volatile bool IsRecording;

            internal volatile bool IsRunning;

            private SequencerImpl sequencer;
            private long tickPosition;
            private bool[][] playingNotes;

            /// <summary>
            /// Thread / Coroutine for this Sequencer
            /// </summary>
            /// <param name="sourceSequencer">the <see cref="SequencerImpl"/></param>
            /// <param name="onOpened">Called on sequencer opened</param>
            public 
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                IEnumerator
#else
                void
#endif
                StartSequencerThread(SequencerImpl sourceSequencer, Action onOpened)
            {
                sequencer = sourceSequencer;
                RefreshPlayingTrack();

                onOpened();

                playingNotes = new bool[16][];
                for (var i = 0; i < 16; i++)
                {
                    playingNotes[i] = new bool[128];
                }

                // playing
                while (sequencer.isOpen)
                {
                    // wait for being notified
                    while (!IsRunning && sequencer.isOpen)
                    {
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                        yield return null;
#else
                        lock (sequencer.thread)
                        {
                            Monitor.Wait(sequencer.thread);
                        }
#endif
                    }

                    if (sequencer.playingTrack == null)
                    {
                        if (sequencer.needRefreshPlayingTrack)
                        {
                            RefreshPlayingTrack();
                        }

                        if (sequencer.playingTrack == null)
                        {
                            continue;
                        }
                    }

#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                    sequencer.OnSequenceStarted?.Invoke();
#else
                    // call from main thread
                    sequencer.asyncOperation.Post(_ =>
                    {
                        sequencer.OnSequenceStarted?.Invoke();
                    }, null);
#endif

                    // process looping
                    var isFirstLoop = true;
                    var loopCount = sequencer.GetLoopCount() == LoopContinuously ? 1 : sequencer.GetLoopCount() + 1;
                    for (var loop = 0; loop < loopCount; loop += sequencer.GetLoopCount() == LoopContinuously ? 0 : 1)
                    {
                        if (sequencer.needRefreshPlayingTrack)
                        {
                            RefreshPlayingTrack();
                        }

                        for (var i = 0; i < sequencer.playingTrack.Size(); i++)
                        {
                            var midiEvent = sequencer.playingTrack.Get(i);
                            var midiMessage = midiEvent.GetMessage();

                            if (sequencer.needRefreshPlayingTrack)
                            {
                                // skip to lastTick
                                if (midiEvent.GetTick() < tickPosition)
                                {
                                    if (midiMessage is MetaMessage metaMessage)
                                    {
                                        // process tempo change message
                                        if (ProcessTempoChange(metaMessage) == false)
                                        {
                                            // not tempo message, process the event
                                            lock (sequencer.receivers)
                                            {
                                                foreach (var receiver in sequencer.receivers)
                                                {
                                                    receiver.Send(metaMessage, 0);
                                                }
                                            }
                                        }
                                    }
                                    else if (midiMessage is SysexMessage)
                                    {
                                        // process system messages
                                        lock (sequencer.receivers)
                                        {
                                            foreach (var receiver in sequencer.receivers)
                                            {
                                                receiver.Send(midiMessage, 0);
                                            }
                                        }
                                    }
                                    else if (midiMessage is ShortMessage shortMessage)
                                    {
                                        // process control change / program change messages
                                        switch (shortMessage.GetCommand())
                                        {
                                            case ShortMessage.NoteOn:
                                            case ShortMessage.NoteOff:
                                                break;
                                            default:
                                                lock (sequencer.receivers)
                                                {
                                                    foreach (var receiver in sequencer.receivers)
                                                    {
                                                        receiver.Send(shortMessage, 0);
                                                    }
                                                }
                                                break;
                                        }
                                    }

                                    continue;
                                }

                                // refresh playingTrack completed
                                sequencer.needRefreshPlayingTrack = false;
                            }

                            // don't skip if GetPlayBeforeLoopOnce() && loop == 0
                            if (((sequencer.GetPlayIntroOnFirstLoop() && !isFirstLoop) || !sequencer.GetPlayIntroOnFirstLoop()) && midiEvent.GetTick() < sequencer.GetLoopStartPoint() ||
                                (sequencer.GetLoopEndPoint() != -1 && midiEvent.GetTick() > sequencer.GetLoopEndPoint()))
                            {
                                if (tickPosition <= sequencer.GetLoopEndPoint() &&
                                    midiEvent.GetTick() > sequencer.GetLoopEndPoint())
                                {
                                    // reached loop end
                                    StopAllPlayingNotes();
                                }

                                // outer loop
                                tickPosition = midiEvent.GetTick();
                                sequencer.tickPositionSetTime = CurrentTimeMillis();
                                isFirstLoop = false;
                                continue;
                            }

                            var eventFireTime = CurrentTimeMillis();
                            if (midiEvent.GetTick() != tickPosition)
                            {
                                var sleepLength = 1.0 / sequencer.GetTicksPerMicrosecond() *
                                    (midiEvent.GetTick() - tickPosition) / 1000 / sequencer.GetTempoFactor();
                                sleepLength -= CurrentTimeMillis() - sequencer.tickPositionSetTime;
                                eventFireTime += sleepLength;
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                                if (sleepLength > UnityEngine.Time.smoothDeltaTime * 1000)
                                {
                                    sleepLength -= sleepLength % (UnityEngine.Time.smoothDeltaTime * 1000.0);
                                    yield return new UnityEngine.WaitForSecondsRealtime((float)(sleepLength / 1000.0));
                                }
#else
                                if (sleepLength > 0f)
                                {
                                    if (!sequencer.isOpen || sequencer.thread == null)
                                    {
                                        break;
                                    }
                                    lock (sequencer.thread)
                                    {
                                        Monitor.Wait(sequencer.thread, (int)sleepLength);
                                    }
                                }
#endif
                            }

                            tickPosition = midiEvent.GetTick();
                            sequencer.tickPositionSetTime = eventFireTime;

                            // pause / resume
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                            while (!IsRunning && sequencer.isOpen)
#else
                            while (!IsRunning && sequencer.isOpen && sequencer.thread != null)
#endif
                            {
                                var pausing = !IsRunning;
                                if (pausing)
                                {
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                                    sequencer.OnSequencePaused?.Invoke();
#else
                                    // call from main thread
                                    sequencer.asyncOperation.Post(_ =>
                                    {
                                        sequencer.OnSequencePaused?.Invoke();
                                    }, null);
#endif
                                }
                                // wait for being notified
                                while (!IsRunning && sequencer.isOpen)
                                {
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                                    yield return null;
#else
                                    lock (sequencer.thread)
                                    {
                                        Monitor.Wait(sequencer.thread);
                                    }
#endif
                                }
                                if (!pausing)
                                {
                                    continue;
                                }

                                if (sequencer.needRefreshPlayingTrack)
                                {
                                    RefreshPlayingTrack();
                                }
                                for (var index = 0; index < sequencer.playingTrack.Size(); index++)
                                {
                                    if (sequencer.playingTrack.Get(index).GetTick() >= tickPosition)
                                    {
                                        i = index;
                                        sequencer.tickPositionSetTime = CurrentTimeMillis();
#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                                        sequencer.OnSequenceStarted?.Invoke();
#else
                                        // call from main thread
                                        sequencer.asyncOperation.Post(_ =>
                                        {
                                            sequencer.OnSequenceStarted?.Invoke();
                                        }, null);
#endif
                                        break;
                                    }
                                }
                                if (sequencer.needRefreshPlayingTrack) {
                                    // `i` will increment after reaching continue
                                    i--;
                                }
                            }

                            if (!sequencer.isOpen)
                            {
                                break;
                            }

                            if (sequencer.needRefreshPlayingTrack)
                            {
                                continue;
                            }

                            // process tempo change message
                            if (midiMessage is MetaMessage message)
                            {
                                if (ProcessTempoChange(message))
                                {
                                    FireEventListeners(message);

                                    // do not send tempo message to the receivers.
                                    continue;
                                }
                            }

                            // apply channel mute / solo
                            if (midiMessage is ShortMessage channelMessage)
                            {
                                var channel = channelMessage.GetChannel();
                                if (sequencer.HasChannelSolo() && !sequencer.GetChannelSolo(channel))
                                {
                                    continue;
                                }

                                if (sequencer.GetChannelMute(channel))
                                {
                                    continue;
                                }
                            }

                            // send MIDI events
                            lock (sequencer.receivers)
                            {
                                foreach (var receiver in sequencer.receivers)
                                {
                                    receiver.Send(midiMessage, 0);
                                }
                            }

                            FireEventListeners(midiMessage);

                            // store playing note status
                            if (midiMessage is ShortMessage noteMessage)
                            {
                                if (noteMessage.GetCommand() == ShortMessage.NoteOn)
                                {
                                    playingNotes[noteMessage.GetChannel()][noteMessage.GetData1()] = noteMessage.GetData2() > 0;
                                }
                                else if (noteMessage.GetCommand() == ShortMessage.NoteOff)
                                {
                                    playingNotes[noteMessage.GetChannel()][noteMessage.GetData1()] = false;
                                }
                            }

#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                            sequencer.OnSequenceTimeUpdated?.Invoke(sequencer.GetMicrosecondPosition() / 1000000f);
#else
                            // call from main thread
                            sequencer.asyncOperation.Post(_ =>
                            {
                                sequencer.OnSequenceTimeUpdated?.Invoke(sequencer.GetMicrosecondPosition() / 1000000f);
                            }, null);
#endif
                        }
                    }

                    // loop end
                    IsRunning = false;
                    sequencer.runningStoppedTime = CurrentTimeMillis();

                    // stop all notes
                    StopAllPlayingNotes();

#if UNITY_WEBGL || !UNITY_5_3_OR_NEWER
                    sequencer.OnSequenceFinished?.Invoke();
#else
                    // call from main thread
                    sequencer.asyncOperation.Post(_ =>
                    {
                        sequencer.OnSequenceFinished?.Invoke();
                    }, null);
#endif
                }
            }

            /// <summary>
            /// Get current tick position
            /// </summary>
            /// <returns>current tick position</returns>
            internal long GetTickPosition()
            {
                if (IsRunning)
                {
                    // running
                    return (long)(tickPosition + (CurrentTimeMillis() - sequencer.tickPositionSetTime) * 1000.0f *
                        sequencer.GetTicksPerMicrosecond());
                }

                // stopping
                return (long)(tickPosition + (sequencer.runningStoppedTime - sequencer.tickPositionSetTime) * 1000.0f *
                    sequencer.GetTicksPerMicrosecond());
            }

            /// <summary>
            /// Set current tick position
            /// </summary>
            /// <param name="tick">current tick position</param>
            internal void SetTickPosition(long tick)
            {
                tickPosition = tick;
                if (IsRunning)
                {
                    sequencer.tickPositionSetTime = CurrentTimeMillis();
                }
            }

            /// <summary>
            /// Start recording
            /// </summary>
            internal void StartRecording()
            {
                if (IsRecording)
                {
                    // already recording
                    return;
                }

                sequencer.recordingTrack = sequencer.sequence.CreateTrack();
                sequencer.SetRecordEnable(sequencer.recordingTrack, -1);
                sequencer.recordingStartedTime = CurrentTimeMillis();
                sequencer.recordStartedTick = GetTickPosition();
                IsRecording = true;
            }

            /// <summary>
            /// Stop recording
            /// </summary>
            internal void StopRecording()
            {
                if (IsRecording == false)
                {
                    // already stopped
                    return;
                }

                var recordEndedTime = CurrentTimeMillis();
                IsRecording = false;

                var eventToRemoval = new HashSet<MidiEvent>();
                foreach (var track in sequencer.sequence.GetTracks())
                {
                    if (track == sequencer.recordingTrack)
                    {
                        continue;
                    }

                    HashSet<int> recordEnableChannels = null;
                    if (sequencer.recordEnable.ContainsKey(track))
                    {
                        recordEnableChannels = sequencer.recordEnable[track];
                    }

                    // remove events while recorded time
                    eventToRemoval.Clear();
                    for (var trackIndex = 0; trackIndex < track.Size(); trackIndex++)
                    {
                        var midiEvent = track.Get(trackIndex);
                        if (isRecordable(recordEnableChannels, midiEvent) &&
                            midiEvent.GetTick() >= sequencer.recordingStartedTime &&
                            midiEvent.GetTick() <= recordEndedTime)
                        {
                            // recorded time
                            eventToRemoval.Add(midiEvent);
                        }
                    }

                    foreach (var anEvent in eventToRemoval)
                    {
                        track.Remove(anEvent);
                    }

                    // add recorded events
                    for (var eventIndex = 0; eventIndex < sequencer.recordingTrack.Size(); eventIndex++)
                    {
                        var midiEvent = sequencer.recordingTrack.Get(eventIndex);
                        if (isRecordable(recordEnableChannels, midiEvent))
                        {
                            track.Add(midiEvent);
                        }
                    }

                    Track.TrackUtils.SortEvents(track);
                }

                // refresh playingTrack
                sequencer.needRefreshPlayingTrack = true;
            }

            /// <summary>
            /// Start playing
            /// </summary>
            internal void StartPlaying()
            {
                if (IsRunning)
                {
                    // already playing
                    return;
                }

                if (sequencer == null)
                {
                    throw new MidiUnavailableException(
                        "sequencer == null, please wait for SequencerImpl will be opened.");
                }

                sequencer.tickPositionSetTime = CurrentTimeMillis();
                IsRunning = true;

#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
                lock (sequencer.thread)
                {
                    Monitor.PulseAll(sequencer.thread);
                }
#endif
            }

            /// <summary>
            /// Stop playing
            /// </summary>
            internal void StopPlaying()
            {
                if (IsRunning == false)
                {
#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
                    // already stopping
                    if (sequencer != null && sequencer.thread != null)
                    {
                        lock (sequencer.thread)
                        {
                            Monitor.PulseAll(sequencer.thread);
                        }
                    }
#endif

                    return;
                }

                IsRunning = false;
                sequencer.runningStoppedTime = CurrentTimeMillis();

#if !UNITY_WEBGL && UNITY_5_3_OR_NEWER
                // force stop sleeping
                lock (sequencer.thread)
                {
                    Monitor.PulseAll(sequencer.thread);
                }
#endif
                StopAllPlayingNotes();
            }

            private void StopAllPlayingNotes()
            {
                var midiMessage = new ShortMessage();
                for (var channel = 0; channel < 16; channel++)
                {
                    for (var note = 0; note < 128; note++)
                    {
                        // send NoteOff event to playing notes
                        if (playingNotes[channel][note])
                        {
                            midiMessage.SetMessage(ShortMessage.NoteOff | channel, note, 0);
                            lock (sequencer.receivers)
                            {
                                foreach (var receiver in sequencer.receivers)
                                {
                                    receiver.Send(midiMessage, 0);
                                }
                            }
                            
                            playingNotes[channel][note] = false;
                        }
                    }
                }
            }

            private void ResetAllControllers()
            {
                var midiMessage = new ShortMessage();
                for (var channel = 0; channel < 16; channel++)
                {
                    lock (sequencer.receivers)
                    {
                        foreach (var receiver in sequencer.receivers)
                        {
                            // The modulation wheel (controller 1), the hold pedal (controller 64), portamento pedal (controller 65), sostenuto pedal (controller 66), and soft pedal (controller 67) are set to zero;
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 1, 0);
                            receiver.Send(midiMessage, 0);
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 64, 0);
                            receiver.Send(midiMessage, 0);
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 65, 0);
                            receiver.Send(midiMessage, 0);
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 66, 0);
                            receiver.Send(midiMessage, 0);
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 67, 0);
                            receiver.Send(midiMessage, 0);
                            // Registered and nonregistered parameter numbers (controllers 98 through 101) are set to 127 (but registered and nonregistered parameters are not actually reset; see below);
                            for (var key = 98; key <= 101; key++)
                            {
                                midiMessage.SetMessage(ShortMessage.ControlChange | channel, key, 127);
                                receiver.Send(midiMessage, 0);
                            }
                            // The channel pressure and the key pressure are set to zero;
                            midiMessage.SetMessage(ShortMessage.ChannelPressure | channel, 0, 0);
                            receiver.Send(midiMessage, 0);
                            for (var key = 0; key < 127; key++)
                            {
                                midiMessage.SetMessage(ShortMessage.PolyPressure | channel, key, 0);
                                receiver.Send(midiMessage, 0);
                            }
                            // Expression (controller 11) is set to 127; and
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 11, 127);
                            receiver.Send(midiMessage, 0);
                            // The pitch wheel is set to center (typically 64, but could be zero).
                            midiMessage.SetMessage(ShortMessage.PitchBend | channel, 0, 0x40);
                            receiver.Send(midiMessage, 0);

                            // At the same time, General MIDI 1 recommends that a number of other controllers are not reset,
                            // including: bank select (controllers 0 and 32),
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 0, 0);
                            receiver.Send(midiMessage, 0);
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 32, 0);
                            receiver.Send(midiMessage, 0);
                            // volume (controller 7),
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 7, 0);
                            receiver.Send(midiMessage, 0);
                            // pan (controller 10),
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 10, 64);
                            receiver.Send(midiMessage, 0);
                            // effects (controllers 91 through 95), sound controllers (controllers 70 through 79),
                            // channel mode controllers (controllers 120 through 127),
                            for (var key = 70; key <= 79; key++)
                            {
                                midiMessage.SetMessage(ShortMessage.ControlChange | channel, key, 0);
                                receiver.Send(midiMessage, 0);
                            }
                            for (var key = 91; key <= 95; key++)
                            {
                                midiMessage.SetMessage(ShortMessage.ControlChange | channel, key, 0);
                                receiver.Send(midiMessage, 0);
                            }
                            for (var key = 120; key <= 127; key++)
                            {
                                midiMessage.SetMessage(ShortMessage.ControlChange | channel, key, 0);
                                receiver.Send(midiMessage, 0);
                            }
                            // the program,
                            midiMessage.SetMessage(ShortMessage.ProgramChange | channel, 0, 0);
                            receiver.Send(midiMessage, 0);
                            // and registered and nonregistered parameters.

                            // Reset All Controllers
                            midiMessage.SetMessage(ShortMessage.ControlChange | channel, 121, 0);
                            receiver.Send(midiMessage, 0);
                        }
                    }
                }
            }

            /// <summary>
            /// Process the specified <see cref="MidiMessage"/> and fire events to registered event listeners.
            /// </summary>
            /// <param name="message">the <see cref="MidiMessage"/></param>
            internal void FireEventListeners(MidiMessage message)
            {
                if (message is MetaMessage metaMessage)
                {
                    lock (sequencer.metaEventListeners)
                    {
                        foreach (var metaEventListener in sequencer.metaEventListeners)
                        {
                            metaEventListener.Meta(metaMessage);
                        }
                    }
                }
                else if (message is ShortMessage shortMessage)
                {
                    if (shortMessage.GetCommand() == ShortMessage.ControlChange)
                    {
                        lock (sequencer.controllerEventListenerMap)
                        {
                            var eventListeners = sequencer.controllerEventListenerMap[shortMessage.GetData1()];
                            if (eventListeners != null)
                            {
                                foreach (var eventListener in eventListeners)
                                {
                                    eventListener.ControlChange(shortMessage);
                                }
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Process the tempo change events
            /// </summary>
            /// <param name="metaMessage">the <see cref="MetaMessage"/></param>
            /// <returns>true if the tempo changed</returns>
            private bool ProcessTempoChange(MetaMessage metaMessage)
            {
                if (metaMessage.GetLength() == 6 && metaMessage.GetStatus() == MetaMessage.Meta)
                {
                    var message = metaMessage.GetMessage();
                    if (message != null && (message[1] & 0xff) == MetaMessage.Tempo && message[2] == 3)
                    {
                        var tempo = (message[5] & 0xff) | //
                                    ((message[4] & 0xff) << 8) | //
                                    ((message[3] & 0xff) << 16);

                        sequencer.SetTempoInMpq(tempo);
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Merge current sequence's track to play
            /// </summary>
            internal void RefreshPlayingTrack()
            {
                if (sequencer.sequence == null)
                {
                    return;
                }

                var tracks = sequencer.sequence.GetTracks();
                if (tracks.Length > 0)
                {
                    try
                    {
                        // at first, merge all track into one track
                        sequencer.playingTrack =
                            Track.TrackUtils.MergeSequenceToTrack(sequencer, sequencer.recordEnable);
                    }
                    catch (InvalidMidiDataException)
                    {
                        // ignore exception
                    }
                }
            }

            /// <summary>
            /// Check if the event can be recorded
            /// </summary>
            /// <param name="recordEnableChannels">the channel IDs that are able to record.</param>
            /// <param name="midiEvent">the <see cref="MidiEvent"/></param>
            /// <returns>true if the event can be recorded</returns>
            private bool isRecordable(HashSet<int> recordEnableChannels, MidiEvent midiEvent)
            {
                if (recordEnableChannels == null)
                {
                    return false;
                }

                if (recordEnableChannels.Contains(-1))
                {
                    return true;
                }

                var status = midiEvent.GetMessage().GetStatus();
                switch (status & ShortMessage.MaskEvent)
                {
                    // channel messages
                    case ShortMessage.NoteOff:
                    case ShortMessage.NoteOn:
                    case ShortMessage.PolyPressure:
                    case ShortMessage.ControlChange:
                    case ShortMessage.ProgramChange:
                    case ShortMessage.ChannelPressure:
                    case ShortMessage.PitchBend:
                        // recorded Track and channel
                        return recordEnableChannels.Contains(status & ShortMessage.MaskChannel);
                    // exclusive messages
                    default:
                        return true;
                }
            }
        }
    }
}