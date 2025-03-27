LibBLEMIDIPlugin = {
    $Ble: {
        // const
        MIDI_STATE_TIMESTAMP: 0,
        MIDI_STATE_WAIT: 1,
        MIDI_STATE_SIGNAL_2BYTES_2: 2,
        MIDI_STATE_SIGNAL_3BYTES_2: 3,
        MIDI_STATE_SIGNAL_3BYTES_3: 4,
        MIDI_STATE_SIGNAL_SYSEX: 5,

        outputs: null,
        outputBuffers: null,
        attachedDevices: null,
        manufacturerNames: null,

        midiStates: null,
        midiEventKinds: null,
        midiEventNotes: null,
        midiEventVelocities: null,
        systemExclusives: null,

        ParseMidiMessage: function (header, midiEvent, targetId) {
            if (Ble.midiStates == null) {
                Ble.midiStates = new Map();
            }
            if (!Ble.midiStates.has(targetId)) {
                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
            }
            var midiState = Ble.midiStates.get(targetId);
            if (Ble.midiEventKinds == null) {
                Ble.midiEventKinds = new Map();
            }
            if (!Ble.midiEventKinds.has(targetId)) {
                Ble.midiEventKinds.set(targetId, 0);
            }
            var midiEventKind = Ble.midiEventKinds.get(targetId);
            if (Ble.midiEventNotes == null) {
                Ble.midiEventNotes = new Map();
            }
            if (!Ble.midiEventNotes.has(targetId)) {
                Ble.midiEventNotes.set(targetId, 0);
            }
            var midiEventNote = Ble.midiEventNotes.get(targetId);
            if (Ble.midiEventVelocities == null) {
                Ble.midiEventVelocities = new Map();
            }
            if (!Ble.midiEventVelocities.has(targetId)) {
                Ble.midiEventVelocities.set(targetId, 0);
            }
            var midiEventVelocity = Ble.midiEventVelocities.get(targetId);

            if (Ble.systemExclusives == null) {
                Ble.systemExclusives = new Map();
            }
            if (!Ble.systemExclusives.has(targetId)) {
                Ble.systemExclusives.set(targetId, []);
            }
            var systemExclusive = Ble.systemExclusives.get(targetId);

            // parser starts
            if (midiState == Ble.MIDI_STATE_TIMESTAMP) {
                if ((midiEvent & 0x80) == 0) {
                    // running status
                    midiState = Ble.MIDI_STATE_WAIT;
                    Ble.midiStates.set(targetId, Ble.MIDI_STATE_WAIT);
                }
    
                if (midiEvent == 0xf7) {
                    // is this end of SysEx???
                    if (systemExclusive.length > 0) {
                        // previous SysEx has been failed, due to timestamp was 0xF7
                        // process SysEx again
                        systemExclusive.push(midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiSystemExclusive', '' + targetId + ',0,' + systemExclusive.join(','));
    
                        systemExclusive = [];
                        Ble.systemExclusives.set(targetId, []);
                    }
    
                    // process next byte with state: Ble.MIDI_STATE_TIMESTAMP
                    midiState = Ble.MIDI_STATE_TIMESTAMP;
                    Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                    return;
                }
            }
            if (midiState == Ble.MIDI_STATE_TIMESTAMP) {
                timestamp = ((header & 0x3f) << 7) | (midiEvent & 0x7f);
                midiState = Ble.MIDI_STATE_WAIT;
                Ble.midiStates.set(targetId, Ble.MIDI_STATE_WAIT);
            } else if (midiState == Ble.MIDI_STATE_WAIT) {
                switch (midiEvent & 0xf0) {
                    case 0xf0: {
                        switch (midiEvent) {
                            case 0xf0:
                                systemExclusive = [];
                                Ble.systemExclusives.set(targetId, []);
                                midiState = Ble.MIDI_STATE_SIGNAL_SYSEX;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_SYSEX);
                                break;
    
                            case 0xf1:
                            case 0xf3:
                                // 0xf1 MIDI Time Code Quarter Frame. : 2bytes
                                // 0xf3 Song Select. : 2bytes
                                midiEventKind = midiEvent;
                                Ble.midiEventKinds.set(targetId, midiEvent);
                                midiState = Ble.MIDI_STATE_SIGNAL_2BYTES_2;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_2BYTES_2);
                                break;
    
                            case 0xf2:
                                // 0xf2 Song Position Pointer. : 3bytes
                                midiEventKind = midiEvent;
                                Ble.midiEventKinds.set(targetId, midiEvent);
                                midiState = Ble.MIDI_STATE_SIGNAL_3BYTES_2;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_3BYTES_2);
                                break;
    
                            case 0xf6:
                                // 0xf6 Tune Request : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiTuneRequest', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xf8:
                                // 0xf8 Timing Clock : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiTimingClock', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xfa:
                                // 0xfa Start : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiStart', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xfb:
                                // 0xfb Continue : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiContinue', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xfc:
                                // 0xfc Stop : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiStop', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xfe:
                                // 0xfe Active Sensing : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiActiveSensing', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xff:
                                // 0xff Reset : 1byte
                                unityInstance.SendMessage('MidiManager', 'OnMidiReset', '' + targetId + ',0');
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
    
                            default:
                                break;
                        }
                    }
                    break;
                    case 0x80:
                    case 0x90:
                    case 0xa0:
                    case 0xb0:
                    case 0xe0:
                        // 3bytes pattern
                        midiEventKind = midiEvent;
                        Ble.midiEventKinds.set(targetId, midiEvent);
                        midiState = Ble.MIDI_STATE_SIGNAL_3BYTES_2;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_3BYTES_2);
                        break;
                    case 0xc0: // program change
                    case 0xd0: // channel after-touch
                        // 2bytes pattern
                        midiEventKind = midiEvent;
                        Ble.midiEventKinds.set(targetId, midiEvent);
                        midiState = Ble.MIDI_STATE_SIGNAL_2BYTES_2;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_2BYTES_2);
                        break;
                    default:
                        // 0x00 - 0x70: running status
                        if ((midiEventKind & 0xf0) != 0xf0) {
                            // previous event kind is multi-bytes pattern
                            midiEventNote = midiEvent;
                            Ble.midiEventNotes.set(targetId, midiEvent);
                            midiState = Ble.MIDI_STATE_SIGNAL_3BYTES_3;
                            Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_3BYTES_3);
                        }
                        break;
                }
            } else if (midiState == Ble.MIDI_STATE_SIGNAL_2BYTES_2) {
                switch (midiEventKind & 0xf0) {
                    // 2bytes pattern
                    case 0xc0: // program change
                        midiEventNote = midiEvent;
                        Ble.midiEventNotes.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiProgramChange', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote);
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xd0: // channel after-touch
                        midiEventNote = midiEvent;
                        Ble.midiEventNotes.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiChannelAftertouch', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote);
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xf0: {
                        switch (midiEventKind) {
                            case 0xf1:
                                // 0xf1 MIDI Time Code Quarter Frame. : 2bytes
                                midiEventNote = midiEvent;
                                Ble.midiEventNotes.set(targetId, midiEvent);
                                unityInstance.SendMessage('MidiManager', 'OnMidiTimeCodeQuarterFrame', '' + targetId + ',0,' + midiEventNote);
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            case 0xf3:
                                // 0xf3 Song Select. : 2bytes
                                midiEventNote = midiEvent;
                                Ble.midiEventNotes.set(targetId, midiEvent);
                                unityInstance.SendMessage('MidiManager', 'OnMidiSongSelect', '' + targetId + ',0,' + midiEventNote);
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                            default:
                                // illegal state
                                midiState = Ble.MIDI_STATE_TIMESTAMP;
                                Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                                break;
                        }
                    }
                        break;
                    default:
                        // illegal state
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                }
            } else if (midiState == Ble.MIDI_STATE_SIGNAL_3BYTES_2) {
                switch (midiEventKind & 0xf0) {
                    case 0x80:
                    case 0x90:
                    case 0xa0:
                    case 0xb0:
                    case 0xe0:
                    case 0xf0:
                        // 3bytes pattern
                        midiEventNote = midiEvent;
                        Ble.midiEventNotes.set(targetId, midiEvent);
                        midiState = Ble.MIDI_STATE_SIGNAL_3BYTES_3;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_SIGNAL_3BYTES_3);
                        break;
                    default:
                        // illegal state
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                }
            } else if (midiState == Ble.MIDI_STATE_SIGNAL_3BYTES_3) {
                switch (midiEventKind & 0xf0) {
                    // 3bytes pattern
                    case 0x80: // note off
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiNoteOff', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote + ',' + midiEventVelocity);
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0x90: // note on
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        if (midiEventVelocity == 0) {
                            unityInstance.SendMessage('MidiManager', 'OnMidiNoteOff', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote + ',' + midiEventVelocity);
                        } else {
                            unityInstance.SendMessage('MidiManager', 'OnMidiNoteOn', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote + ',' + midiEventVelocity);
                        }
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xa0: // control polyphonic key pressure
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiPolyphonicAftertouch', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote + ',' + midiEventVelocity);
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xb0: // control change
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiControlChange', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + midiEventNote + ',' + midiEventVelocity);
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xe0: // pitch bend
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiPitchWheel', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + (midiEventNote & 0x7f) | ((midiEventVelocity & 0x7f) << 7));
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    case 0xf0: // Song Position Pointer.
                        midiEventVelocity = midiEvent;
                        Ble.midiEventVelocities.set(targetId, midiEvent);
                        unityInstance.SendMessage('MidiManager', 'OnMidiSongPositionPointer', '' + targetId + ',0,' + (midiEventKind & 0xf) + ',' + (midiEventNote & 0x7f) | ((midiEventVelocity & 0x7f) << 7));
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                    default:
                        // illegal state
                        midiState = Ble.MIDI_STATE_TIMESTAMP;
                        Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                        break;
                }
            } else if (midiState == Ble.MIDI_STATE_SIGNAL_SYSEX) {
                if (midiEvent == 0xf7) {
                    // the end of message
                    systemExclusive.push(midiEvent);
                    unityInstance.SendMessage('MidiManager', 'OnMidiSystemExclusive', '' + targetId + ',0,' + systemExclusive.join(','));

                    systemExclusive = [];
                    Ble.systemExclusives.set(targetId, []);
                    midiState = Ble.MIDI_STATE_TIMESTAMP;
                    Ble.midiStates.set(targetId, Ble.MIDI_STATE_TIMESTAMP);
                } else {
                    systemExclusive.push(midiEvent);
                    Ble.systemExclusives.set(targetId, systemExclusive);
                }
            }
        },

        ParseMidi: function (message, targetId) {
            if (message == null || message.length <= 1) {
                return;
            }
            for (var i = 1; i < message.length; i++) {
                Ble.ParseMidiMessage(message[0], message[i], targetId);
            }
        },

        tryAtMost: function (tries, executor) {
            --tries;
            return new Promise(executor)
                .catch(function (err) {
                    if (tries > 0) {
                        Ble.tryAtMost(tries, executor);
                    } else {
                        Promise.reject(err);
                    }
                });
        },

        midiServicePromise: function (device) {
            return function (resolve, reject) {
                device.gatt.connect()
                .then(function (server) {
                    const midiService = server.getPrimaryService('03b80e5a-ede8-4b33-a751-6ce34ec4c700');
                    midiService.then(function (service) {
                        return service.getCharacteristic('7772e5db-3868-4112-a1a9-f2669d106bf3');
                    })
                    .then(function (characteristic) {
                        Ble.outputs.set(characteristic.service.device.id, characteristic);
                        return characteristic.startNotifications();
                    })
                    .then(function (characteristic) {
                        // Set up event listener for when characteristic value changes.
                        characteristic.addEventListener('characteristicvaluechanged', function(event) {
                            Ble.ParseMidi(new Uint8Array(event.target.value.buffer), event.target.service.device.id);
                        });

                        resolve();
                    })
                    .catch(function (error) {
                        // console.log('midiServicePromise failed:' + error);
                        reject();
                    });
                })
                .catch(function (error) {
                    // console.log('midiServicePromise failed:' + error);
                    reject();
                });
            };
        },

        deviceInformationServicePromise: function (device) {
            return function (resolve, reject) {
                device.gatt.connect()
                .then(function (server) {
                    // Device Infomation Service
                    const decoder = new TextDecoder('utf-8');
                    const deviceInformationService = server.getPrimaryService('device_information');

                    deviceInformationService.then(function (service) {
                        // Manufacturer Name
                        return service.getCharacteristic('manufacturer_name_string');
                    })
                    .then(function (manufacturerName) {
                        return manufacturerName.readValue();
                    })
                    .then(function (value) {
                        if (value != null) {
                            Ble.manufacturerNames.set(device.id, decoder.decode(value));
                        }
                        resolve();
                    })
                    .catch(function (error) {
                        // console.log('deviceInformationServicePromise failed:' + error);
                        reject();
                    });
                })
                .catch(function (error) {
                    // console.log('deviceInformationServicePromise failed:' + error);
                    reject();
                });
            };
        },
    },

    bleMidiPluginInitialize: function () {
        Ble.outputs = new Map();
        Ble.outputBuffers = new Map();
        Ble.attachedDevices = new Map();
        Ble.manufacturerNames = new Map();

        setInterval(function() {
            Ble.outputs.forEach(function (device, deviceId, map) {
                if (Ble.outputBuffers.has(deviceId)) {
                    var buffer = Ble.outputBuffers.get(deviceId);
                    Ble.outputBuffers.set(deviceId, []);
                    if (buffer.length > 0) {
                        device.writeValueWithoutResponse(new Uint8Array(buffer));
                    }
                }
            });
        }, 10);
    },

    startScanBluetoothMidiDevices: function () {
        navigator.bluetooth.requestDevice({
            filters: [{ services: ['03b80e5a-ede8-4b33-a751-6ce34ec4c700'] }],
            optionalServices: [ 'device_information' ]
        })
        .then(function (device) {
            device.addEventListener('gattserverdisconnected', function(event) {
                Ble.attachedDevices.delete(event.target.id);

                unityInstance.SendMessage('MidiManager', 'OnMidiInputDeviceDetached', event.target.id);
                unityInstance.SendMessage('MidiManager', 'OnMidiOutputDeviceDetached', event.target.id);

                if (device.gatt.connected) {
                    device.gatt.disconnect();
                }
            });

            if (Ble.attachedDevices.has(device.id)) {
                return;
            }
            Ble.attachedDevices.set(device.id, device);

            const midiServicePromise = Ble.midiServicePromise(device)
            Ble.tryAtMost(3, midiServicePromise)
            .then(function () {
                // connected successfully, then get device information
                const deviceInformationServicePromise = Ble.deviceInformationServicePromise(device);
                Ble.tryAtMost(3, deviceInformationServicePromise)
                .then(function () {})
                .catch(function (error) {
                    // console.log('deviceInformationServicePromise retry failed:' + error);
                })
                .then(function () {
                    unityInstance.SendMessage('MidiManager', 'OnMidiInputDeviceAttached', device.id);
                    unityInstance.SendMessage('MidiManager', 'OnMidiOutputDeviceAttached', device.id);
                });
            })
            .catch(function (error) {
                // console.log('midiServicePromise retry failed:' + error);
            });
        });
    },

    getBleDeviceName: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        var deviceName = null;
        {
            var device = Ble.attachedDevices.get(deviceId);
            if (device != null) {
                deviceName = device.name;
            }
        }

        if (deviceName == null) {
            return null;
        }

        var bufferSize = lengthBytesUTF8(deviceName) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(deviceName, buffer, bufferSize);
        return buffer;
    },

    getBleVendorId: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        var manufacturerName = Ble.manufacturerNames.get(deviceId);

        if (manufacturerName == null) {
            return null;
        }

        var bufferSize = lengthBytesUTF8(manufacturerName) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(manufacturerName, buffer, bufferSize);
        return buffer;
    },

    sendBleMidiNoteOff: function(deviceIdStr, channel, note, velocity) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0x80 | channel, note, velocity]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiNoteOn: function(deviceIdStr, channel, note, velocity) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0x90 | channel, note, velocity]);
        Ble.outputBuffers.set(deviceId, buffer);
    },
    
    sendBleMidiPolyphonicAftertouch: function(deviceIdStr, channel, note, pressure) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xa0 | channel, note, pressure]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiControlChange: function(deviceIdStr, channel, func, value) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xb0 | channel, func, value]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiProgramChange: function(deviceIdStr, channel, program) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xc0 | channel, program]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiChannelAftertouch: function(deviceIdStr, channel, pressure) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xd0 | channel, pressure]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiPitchWheel: function(deviceIdStr, channel, amount) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xe0 | channel, amount & 0x7f, (amount >> 7) & 0x7f]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiSystemExclusive: function(deviceIdStr, data) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat(data);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiTimeCodeQuarterFrame: function(deviceIdStr, value) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xf1, value]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiSongPositionPointer: function(deviceIdStr, position) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xf2, position & 0x7f, (position >> 7) & 0x7f]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiSongSelect: function(deviceIdStr, song) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer = buffer.concat([0xf3, song]);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiTuneRequest: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xf6);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiTimingClock: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xf8);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiStart: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xfa);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiContinue: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xfb);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiStop: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xfc);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiActiveSensing: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xfe);
        Ble.outputBuffers.set(deviceId, buffer);
    },

    sendBleMidiReset: function(deviceIdStr) {
        var deviceId = UTF8ToString(deviceIdStr);
        if (!Ble.outputBuffers.has(deviceId)) {
            Ble.outputBuffers.set(deviceId, []);
        }
        var buffer = Ble.outputBuffers.get(deviceId);
        var t = performance.now() % 8192;
        if (buffer.length == 0) {
            buffer.push(((t >> 7) & 0x3f) | 0x80);
        }
        buffer.push((t & 0x7f) | 0x80);
        buffer.push(0xff);
        Ble.outputBuffers.set(deviceId, buffer);
    }
};

autoAddDeps(LibBLEMIDIPlugin, '$Ble');
mergeInto(LibraryManager.library, LibBLEMIDIPlugin);
