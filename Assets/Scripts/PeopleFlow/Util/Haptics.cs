using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Light haptic feedback wrapper. On device it issues a short vibration; in the Editor it
    /// no-ops. Uses the Android Vibrator service for a *short* buzz (Handheld.Vibrate is ~500ms,
    /// too heavy for "light" feedback), and falls back to Handheld.Vibrate on iOS.
    /// </summary>
    public static class Haptics
    {
        public static void Light()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(20);   // 20ms tick
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

        public static void Success()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(40);
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static void VibrateAndroid(long milliseconds)
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator != null)
                        vibrator.Call("vibrate", milliseconds);
                }
            }
            catch
            {
                Handheld.Vibrate();
            }
        }
#endif
    }
}
