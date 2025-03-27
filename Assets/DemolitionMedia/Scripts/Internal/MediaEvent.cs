
#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DemolitionStudios.DemolitionMedia
{
    /// Possible graphics interface device types
    public enum GraphicsInterfaceDeviceType
    {
        Unknown = 0,
        D3D9,
        D3D11,
        D3D12,
        OpenGL,
        Metal,
        Vulkan,
        PS4
    };

    /// Possible video frame present modes
    public enum FramePresentMode
    {
        /// n/a
        None = 0,
        /// Preloaded native immutable textures for next frames (default)
        ImmutableTextures, 
        /// Use single native dynamic texture
        DynamicTextureFallback,
        /// Don't upload textures on native plugin side, instead pass raw texture data outside
        PassRawTextureDataOutside,
    };

    /// Possible sync modes
    public enum SyncMode
    {
        /// n/a
        SyncNone = 0,
        /// Syncronize to the audio stream clock
        SyncAudioMaster,
        /// Syncronize to the video stream clock
        SyncVideoMaster,
        /// Synchronize to an external clock (unavailable)
        SyncExternalClock,
        /// Synchronize to external clock values, provided by the host application (unavailable)
        SyncExternalClockValue,
        /// Use frame index queue provided by the host application (unavailable)
        SyncExternalFrameIndexQueue
    }

    /// Enumerates possible states of media playback.
    public enum MediaState
    {
        /// Media has been closed and cannot be played again.
        Closed = 0,
        /// Media is preloading to CPU memory.
        PreloadingToMemory,
        /// Media is opening.
        Opening,
        /// Unrecoverable error occurred during loading or playback.
        Error,
        /// Playback has been paused, but can be resumed.
        Paused,
        /// Media is currently playing.
        Playing,
        /// Playback has been stopped, but can be restarted.
        Stopped
    }

    /// Enumerates possible errors while opening or playing media.
    public enum MediaError
    {
        /// No error.
        NoError = 0,
        /// Couldn't allocate memory.
        AllocateMemoryError,
        /// Couldn't open the input media source.
        OpenInputError,
        /// Couldn't find a suitable codec for the input media source.
        FindCodecError,
        /// No audio/video streams found for playback.
        NoStreamsError,
        /// Some other kind of ffmpeg error.
        OtherFFmpegError,
        /// Graphics device error (unsupported texture format, etc).
        GraphicsDeviceError,
        /// Hap decode error.
        HapDecodeError
    }

    public class MediaEvent
#if UNITY_5_3_OR_NEWER
        : UnityEngine.Events.UnityEvent<Media, MediaEvent.Type, MediaError>
#endif
    {
        /// Enumerates possible media events.
        public enum Type
        {
            /// The current media source is about to be closed.
            ClosingStarted = 0,
            /// The current media source has been closed.
            Closed,
            /// Media opening has been started.
            OpeningStarted,
            /// Media preloading to memory has been started.
            PreloadingToMemoryStarted,
            /// Media preloading to memory has been finished.
            PreloadingToMemoryFinished,
            /// A new media source has been opened.
            Opened,
            /// A media source failed to open.
            OpenFailed,
            /// Texture for rendering video stream has been created.
            VideoRenderTextureCreated,
            /// Playback has been started.
            PlaybackStarted,
            /// Playback has been stopped.
            PlaybackStopped,
            /// The end of the media has been reached.
            PlaybackEndReached,
            /// Playback has been suspended/paused.
            PlaybackSuspended,
            /// Playback has been resumed.
            PlaybackResumed,
            /// Next loop reached during playback.
            PlaybackNewLoop,
            /// Some unrecoverable error has happended during playback.
            PlaybackErrorOccured,
            /// GPU GDeflate Compute decoding not available
            GPUComputeBasedDecompressionNotAvailable,
            /// Total number of events.
            Count = PlaybackErrorOccured + 1
        }
    }

    /// Possible video frame pixel formats
    public enum PixelFormat
    {
        /// n/a
        None = 0,
        /// packed RGB 8:8:8, 24bpp, RGBRGB... (e.g. Hap)
        RGB,
        /// packed RGBA 8:8:8:8, 32bpp, RGBARGBA... (e.g. Hap Alpha)
        RGBA,
        /// packed YCoCg 8:8:8, 24bpp (e.g. Hap Q)
        YCoCg,
        /// packed YCoCg + Alpha plane (e.g. Hap Q Alpha)
        YCoCgAndAlphaP,
        /// packed Alpha 8bpp, (e.g. Hap Alpha-Only)
        Alpha,
        /// planar YUV 4:2:0, 12bpp, (1 Cr & Cb sample per 2x2 Y samples)
        YUV420P,
        YUV420P10LE,
        /// planar YUV 4:2:2, 20bpp, (1 Cr & Cb sample per 2x1 Y samples), little-endian
        YUV422P10LE,
        YUV420P12LE,
        YUV444P12LE,
        YUV444P16LE,
        /// planar YUV 4:4:4 40bpp, (1 Cr & Cb sample per 1x1 Y & A samples, little-endian)
        YUVA444P10LE,
        /// planar YUV 4:4:4 64bpp, (1 Cr & Cb sample per 1x1 Y & A samples, little-endian)
        YUVA444P16LE
    }

    // Possible audio sample formats.
    // Warning: values should match the AVSampleFormat enum.
    public enum SampleFormat
    {
      /// n/a
      Unknown = -1,       
      /// unsigned 8 bits
      Unsigned8,          
      /// signed 16 bits
      Signed16,           
      /// signed 32 bits
      Signed32,        
      /// float
      Float,           
      /// double
      Double,           
      /// unsigned 8 bits, planar
      Unsigned8_Planar,         
      /// signed 16 bits, planar
      Signed16_Planar,          
      /// signed 32 bits, planar
      Signed32_Planar,       
      /// float, planar
      Float_Planar,          
      /// double, planar
      Double_Planar,          
      /// Number of sample formats
      Count             
    }

    public enum FrameType
    {
        Current = 0,
        Next
    }
}