using System;
using System.Collections;
using UnityEngine;
using System.IO;

namespace InitializationFramework
{
    public class ReadSettings : MonoBehaviour, IInitializable
    {
        [SerializeField] GlobalSettings gs;

        [Serializable]
        class Settings_JSON
        {
            public class GeneralSettings
            {
                public int FPS { get; set; }
                public float InputScaleFactor { get; set; }
                public float Threshold { get; set; }
            }

            public GeneralSettings GeneralAppSettings;

            public class NetworkSettings
            {
                public int Recieve_UDP_Port { get; set; }
            }

            public NetworkSettings NetworkAppSettings;

            public class DMX_Light_Settings
            {
                public string DMX_Port_Name { get; set; }
                public int DMX_Update_Rate { get; set; }
                public ushort UniverseToWrite { get; set; }
            }

            public DMX_Light_Settings DMX_Settings { get; set; }
        }

        public IEnumerator Initialize(System.Action<UnityEngine.Object> OnFinished)
        {
            gameObject.SetActive(true);

            DirectoryInfo dirInfo = Directory.GetParent(Application.dataPath);
            string nm = "settings.json";
            string folderPath = Path.Combine(dirInfo.FullName, nm);

            Settings_JSON settings = null;

            try
            {
                string inputJSON = File.ReadAllText(folderPath);
                settings = JsonUtility.FromJson<Settings_JSON>(inputJSON);
            }
            catch (Exception err)
            {
                Debug.LogError($"Ошибка чтения файла настроек: {err.Message}");
                settings = null;
            }

            if (settings != null)
            {
                ParseGeneralSettings(settings);
                ParseNetworkSettings(settings);
                ParseDMX(settings);
            }
            else
            {
                Debug.Log("Используются настройки по умолчанию из-за ошибки чтения");
                SetupDefaultSettings();
            }

            yield return null;
        }

        void ParseNetworkSettings(Settings_JSON settings)
        {
            gs.networkSettings.commandsRecievePort = settings.NetworkAppSettings.Recieve_UDP_Port;
        }

        void ParseGeneralSettings(Settings_JSON settings)
        {
            gs.generalSettings.targetFPS = settings.GeneralAppSettings.FPS;
            Application.targetFrameRate = gs.generalSettings.targetFPS;
            gs.generalSettings.inputScaleFactor = settings.GeneralAppSettings.InputScaleFactor;
            gs.generalSettings.threshold = settings.GeneralAppSettings.Threshold;
        }

        void ParseDMX(Settings_JSON settings)
        {
            gs.dmxSettings.dmxPortName = settings.DMX_Settings.DMX_Port_Name;
            gs.dmxSettings.dmxUpdateRate = settings.DMX_Settings.DMX_Update_Rate;
            gs.dmxSettings.dmxUniverseIndex = settings.DMX_Settings.UniverseToWrite;
        }

        void SetupDefaultSettings()
        {
            gs.ResetData();
        }

        public IEnumerator Deinitialize(System.Action<UnityEngine.Object> OnFinished)
        {
            gameObject.SetActive(false);
            yield return null;
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }
    }
}