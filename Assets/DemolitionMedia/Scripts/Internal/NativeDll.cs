// on OpenGL ES there is no way to query texture extents from native texture id
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
    #define UNITY_GLES_RENDERER
#endif

using System.Runtime.InteropServices;


namespace DemolitionStudios.DemolitionMedia
{
    using boolT = System.Byte;

    public class HapEnums
    {
        // Available 1st stage compressors
        public enum HapCompressor
        {
            None = 0,
            Snappy,
            GDeflate,
        };

        public enum ShaderModel
        {
            SHADER_MODEL_NONE = 0,
            SHADER_MODEL_5_1,
            SHADER_MODEL_6_0,
            SHADER_MODEL_6_1,
            SHADER_MODEL_6_2,
            SHADER_MODEL_6_3,
            SHADER_MODEL_6_4,
            SHADER_MODEL_6_5,
            SHADER_MODEL_6_6,
        };
    }

    internal partial class NativeDll
    {
        // Native plugin rendering events are only called if a plugin is used
        // by some script. This means we have to DllImport at least
        // one function in some active script.

        #region enums
        // Available texture formats
        public enum NativeTextureFormat
        {
            Unknown = 0,
            Ru8,
            RGu8,
            RGBu8,
            RGBAu8,

            Ru16,
            RGu16,
            RGBu16,
            RGBAu16,

            Rf16,
            RGf16,
            RGBAf16,

            Ri16,
            RGi16,
            RGBAi16,

            Rf32,
            RGf32,
            RGBAf32,
            Ri32,
            RGi32,
            RGBAi32,

            DXT1,   // aka BC1
            DXT5,   // aka BC3
            RGTC1,  // aka BC4
            BPTCfu, // aka BC6U
            BPTCfs, // aka BC6S
            BPTC,   // aka BC7
        };
        #endregion

        #region structs

        // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/customize-struct-marshaling

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct MediaGeneralOpenParams
        {
            [MarshalAs(UnmanagedType.LPStr)] public string path;
            [MarshalAs(UnmanagedType.LPStr)] public string decryptionKey;
            [MarshalAs(UnmanagedType.U1)] public boolT enableHeaderMagicProtection;
            [MarshalAs(UnmanagedType.U1)] public boolT preloadToMemory;
            [MarshalAs(UnmanagedType.I4)] public SyncMode syncMode;
            [MarshalAs(UnmanagedType.I4)] public int videoFrameQueueMemoryLimitMb;
            [MarshalAs(UnmanagedType.I4)] public int packetQueuesMemoryLimitMb;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct MediaAudioOpenParams
        {
            [MarshalAs(UnmanagedType.U1)] public boolT enableAudio;
            [MarshalAs(UnmanagedType.U1)] public boolT useNativeAudioPlugin;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct MediaGraphicsInterfaceOpenParams
        {
            [MarshalAs(UnmanagedType.I4)] public GraphicsInterfaceDeviceType deviceType;
            public System.IntPtr devicePtr;
            [MarshalAs(UnmanagedType.U1)] public boolT useSrgbCompressedTextureFormats;
            [MarshalAs(UnmanagedType.I4)] public FramePresentMode framePresentMode;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct NativeTextureDesc
        {
            [MarshalAs(UnmanagedType.I4)] public NativeTextureFormat nativeFormat;
            public System.IntPtr nativeTexture;
            public System.IntPtr shaderResourceView;
            [MarshalAs(UnmanagedType.I4)] public int width;
            [MarshalAs(UnmanagedType.I4)] public int height;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct NativeTextureData
        {
            [MarshalAs(UnmanagedType.I4)] public NativeTextureFormat nativeFormat;
            public System.IntPtr ptr;
            [MarshalAs(UnmanagedType.I4)] public int size;
            [MarshalAs(UnmanagedType.I4)] public int width;
            [MarshalAs(UnmanagedType.I4)] public int height;

            [MarshalAs(UnmanagedType.I4)] public int uncompressedSize;
            [MarshalAs(UnmanagedType.I4)] public HapEnums.HapCompressor compressor;
            [MarshalAs(UnmanagedType.I4)] public int chunkCount;
            public System.IntPtr chunkPositions;
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 4)]
        public struct GraphicsDeviceInfo
        {
            [MarshalAs(UnmanagedType.U1)] public boolT supportsInt64ShaderOps;
            [MarshalAs(UnmanagedType.U1)] public boolT supports16BitTypes;
            [MarshalAs(UnmanagedType.U1)] public boolT supportsWaveIntrinsics;
            [MarshalAs(UnmanagedType.U1)] public boolT supportsWaveMatch;
            [MarshalAs(UnmanagedType.U4)] public uint SIMDWidth;
            [MarshalAs(UnmanagedType.U4)] public uint SIMDLaneCount;
            [MarshalAs(UnmanagedType.I4)] public HapEnums.ShaderModel supportedShaderModel;

            public bool hasInt64ShaderOps() { return supportsInt64ShaderOps != 0; }
            public bool has16BitTypes() { return supports16BitTypes != 0; }
            public bool hasWaveIntrinsics() { return supportsWaveIntrinsics != 0; }
            public bool hasWaveMatch() { return supportsWaveMatch != 0; }
        };
        #endregion

        #region dllimports

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT Initialize();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Deinitialize();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetPluginVersion(out int major, out int minor, out int revision, out boolT beta);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsDemoVersion();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsProVersion();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int CreateNewMediaId();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void DestroyMediaId(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT Open(int mediaId, MediaGeneralOpenParams generalParams, MediaAudioOpenParams audioParams, MediaGraphicsInterfaceOpenParams giParams);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Close(int mediaId);

		[DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
		public static extern PixelFormat GetPixelFormat(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT AreNativeTexturesCreated(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT NativeTexturesFirstFrameUploaded(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
		public static extern int GetNativeTexturesCount(int mediaId, int frameIndex);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int LockFrameNativeTextures(int mediaId, FrameType frameType);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void UnlockNativeTextures(int mediaId, int frameIndex);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
		public static extern boolT GetNativeTexturePtrByIndex(int mediaId, FrameType frameType, int textureIndex,
                                                             out NativeTextureDesc textureDesc);
        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT GetNativeTextureDataByIndex(int mediaId, FrameType frameType, int textureIndex,
                                                              out NativeTextureData textureData);
        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT GetResolution(int mediaId, out int width, out int height);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT HasAudioStream(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void GetSourceAudioStreamParameters(int mediaId, out int sampleRate, out int channels);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT GetFramedropEnabled(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void GetFramedropCount(int mediaId, out int earlyDrops, out int lateDrops);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetFramedropEnabled(int mediaId, boolT enabled);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsDecodingHardwareAccelerated(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void GetActiveSegment(int mediaId, out float startTime, out float endTime);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetActiveSegment(int mediaId, float startTime, float endTime);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void GetActiveSegmentFrames(int mediaId, out int startFrame, out int endFrame);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetActiveSegmentFrames(int mediaId, int startFrame, int endFrame);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void ResetActiveSegment(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetLoops(int mediaId, int loops);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetLoops(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsNewLoop(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetNumberOfLoopsSinceStart(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT GetAudioMuted(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetAudioMuted(int mediaId, boolT muted);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT GetAudioEnabled(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void DisableAudio(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Play(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Pause(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void TogglePause(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void Stop(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern SyncMode GetSyncMode(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SetSyncMode(int mediaId, SyncMode mode);

        [DllImport((_dllName))]
        public static extern float GetPlaybackRate(int mediaId);

        [DllImport((_dllName))]
        public static extern void SetPlaybackRate(int mediaId, float rate);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SeekToTime(int mediaId, float seconds);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SeekToFrame(int mediaId, int frame);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SeekToStart(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void SeekToEnd(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetNumFrames(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern float GetFramerate(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void StepForward(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void StepBackward(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern float GetDuration(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern float GetCurrentTime(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetCurrentFrame(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern MediaState GetMediaState(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT CanPlay(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsPlaying(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsLooping(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern boolT IsFinished(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern MediaError GetError(int mediaId);

		[DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
		public static extern void SetAudioParams(SampleFormat sampleFormat, int sampleRate, int bufferLength, int channels);

		[DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
		public static extern int FillAudioBuffer(int mediaId, float[] buffer, int offset, int length, int maxChannels);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int DebugGetFrameQueueParams(int mediaId, out int remaining, out int previous, out int current, out int next);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern FramePresentMode GetVideoFramePresentMode(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern void UpdateDisplayedFrameIndex(int mediaId);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern uint GetNumDecodeChunksThreads();

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern bool GetGraphicsDeviceInfo(out GraphicsDeviceInfo deviceInfo);

        [DllImport(_dllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetMaxCompressedFrameSize(int mediaId);

        #endregion
    }
}
