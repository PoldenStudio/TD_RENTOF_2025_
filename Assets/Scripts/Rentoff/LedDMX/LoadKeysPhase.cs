using IA;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace InitializationFramework
{
    public class LoadKeysPhase : MonoBehaviour, IInitializable
    {
        [System.Serializable]
        public class Settings
        {
            public int leftAmbilightIndex = 1;

            public int rightAmbilightIndex = 140;

            public int upperAmbilightIndex = 400;

            public int lowerAmbilightIndex = 410;

            public int kinetic_L = 509;

            public int kinetic_R = 511;
        }

        [SerializeField]
        Settings st;

        [SerializeField]
        string fn = "keys.data";

        [SerializeField]
        DMX_semi db;

        [SerializeField]
        bool bypassOnInit = true;

        // Добавлено: Ссылка на LEDController
        [SerializeField]
        private LEDControl.LEDController ledController;


        // Добавлено: Байтовый массив для хранения данных
        private byte[] dmxData;

        public IEnumerator Initialize(System.Action<UnityEngine.Object> OnFinished)
        {
            gameObject.SetActive(true);

            var absPath = Application.streamingAssetsPath + System.IO.Path.AltDirectorySeparatorChar + fn;

            try
            {
                if (System.IO.File.Exists(absPath))
                {
                    var fl = System.IO.File.OpenRead(absPath);

                    using (var str = new System.IO.BinaryReader(fl))
                    {
                        int leftDiodsCount = 35;
                        int rightDiodsCount = 35;
                        int rgbw = 1 + 1 + 1 + 1;
                        int rgbwmix = 1 + 1 + 1 + 1 + 1;
                        int kineticChannels = 1 + 1;

                        byte[] leftdiodsKeys = new byte[35 * rgbw];
                        byte[] rightdiodsKeys = new byte[35 * rgbw];
                        byte[] upperLight = new byte[rgbwmix];
                        byte[] lowerLight = new byte[rgbwmix];
                        byte[] kineticData_L = new byte[kineticChannels];
                        byte[] kineticData_R = new byte[kineticChannels];

                        int totalFrameBytes = leftdiodsKeys.Length + rightdiodsKeys.Length + upperLight.Length + lowerLight.Length + kineticData_L.Length + kineticData_R.Length;

                        // Инициализируем массив dmxData нужным размером
                        dmxData = new byte[512];

                        int frameIndex = 0;

                        for (long i = 0; i < str.BaseStream.Length; i += totalFrameBytes)
                        {
                            float fc = (float)i / (float)str.BaseStream.Length;

                            leftdiodsKeys = str.ReadBytes(leftDiodsCount * rgbw);
                            rightdiodsKeys = str.ReadBytes(rightDiodsCount * rgbw);
                            upperLight = str.ReadBytes(rgbwmix);
                            lowerLight = str.ReadBytes(rgbwmix);
                            kineticData_L = str.ReadBytes(kineticChannels);
                            kineticData_L[0] = (byte)(fc * 125f);
                            kineticData_R = str.ReadBytes(kineticChannels);
                            kineticData_R[0] = (byte)(fc * 125f);

                            db.frames[frameIndex] = (db.frames[frameIndex] == null) ? new DMX_semi.DMXFrame() : db.frames[frameIndex];

                            // Копируем данные во временный буфер
                            Array.Copy(leftdiodsKeys, 0, dmxData, st.leftAmbilightIndex - 1, leftdiodsKeys.Length);
                            Array.Copy(rightdiodsKeys, 0, dmxData, st.rightAmbilightIndex - 1, rightdiodsKeys.Length);
                            Array.Copy(upperLight, 0, dmxData, st.upperAmbilightIndex - 1, upperLight.Length);
                            Array.Copy(lowerLight, 0, dmxData, st.lowerAmbilightIndex - 1, lowerLight.Length);
                            Array.Copy(kineticData_L, 0, dmxData, st.kinetic_L - 1, kineticData_L.Length);
                            Array.Copy(kineticData_R, 0, dmxData, st.kinetic_R - 1, kineticData_R.Length);

                            ++frameIndex;
                        }
                    }

                    // После загрузки всех данных сохраняем массив в LEDController
                    if (ledController != null)
                    {
                        ledController.DirectDMXData = dmxData;
                    }
                    else
                    {
                        Debug.LogError("LEDController не назначен в LoadKeysPhase!");
                    }

                }
            }
            catch (System.Exception err)
            {
                print(err);
            }

            yield return null;
        }

        public IEnumerator Deinitialize(System.Action<UnityEngine.Object> OnFinished)
        {
            yield return null;
        }

        private void Reset()
        {
            gameObject.SetActive(false);
        }

        Color RGBtoRGBW(Color rgb)
        {
            float r = (rgb.r * 255.0f);
            float g = (rgb.g * 255.0f);
            float b = (rgb.b * 255.0f);

            float kWhiteRedChannel = 255f;
            float kWhiteGreenChannel = 209f;
            float kWhiteBlueChannel = 163f;

            Color res = new Color();

            float whiteValueForRed = r / kWhiteRedChannel;
            float whiteValueForGreen = g / kWhiteGreenChannel;
            float whiteValueForBlue = b / kWhiteBlueChannel;

            float minWhiteValue = Mathf.Min(whiteValueForRed,
                                       Mathf.Min(whiteValueForGreen,
                                           whiteValueForBlue));

            res.r = (byte)(r - minWhiteValue * kWhiteRedChannel / 255f);
            res.g = (byte)(g - minWhiteValue * kWhiteGreenChannel / 255f);
            res.b = (byte)(b - minWhiteValue * kWhiteBlueChannel / 255f);
            res.a = (byte)(minWhiteValue * 255f);

            return res;
        }
    }
}