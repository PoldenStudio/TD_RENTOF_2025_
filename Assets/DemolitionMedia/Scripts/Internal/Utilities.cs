using System;

#if UNITY_5_3_OR_NEWER
using UnityEngine;
#endif

namespace DemolitionStudios.DemolitionMedia
{
    public static class Utilities
    {
        public static byte toByte(bool v)
        {
            return v ? (byte)1 : (byte)0;
        }

        public static void Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = value;
            }
		}

        public static bool ApproximatelyEqual(float a, float b)
        {
            const float epsilon = 1.0e-05f;
            if (Math.Abs(a - b) <= epsilon)
                return true;
            return Math.Abs(a - b) <= epsilon * Math.Max(Math.Abs(a), Math.Abs(b));
        }

        public static bool ApproximatelyEqual(double a, double b)
        {
            const float epsilon = 1.0e-05f;
            if (Math.Abs(a - b) <= epsilon)
                return true;
            return Math.Abs(a - b) <= epsilon * Math.Max(Math.Abs(a), Math.Abs(b));
        }

#if UNITY_5_3_OR_NEWER
        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            var type = original.GetType();
            var copy = destination.AddComponent(type);
            var fields = type.GetFields();
            foreach (var field in fields) 
                field.SetValue(copy, field.GetValue(original));
            return copy as T;
        }
#endif

        public static void Log(string logMsg)
        {
#if !DEMOLITION_MEDIA_DISABLE_LOGS && !DEMOLITION_MEDIA_DISABLE_INFO_LOGS
            Debug.Log(logMsg);
#endif
        }

        public static void LogError(string logMsg)
        {
#if !DEMOLITION_MEDIA_DISABLE_LOGS
            Debug.LogError(logMsg);
#endif
        }

        public static void LogWarning(string logMsg)
        {
#if !DEMOLITION_MEDIA_DISABLE_LOGS
            Debug.LogWarning(logMsg);
#endif
        }
    }

    class DisplayEventEntry
    {
        public DisplayEventEntry(string eventName, float timer)
        {
            EventName = eventName;
            Timer = timer;
        }

        public bool DecrementTimer(float dt)
        {
            Timer -= dt;
            return Timer > 0.0f;
        }

        public string EventName;
        public float Timer;
    }
}
