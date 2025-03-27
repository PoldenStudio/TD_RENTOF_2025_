using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace jp.kshoji.unity.midi
{
#region MpeCommon
    internal static class MpeUtils
    {
        /// <summary>
        /// Setup MPE zone configuration
        /// </summary>
        /// <param name="mpeStatus">the mpe status</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="memberChannelCount">0-15</param>
        /// <returns>true: the other zone has changed</returns>
        internal static bool SetupZone(MpeStatus mpeStatus, int managerChannel, int memberChannelCount)
        {
            var anotherZoneChanged = false;
            if (managerChannel == 0)
            {
                // lower zone
                for (var i = 0; i < 16; i++)
                {
                    mpeStatus.lowerZone.memberChannels[i] = null;
                }

                if (memberChannelCount > 0)
                {
                    // check already defined another zone
                    if (mpeStatus.upperZone.memberChannels[15] != null && mpeStatus.upperZone.memberChannels[15].isManagerChannel &&
                        mpeStatus.upperZone.memberChannelCount > 0)
                    {
                        // has upper zone members
                        if (memberChannelCount + mpeStatus.upperZone.memberChannelCount > 14)
                        {
                            Debug.Log(
                                $"Invalid zone member channel count: {memberChannelCount}. Already defined upper zone, member channel count: {mpeStatus.upperZone.memberChannelCount}.");
                            // shrink upper zone
                            var upperMemberChannelCount = 14 - memberChannelCount;
                            if (upperMemberChannelCount < 1)
                            {
                                // upper zone will be removed
                                for (var i = 0; i < 16; i++)
                                {
                                    mpeStatus.upperZone.memberChannels[i] = null;
                                }
                            }
                            else
                            {
                                // reduce upper zone channels
                                for (var i = 15 - upperMemberChannelCount; i < 15; i++)
                                {
                                    mpeStatus.upperZone.memberChannels[i].channel = i;
                                }
                                mpeStatus.upperZone.memberChannelCount = upperMemberChannelCount;
                            }

                            anotherZoneChanged = true;
                        }
                    }

                    // lower manager channel
                    mpeStatus.lowerZone.memberChannelCount = memberChannelCount;
                    mpeStatus.lowerZone.memberChannels[0] = new MemberChannelStatus
                    {
                        isManagerChannel = true,
                        channel = 0,
                    };

                    for (var i = 1; i < memberChannelCount + 1; i++)
                    {
                        mpeStatus.lowerZone.memberChannels[i] = new MemberChannelStatus
                        {
                            channel = i
                        };
                    }
                }
            }
            else // if (managerChannel == 15)
            {
                // upper zone
                for (var i = 0; i < 16; i++)
                {
                    mpeStatus.upperZone.memberChannels[i] = null;
                }

                if (memberChannelCount > 0)
                {
                    // check already defined another zone
                    if (mpeStatus.lowerZone.memberChannels[0] != null && mpeStatus.lowerZone.memberChannels[0].isManagerChannel &&
                        mpeStatus.lowerZone.memberChannelCount > 0)
                    {
                        // has lower zone members
                        if (memberChannelCount + mpeStatus.lowerZone.memberChannelCount > 14)
                        {
                            Debug.Log($"Invalid zone member channel count: {memberChannelCount}. Already defined lower zone, member channel count: {mpeStatus.lowerZone.memberChannelCount}.");
                            // shrink lower zone
                            var lowerMemberChannelCount = 14 - memberChannelCount;
                            if (lowerMemberChannelCount < 1)
                            {
                                // lower zone will be removed
                                for (var i = 0; i < 16; i++)
                                {
                                    mpeStatus.lowerZone.memberChannels[i] = null;
                                }
                            }
                            else
                            {
                                // reduce lower zone channels
                                for (var i = lowerMemberChannelCount + 1; i < 16; i++)
                                {
                                    mpeStatus.lowerZone.memberChannels[i] = null;
                                }
                                mpeStatus.lowerZone.memberChannelCount = lowerMemberChannelCount;
                            }

                            anotherZoneChanged = true;
                        }
                    }

                    // upper manager channel
                    mpeStatus.upperZone.memberChannelCount = memberChannelCount;
                    mpeStatus.upperZone.memberChannels[15] = new MemberChannelStatus
                    {
                        isManagerChannel = true,
                        channel = 15,
                    };

                    for (var i = 15 - memberChannelCount; i < 15; i++)
                    {
                        mpeStatus.upperZone.memberChannels[i] = new MemberChannelStatus
                        {
                            channel = i
                        };
                    }
                }
            }

            return anotherZoneChanged;
        }
    }

    internal class MpeStatus
    {
        internal ZoneStatus lowerZone;
        internal ZoneStatus upperZone;

        internal MpeStatus()
        {
            lowerZone = new ZoneStatus();
            lowerZone.managerChannel = 0;
            lowerZone.memberChannelCount = 0;
            lowerZone.memberChannels = new MemberChannelStatus[16];
            upperZone = new ZoneStatus();
            upperZone.managerChannel = 15;
            upperZone.memberChannelCount = 0;
            upperZone.memberChannels = new MemberChannelStatus[16];
        }

        internal ZoneStatus GetZone(int channel)
        {
            var lowerChannel =
                lowerZone.memberChannels.FirstOrDefault(n => n != null && n.channel == channel);
            if (lowerChannel != null)
            {
                return lowerZone;
            }

            var upperChannel =
                upperZone.memberChannels.FirstOrDefault(n => n != null && n.channel == channel);
            if (upperChannel != null)
            {
                return upperZone;
            }

            // no zone defined for the channel
            return null;
        }
    }

    internal class ZoneStatus
    {
        private const int PitchCenter = 8192;

        internal int managerChannel;

        internal int memberChannelCount = 0;

        /// <summary>
        /// MIDI Mode
        /// </summary>
        internal int midiMode = 3;

        /// <summary>
        /// Control Change values for zone
        /// </summary>
        internal Dictionary<int, int> controllers = new Dictionary<int, int>();

        /// <summary>
        /// Polyphonic Aftertouch values for zone
        /// </summary>
        internal Dictionary<int, int> polyphonicAftertouches = new Dictionary<int, int>();

        /// <summary>
        /// Channel Aftertouch values for zone
        /// </summary>
        internal int channelAftertouch = 127;

        /// <summary>
        /// PitchBend value for zone
        /// </summary>
        internal int pitch = PitchCenter;

        /// <summary>
        /// Program for zone
        /// </summary>
        internal int program;

        /// <summary>
        /// Member channel status
        /// </summary>
        internal MemberChannelStatus[] memberChannels;
    }

    internal class MemberChannelStatus
    {
        private const int PitchCenter = 8192;

        internal bool isManagerChannel = false;
        internal int channel;
        internal int? lastNote;
        internal HashSet<int> notes = new HashSet<int>();
        internal int pitch = PitchCenter;
        internal int program;
        internal double? noteOffTiming;
        internal double? noteOnTiming;
        internal Dictionary<int, int> controllers = new Dictionary<int, int>();
        internal Dictionary<int, int> polyphonicAftertouches = new Dictionary<int, int>();
        internal int channelAftertouch = 127;
    }

    internal class ChannelStatus
    {
        internal Dictionary<int, int> controllers = new Dictionary<int, int>();
    }
#endregion

#region MpeInput
    /// <summary>
    /// MIDI input event handler for MIDI Polyphonic Expression 
    /// </summary>
    internal class MpeInputEventHandler : IMidiAllEventsHandler
    {
        /// <summary>
        /// MPE configurations map
        /// </summary>
        private static Dictionary<string, MpeStatus> mpeStatusMap = new Dictionary<string, MpeStatus>();

        /// <summary>
        /// channel status map
        /// </summary>
        private static Dictionary<string, ChannelStatus[]> channelsMap = new Dictionary<string, ChannelStatus[]>();

        public void OnMidiNoteOn(string deviceId, int group, int channel, int note, int velocity)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpeNoteOnEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, note, velocity },
                }, (handler, message) =>
                {
                    handler.OnMpeNoteOn(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
                });
            }
        }

        public void OnMidiNoteOff(string deviceId, int group, int channel, int note, int velocity)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpeNoteOffEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, note, velocity },
                }, (handler, message) =>
                {
                    handler.OnMpeNoteOff(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
                });
            }
        }
        
        public void OnMidiPolyphonicAftertouch(string deviceId, int group, int channel, int note, int pressure)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpePolyphonicAftertouchEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, note, pressure },
                }, (handler, message) =>
                {
                    handler.OnMpePolyphonicAftertouch(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
                });
            }
        }

        public void OnMidiProgramChange(string deviceId, int group, int channel, int program)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpeProgramChangeEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, program },
                }, (handler, message) =>
                {
                    handler.OnMpeProgramChange(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1]);
                });
            }
        }

        public void OnMidiControlChange(string deviceId, int group, int channel, int function, int value)
        {
            ChannelStatus[] channels;
            lock (channelsMap)
            {
                if (!channelsMap.TryGetValue(deviceId, out channels))
                {
                    // managerChannel is 0 or 15
                    channels = new ChannelStatus[16];
                    channels[0] = new ChannelStatus();
                    channels[15] = new ChannelStatus();

                    channelsMap[deviceId] = channels;
                }
            }

            if (channel == 0 || channel == 15)
            {
                // Store controller value for zone configuration
                channels[channel].controllers[function] = value;

                // MPE Configuration Message (MCM)
                // Message Format: [Bn 64 06] [Bn 65 00] [Bn 06 <mm>]
                if (channels[channel].controllers.TryGetValue(0x64, out var value0x64) &&
                    channels[channel].controllers.TryGetValue(0x65, out var value0x65))
                {
                    if (value0x64 == 6 && value0x65 == 0 && function == 0x06)
                    {
                        MpeStatus mpe;
                        lock (mpeStatusMap)
                        {
                            if (!mpeStatusMap.TryGetValue(deviceId, out mpe))
                            {
                                mpe = new MpeStatus();
                                mpeStatusMap[deviceId] = mpe;
                            }
                        }

                        // MPE zone configuration, value is memberChannelCount
                        var anotherZoneChanged = MpeUtils.SetupZone(mpe, channel, value);

                        MidiManager.Instance.ExecuteMidiEvent<IMpeZoneDefinedEventHandler>(new MidiManager.MidiMessage
                        {
                            DeviceId = deviceId,
                            Messages = new decimal[]{ channel, value },
                        }, (handler, message) =>
                        {
                            handler.OnMpeZoneDefined(message.DeviceId,
                                (int)message.Messages[0], (int)message.Messages[1]);
                        });

                        if (anotherZoneChanged)
                        {
                            var anotherZone = channel == 0 ? mpe.upperZone : mpe.lowerZone;
                            MidiManager.Instance.ExecuteMidiEvent<IMpeZoneDefinedEventHandler>(new MidiManager.MidiMessage
                            {
                                DeviceId = deviceId,
                                Messages = new decimal[]{ anotherZone.managerChannel, anotherZone.memberChannelCount },
                            }, (handler, message) =>
                            {
                                handler.OnMpeZoneDefined(message.DeviceId,
                                    (int)message.Messages[0], (int)message.Messages[1]);
                            });
                        }
                    }
                }
            }

            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpeControlChangeEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, function, value },
                }, (handler, message) =>
                {
                    handler.OnMpeControlChange(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1], (int)message.Messages[2]);
                });
            }
        }

        public void OnMidiChannelAftertouch(string deviceId, int group, int channel, int pressure)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpeChannelAftertouchEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, pressure },
                }, (handler, message) =>
                {
                    handler.OnMpeChannelAftertouch(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1]);
                });
            }
        }

        public void OnMidiPitchWheel(string deviceId, int group, int channel, int amount)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            // check member channel
            var zone = mpeStatus.GetZone(channel);
            if (zone != null)
            {
                MidiManager.Instance.ExecuteMidiEvent<IMpePitchWheelEventHandler>(new MidiManager.MidiMessage
                {
                    DeviceId = deviceId,
                    Messages = new decimal[]{ zone.managerChannel, amount },
                }, (handler, message) =>
                {
                    handler.OnMpePitchWheel(message.DeviceId,
                        (int)message.Messages[0], (int)message.Messages[1]);
                });
            }
        }

        public void OnMidiSystemExclusive(string deviceId, int group, byte[] systemExclusive)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeSystemExclusiveEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
                SystemExclusive = systemExclusive,
            }, (handler, message) =>
            {
                handler.OnMpeSystemExclusive(message.DeviceId, message.SystemExclusive);
            });
        }

        public void OnMidiTimeCodeQuarterFrame(string deviceId, int group, int timing)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeTimeCodeQuarterFrameEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
                Messages = new decimal[]{ timing },
            }, (handler, message) =>
            {
                handler.OnMpeTimeCodeQuarterFrame(message.DeviceId, (int)message.Messages[0]);
            });
        }

        public void OnMidiSongSelect(string deviceId, int group, int song)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeSongSelectEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
                Messages = new decimal[]{ song },
            }, (handler, message) =>
            {
                handler.OnMpeSongSelect(message.DeviceId, (int)message.Messages[0]);
            });
        }

        public void OnMidiSongPositionPointer(string deviceId, int group, int position)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeSongPositionPointerEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
                Messages = new decimal[]{ position },
            }, (handler, message) =>
            {
                handler.OnMpeSongPositionPointer(message.DeviceId, (int)message.Messages[0]);
            });
        }

        public void OnMidiTuneRequest(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeTuneRequestEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeTuneRequest(message.DeviceId);
            });
        }

        public void OnMidiTimingClock(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeTimingClockEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeTimingClock(message.DeviceId);
            });
        }

        public void OnMidiStart(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeStartEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeStart(message.DeviceId);
            });
        }

        public void OnMidiContinue(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeContinueEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeContinue(message.DeviceId);
            });
        }

        public void OnMidiStop(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeStopEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeStop(message.DeviceId);
            });
        }

        public void OnMidiActiveSensing(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeActiveSensingEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeActiveSensing(message.DeviceId);
            });
        }

        public void OnMidiReset(string deviceId, int group)
        {
            MidiManager.Instance.ExecuteMidiEvent<IMpeResetEventHandler>(new MidiManager.MidiMessage
            {
                DeviceId = deviceId,
            }, (handler, message) =>
            {
                handler.OnMpeReset(message.DeviceId);
            });
        }

        public void OnMidiCableEvents(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            // ignored
        }

        public void OnMidiSingleByte(string deviceId, int group, int byte1)
        {
            // ignored
        }

        public void OnMidiSystemCommonMessage(string deviceId, int group, byte[] message)
        {
            // ignored
        }

        public void OnMidiMiscellaneousFunctionCodes(string deviceId, int group, int byte1, int byte2, int byte3)
        {
            // ignored
        }
    }
