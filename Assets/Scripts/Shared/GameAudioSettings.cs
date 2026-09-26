using UnityEngine;

namespace Match3.Shared
{
    public enum GameAudioChannel
    {
        Music,
        Sfx
    }

    // MainMenu ve GameBoard sahnelerinin kullandığı tek kalıcı ses ayarı kaynağı.
    // Açık/kapalı durumu PlayerPrefs'te tutulur.
    public static class GameAudioSettings
    {
        private const string MusicPrefsKey = "MusicOn";
        private const string SfxPrefsKey = "SfxOn";
        private const string MusicMixerParameter = "MusicVolume";
        private const string SfxMixerParameter = "SfxVolume";
        private const float EnabledVolumeDb = 0f;
        private const float MutedVolumeDb = -80f;

        public static bool IsEnabled(GameAudioChannel channel)
        {
            return PlayerPrefs.GetInt(GetPrefsKey(channel), 1) == 1;
        }

        // Kanalı açıksa kapatır, kapalıysa açar; yeni durumu döner.
        public static bool Toggle(GameAudioChannel channel)
        {
            bool isEnabled = !IsEnabled(channel);

            PlayerPrefs.SetInt(GetPrefsKey(channel), isEnabled ? 1 : 0);
            PlayerPrefs.Save();

            return isEnabled;
        }

        public static float GetVolumeDb(GameAudioChannel channel)
        {
            return IsEnabled(channel) ? EnabledVolumeDb : MutedVolumeDb;
        }

        public static string GetMixerParameter(GameAudioChannel channel)
        {
            return channel == GameAudioChannel.Music ? MusicMixerParameter : SfxMixerParameter;
        }

        private static string GetPrefsKey(GameAudioChannel channel)
        {
            return channel == GameAudioChannel.Music ? MusicPrefsKey : SfxPrefsKey;
        }
    }
}
