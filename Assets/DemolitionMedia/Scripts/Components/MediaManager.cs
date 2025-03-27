using UnityEngine;


namespace DemolitionStudios.DemolitionMedia
{
    /// Native plugin version which should work with the current C# scripts
    public static class NativePluginVersion
    {
        public const int MAJOR    = 3;
        public const int MINOR    = 2;
        public const int REVISION = 0;
        public const bool BETA = false;

        /// Returns the version string
        public static string GetString()
        {
            return MAJOR + "." + MINOR + "." + REVISION + (BETA ? " beta" : "");
        }
    }

    /// <summary>
    ///     A singleton media manger class.
    ///     Handles rendering and other global stuff.
    /// </summary>
    [AddComponentMenu("Demolition Media/Media Manager (must have)")]
    public class MediaManager : MonoBehaviour
    {

        /// Whether initialized
        private bool _initialized;

        /// Singleton instance
        private static MediaManager _instance;
        public static MediaManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to find component in the scene
                    _instance = (MediaManager)GameObject.FindObjectOfType(typeof(MediaManager));

                    if (_instance == null)
                    {
                        Utilities.LogError("[DemolitionMedia] you need to add DemolitionMediaManager component to the scene!");
                        return null;
                    }

                    if (!_instance._initialized)
                        _instance.Initialize();
                }

                return _instance;
            }
        }

        void Awake()
        {
            if (!_initialized)
            {
                _instance = this;
                Initialize();
            }

        }

        void OnDestroy()
        {
            Deinitialize();
        }

        private bool Initialize()
        {
            try
            {
                // Initialize the native plugin
                if (NativeDll.Initialize() == 0)
                {
                    Utilities.LogError("[DemolitionMedia] native plugin initialization failed!");
                    Deinitialize();
                    this.enabled = false;
                    return false;
                }

                // Get the native plugin version
                int major, minor, revision;
                byte beta;
                NativeDll.GetPluginVersion(out major, out minor, out revision, out beta);
                bool pro = NativeDll.IsProVersion() != 0;

                Utilities.Log("[DemolitionMedia] Native plugin version: " + major + "." + minor + "." + revision + 
                    (beta != 0 ? " beta" : "") + (pro ? " PRO" : ""));

                if (major != NativePluginVersion.MAJOR ||
                    minor != NativePluginVersion.MINOR ||
                    revision != NativePluginVersion.REVISION)
                {
                    Utilities.LogError("[DemolitionMedia] this version of C# scripts is supposed to work with native plugin version " +
                                     NativePluginVersion.MAJOR + "." + 
                                     NativePluginVersion.MINOR + "." + 
                                     NativePluginVersion.REVISION);
                    Utilities.LogError("[DemolitionMedia] you might need to update the C# scripts in order to make it work correctly with the current native plugin version");
                }

                if (NativeDll.IsDemoVersion() != 0)
                {
                    Utilities.LogWarning("[DemolitionMedia] this is demo version of the DemolitionMedia plugin. Video texture will periodically have some distortions. Get the full version on the Asset Store");
                }
                if (beta != 0)
                {
                    Utilities.LogWarning("[DemolitionMedia] this is beta version of the DemolitionMedia plugin");
                }
            }
            catch (System.DllNotFoundException e)
            {
                Utilities.LogError("[DemolitionMedia] couldn't load the native plugin DLL");
                throw e;
            }


            _initialized = true;
            return true;
        }

        private void Deinitialize()
        {
            // Clean up any open movies
            Media[] medias = (Media[])FindObjectsOfType(typeof(Media));

            if (medias != null)
            {
                for (int i = 0; i < medias.Length; i++) {
                    medias[i].Close();
                }
            }

            _instance = null;
            _initialized = false;

            Utilities.Log("[DemolitionMedia] Shutting down native plugin");
            NativeDll.Deinitialize();
        }

    }
}