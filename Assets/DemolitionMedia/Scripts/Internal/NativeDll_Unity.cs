using System;
using System.Runtime.InteropServices;


namespace DemolitionStudios.DemolitionMedia
{
    internal partial class NativeDll
    {
        // TODO: https://stackoverflow.com/questions/39323153/plugin-works-in-linux-unity-editor-but-not-in-standalone-linux-build
#if UNITY_IPHONE && !UNITY_EDITOR
		private const string _dllName = "__Internal";
#else
        private const string _dllName = "AudioPluginDemolitionMedia";
#endif

        [DllImport(_dllName)]
        public static extern IntPtr GetRenderEventFunc();

        [DllImport(_dllName)]
        public static extern IntPtr TextureUpdater_GetTextureUpdateCallback();
    }
}