#endregion

#region MpeOutput
    /// <summary>
    /// MIDI Polyphonic Expression output manager
    /// </summary>
    public class MpeManager
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static double CurrentTimeMillis()
        {
            return (DateTime.UtcNow - Epoch).TotalMilliseconds;
        }

        /// <summary>
        /// Get an instance<br />
        /// SHOULD be called by Unity's main thread.
        /// </summary>
        public static MpeManager Instance => lazyInstance.Value;

        private static readonly Lazy<MpeManager> lazyInstance = new Lazy<MpeManager>(() => new MpeManager(), LazyThreadSafetyMode.ExecutionAndPublication);

        
        /// <summary>
        /// MPE configurations map
        /// </summary>
        private static Dictionary<string, MpeStatus> mpeStatusMap = new Dictionary<string, MpeStatus>();

        /// <summary>
        /// channel status map
        /// </summary>
        private static Dictionary<string, ChannelStatus[]> channelsMap = new Dictionary<string, ChannelStatus[]>();

        private static int? GetNotePlayingChannel(ZoneStatus zone, int note)
        {
            // check the same note exists
            var notePlayingChannel = zone.memberChannels.FirstOrDefault(n => n != null && !n.isManagerChannel && n.notes.Contains(note));
            if (notePlayingChannel != null)
            {
                return notePlayingChannel.channel;
            }

            return null;
        }

        private static int? SelectNotePlayingChannel(ZoneStatus zone, int note)
        {
            // re-use a Channel that has been most recently deployed to play a certain Note Number once the previous note has entered its Note Off state
            var notePlayedChannel = zone.memberChannels.FirstOrDefault(n => n != null && !n.isManagerChannel && n.lastNote == note);
            if (notePlayedChannel != null)
            {
                return notePlayedChannel.channel;
            }

            // find most inactive channel
            var mostInactiveChannel = zone.memberChannels.Where(n => n != null && !n.isManagerChannel && n.notes.Count == 0 && n.noteOffTiming.HasValue).OrderBy(n => n.noteOffTiming).FirstOrDefault();
            if (mostInactiveChannel != null)
            {
                return mostInactiveChannel.channel;
            }

            if (zone.midiMode == 3)
            {
                // Poly mode
                // find the least note count
                var leastNoteCountChannel = zone.memberChannels.Where(n => n != null && !n.isManagerChannel).OrderBy(n => n.notes.Count).FirstOrDefault();
                if (leastNoteCountChannel != null)
                {
                    return leastNoteCountChannel.channel;
                }

                // if inactive channel not found, select the most idle channel
                var mostIdleChannel = zone.memberChannels.Where(n => n != null && !n.isManagerChannel && n.noteOnTiming.HasValue).OrderBy(n => n.noteOnTiming).FirstOrDefault();
                if (mostIdleChannel != null)
                {
                    return mostIdleChannel.channel;
                }
            }

            // Mono mode: note playable channel not found
            return null;
        }

        /// <summary>
        /// Setup MIDI Polyphonic Expression output device instance
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="managerChannel">0 or 15</param>
        /// <param name="memberChannelCount">0: MPE disabled, from 1 to 15: MPE enabled</param>
        public void SetupMpeZone(string deviceId, int managerChannel, int memberChannelCount)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (managerChannel != 0 && managerChannel != 15)
            {
                Debug.LogWarning($"Invalid parameter. The managerChannel should be 0 or 15.");
                return;
            }

            if (memberChannelCount > 15)
            {
                Debug.LogWarning("Invalid zone member channel count. Changed to 15");
                memberChannelCount = 15;
            }

            MpeUtils.SetupZone(mpeStatus, managerChannel, memberChannelCount);

            // send RPN messages
            MidiManager.Instance.SendMidiControlChange(deviceId, 0, managerChannel, 0x64, 6);
            MidiManager.Instance.SendMidiControlChange(deviceId, 0, managerChannel, 0x65, 0);
            MidiManager.Instance.SendMidiControlChange(deviceId, 0, managerChannel, 0x06, memberChannelCount);
        }

        private static void SendZoneChannelParameters(string deviceId, ZoneStatus zone, int channel)
        {
            var channelStatus = zone.memberChannels[channel];
            foreach (var pair in zone.polyphonicAftertouches)
            {
                if (!channelStatus.polyphonicAftertouches.ContainsKey(pair.Key) || channelStatus.polyphonicAftertouches[pair.Key] != pair.Value)
                {
                    MidiManager.Instance.SendMidiPolyphonicAftertouch(deviceId, 0, channel, pair.Key, pair.Value);
                    channelStatus.polyphonicAftertouches[pair.Key] = pair.Value;
                }
            }

            foreach (var pair in zone.controllers)
            {
                if (!channelStatus.controllers.ContainsKey(pair.Key) || channelStatus.controllers[pair.Key] != pair.Value)
                {
                    MidiManager.Instance.SendMidiControlChange(deviceId, 0, channel, pair.Key, pair.Value);
                    channelStatus.controllers[pair.Key] = pair.Value;
                }
            }

            if (channelStatus.program != zone.program)
            {
                MidiManager.Instance.SendMidiProgramChange(deviceId, 0, channel, zone.program);
                channelStatus.program = zone.program;
            }

            if (channelStatus.channelAftertouch != zone.channelAftertouch)
            {
                MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0, channel, zone.channelAftertouch);
                channelStatus.channelAftertouch = zone.channelAftertouch;
            }

            if (channelStatus.pitch != zone.pitch)
            {
                MidiManager.Instance.SendMidiPitchWheel(deviceId, 0, channel, zone.pitch);
                channelStatus.pitch = zone.pitch;
            }
        }

        /// <summary>
        /// Changes MIDI mode
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15</param>
        /// <param name="mode">3: Omni Off, Poly<br/>
        /// 4: Omni Off, Mono<br/>
        /// other values will be ignored
        /// </param>
        public void ChangeMidiMode(string deviceId, int channel, int mode)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                return;
            }

            // send to basic channel (lowest member channel) only
            var basicChannel = zone.memberChannels.FirstOrDefault(n => n != null);
            if (basicChannel == null)
            {
                Debug.LogWarning("The device is not configured as MPE.");
                return;
            }

            switch (mode)
            {
                case 3: // Omni Off, Polyphonic
                    // v = 0: Poly Mode On (Mono Off)
                    MidiManager.Instance.SendMidiControlChange(deviceId, 0, basicChannel.channel, 127, 0);
                    zone.midiMode = 3;
                    break;
                case 4: // Omni Off, Monophonic
                    // v = M: Mono Mode On (Poly Off) where M is the number of channels (Omni Off)
                    MidiManager.Instance.SendMidiControlChange(deviceId, 0, basicChannel.channel, 126, 1);
                    zone.midiMode = 4;
                    break;
            }
        }

        /// <summary>
        /// Sends a Note On message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMpeNoteOn(string deviceId, int channel, int note, int velocity)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiNoteOn(deviceId, 0, channel, note, velocity);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiNoteOn(deviceId, 0, channel, note, velocity);
                return;
            }
         
            var selectedChannel = GetNotePlayingChannel(zone, note);
            if (selectedChannel.HasValue)
            {
                // already same note playing: stop it first
                MidiManager.Instance.SendMidiNoteOff(deviceId, 0, selectedChannel.Value, note, 64);
            }
            else
            {
                // select an inactive channel
                selectedChannel = SelectNotePlayingChannel(zone, note);
            }

            if (!selectedChannel.HasValue)
            {
                Debug.LogWarning($"Member channels are all active, the note {note} can't be played.");
                return;
            }

            // Apply channel parameters before playing new note
            SendZoneChannelParameters(deviceId, zone, selectedChannel.Value);
            MidiManager.Instance.SendMidiNoteOn(deviceId, 0, selectedChannel.Value, note, velocity);
            
            var channelStatus = zone.memberChannels[selectedChannel.Value];
            channelStatus.lastNote = note;
            channelStatus.notes.Add(note);
            channelStatus.noteOnTiming = CurrentTimeMillis();
            channelStatus.noteOffTiming = null;
        }

        /// <summary>
        /// Sends a Note Off message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="note">0-127</param>
        /// <param name="velocity">0-127</param>
        public void SendMpeNoteOff(string deviceId, int channel, int note, int velocity)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiNoteOff(deviceId, 0, channel, note, velocity);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiNoteOff(deviceId, 0, channel, note, velocity);
                return;
            }
         
            var selectedChannel = GetNotePlayingChannel(zone, note);
            if (!selectedChannel.HasValue)
            {
                // currently, the note wasn't played
                return;
            }

            MidiManager.Instance.SendMidiNoteOff(deviceId, 0, selectedChannel.Value, note, velocity);
            
            var channelStatus = zone.memberChannels[selectedChannel.Value];
            channelStatus.notes.Remove(note);
            channelStatus.noteOffTiming = CurrentTimeMillis();
        }

        /// <summary>
        /// Sends a Polyphonic Aftertouch message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="note">0-127</param>
        /// <param name="pressure">0-127</param>
        public void SendMpePolyphonicAftertouch(string deviceId, int channel, int note, int pressure)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiPolyphonicAftertouch(deviceId, 0, channel, note, pressure);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiPolyphonicAftertouch(deviceId, 0, channel, note, pressure);
                return;
            }
         
            zone.polyphonicAftertouches[note] = pressure;

            if (zone.managerChannel == channel)
            {
                // send to all active channel
                var activeChannels = zone.memberChannels.Where(n => n != null && n.notes.Count > 0).Select(n => n.channel);
                foreach (var activeChannel in activeChannels)
                {
                    MidiManager.Instance.SendMidiPolyphonicAftertouch(deviceId, 0, activeChannel, note, pressure);
                }
            }
            // PolyphonicAftertouch should not send to member channel
        }

        /// <summary>
        /// Sends a Control Change message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="function">0-127</param>
        /// <param name="value">0-127</param>
        public void SendMpeControlChange(string deviceId, int channel, int function, int value)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            ChannelStatus[] channels;
            lock (channelsMap)
            {
                if (!channelsMap.TryGetValue(deviceId, out channels))
                {
                    // managerChannel is 0 or 15
                    channels = new ChannelStatus[16];
                    channels[0] = new ChannelStatus();
                    channels[15] = new ChannelStatus();

                    channelsMap[deviceId] = channels;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiControlChange(deviceId, 0, channel, function, value);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiControlChange(deviceId, 0, channel, function, value);
                return;
            }
         
            zone.controllers[function] = value;

            if (zone.managerChannel == channel)
            {
                if (function == 120 || function == 125 || function == 126 || function == 127)
                {
                    // don't send function 120, 125, 126, 127 to manager
                    return;
                }
                // send to all active channel
                var activeChannels = zone.memberChannels.Where(n => n != null && n.notes.Count > 0).Select(n => n.channel);
                foreach (var activeChannel in activeChannels)
                {
                    MidiManager.Instance.SendMidiControlChange(deviceId, 0, activeChannel, function, value);
                }
            }
            else if (zone.memberChannels[channel] != null)
            {
                channels[channel].controllers[function] = value;

                // MPE Configuration Message (MCM)
                // Message Format: [Bn 64 06] [Bn 65 00] [Bn 06 <mm>]
                if (channels[channel].controllers.TryGetValue(0x64, out var value0x64) &&
                    channels[channel].controllers.TryGetValue(0x65, out var value0x65))
                {
                    if (value0x64 == 6 && value0x65 == 0 && function == 0x06)
                    {
                        // don't send MPE Configuration Message to member
                        return;
                    }
                }

                if (function == 125)
                {
                    // don't send function 125 to member
                    return;
                }

                // send to member channel
                if (zone.midiMode == 4)
                {
                    MidiManager.Instance.SendMidiControlChange(deviceId, 0, channel, function, value);
                    zone.memberChannels[channel].controllers[function] = value;
                }
                else
                {
                    // Send: Not recommended.
                    // function 0, 32(bank select): valid in MIDI Mode 4 only
                    if (function != 0 && function != 32)
                    {
                        MidiManager.Instance.SendMidiControlChange(deviceId, 0, channel, function, value);
                        zone.memberChannels[channel].controllers[function] = value;
                    }
                }
            }
        }

        /// <summary>
        /// Sends a Program Change message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="program">0-127</param>
        public void SendMpeProgramChange(string deviceId, int channel, int program)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiProgramChange(deviceId, 0, channel, program);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiProgramChange(deviceId, 0, channel, program);
                return;
            }
         
            zone.program = program;

            if (zone.managerChannel == channel)
            {
                // send to all active channel
                var activeChannels = zone.memberChannels.Where(n => n != null && n.notes.Count > 0).Select(n => n.channel);
                foreach (var activeChannel in activeChannels)
                {
                    MidiManager.Instance.SendMidiProgramChange(deviceId, 0, activeChannel, program);
                }
            }
            else if (zone.memberChannels[channel] != null)
            {
                // send to member channel
                if (zone.midiMode == 4)
                {
                    // valid in MIDI Mode 4 only
                    MidiManager.Instance.SendMidiProgramChange(deviceId, 0, channel, program);
                    zone.memberChannels[channel].program = program;
                }
            }
        }

        /// <summary>
        /// Sends a Channel Aftertouch message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="pressure">0-127</param>
        public void SendMpeChannelAftertouch(string deviceId, int channel, int pressure)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0, channel, pressure);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0, channel, pressure);
                return;
            }
         
            zone.channelAftertouch = pressure;

            if (zone.managerChannel == channel)
            {
                // send to all active channel
                var activeChannels = zone.memberChannels.Where(n => n != null && n.notes.Count > 0).Select(n => n.channel);
                foreach (var activeChannel in activeChannels)
                {
                    MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0, activeChannel, pressure);
                }
            }
            else if (zone.memberChannels[channel] != null)
            {
                // send to member channel
                MidiManager.Instance.SendMidiChannelAftertouch(deviceId, 0, channel, pressure);
                zone.memberChannels[channel].channelAftertouch = pressure;
            }
        }

        /// <summary>
        /// Sends a Pitch Wheel message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="channel">0-15, ignored if MPE function enabled for the device</param>
        /// <param name="amount">0-16383</param>
        public void SendMpePitchWheel(string deviceId, int channel, int amount)
        {
            MpeStatus mpeStatus;
            lock (mpeStatusMap)
            {
                if (!mpeStatusMap.TryGetValue(deviceId, out mpeStatus))
                {
                    return;
                }
            }

            if (mpeStatus.lowerZone.memberChannels.All(n => n == null) && mpeStatus.upperZone.memberChannels.All(n => n == null))
            {
                // The device is not configured as MPE, just send to the channel
                MidiManager.Instance.SendMidiPitchWheel(deviceId, 0, channel, amount);
                return;
            }

            var zone = mpeStatus.GetZone(channel);
            if (zone == null)
            {
                // The channel is not related to MPE zone, just send to the channel
                MidiManager.Instance.SendMidiPitchWheel(deviceId, 0, channel, amount);
                return;
            }
         
            zone.pitch = amount;

            if (zone.managerChannel == channel)
            {
                // send to all active channel
                var activeChannels = zone.memberChannels.Where(n => n != null && n.notes.Count > 0).Select(n => n.channel);
                foreach (var activeChannel in activeChannels)
                {
                    MidiManager.Instance.SendMidiPitchWheel(deviceId, 0, activeChannel, amount);
                }
            }
            else if (zone.memberChannels[channel] != null)
            {
                // send to member channel
                MidiManager.Instance.SendMidiPitchWheel(deviceId, 0, channel, amount);
                zone.memberChannels[channel].pitch = amount;
            }
        }

        /// <summary>
        /// Sends a System Exclusive message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="sysEx">byte array starts with F0, ends with F7</param>
        public void SendMpeSystemExclusive(string deviceId, byte[] sysEx)
        {
            MidiManager.Instance.SendMidiSystemExclusive(deviceId, 0, sysEx);
        }

        /// <summary>
        /// Sends a Time Code Quarter Frame message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="timing">0-127</param>
        public void SendMpeTimeCodeQuarterFrame(string deviceId, int timing)
        {
            MidiManager.Instance.SendMidiTimeCodeQuarterFrame(deviceId, 0, timing);
        }

        /// <summary>
        /// Sends a Song Select message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="song">0-127</param>
        public void SendMpeSongSelect(string deviceId, int song)
        {
            MidiManager.Instance.SendMidiSongSelect(deviceId, 0, song);
        }

        /// <summary>
        /// Sends a Song Position Pointer message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        /// <param name="position">0-16383</param>
        public void SendMpeSongPositionPointer(string deviceId, int position)
        {
            MidiManager.Instance.SendMidiSongPositionPointer(deviceId, 0, position);
        }

        /// <summary>
        /// Sends a Tune Request message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeTuneRequest(string deviceId)
        {
            MidiManager.Instance.SendMidiTuneRequest(deviceId, 0);
        }

        /// <summary>
        /// Sends a Timing Clock message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeTimingClock(string deviceId)
        {
            MidiManager.Instance.SendMidiTimingClock(deviceId, 0);
        }

        /// <summary>
        /// Sends a Start message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeStart(string deviceId)
        {
            MidiManager.Instance.SendMidiStart(deviceId, 0);
        }

        /// <summary>
        /// Sends a Continue message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeContinue(string deviceId)
        {
            MidiManager.Instance.SendMidiContinue(deviceId, 0);
        }

        /// <summary>
        /// Sends a Stop message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeStop(string deviceId)
        {
            MidiManager.Instance.SendMidiStop(deviceId, 0);
        }

        /// <summary>
        /// Sends an Active Sensing message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeActiveSensing(string deviceId)
        {
            MidiManager.Instance.SendMidiActiveSensing(deviceId, 0);
        }

        /// <summary>
        /// Sends a Reset message
        /// </summary>
        /// <param name="deviceId">the device id</param>
        public void SendMpeReset(string deviceId)
        {
            MidiManager.Instance.SendMidiReset(deviceId, 0);
        }
    }
#endregion
}
