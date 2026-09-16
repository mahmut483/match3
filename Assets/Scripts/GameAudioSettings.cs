using System;
using UnityEngine;

public enum GameAudioChannel
{
    Music,
    Sfx
}

public interface IAudioSettingsStorage
{
    int GetInt(string key, int defaultValue);
    void SetInt(string key, int value);
    void Save();
}

public sealed class PlayerPrefsAudioSettingsStorage : IAudioSettingsStorage
{
    public int GetInt(string key, int defaultValue)
    {
        return PlayerPrefs.GetInt(key, defaultValue);
    }

    public void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }

    public void Save()
    {
        PlayerPrefs.Save();
    }
}

// MainMenu ve GameBoard sahnelerinin kullandığı tek kalıcı ses ayarı kaynağı.
public sealed class GameAudioSettings
{
    private const string MusicPrefsKey = "MusicOn";
    private const string SfxPrefsKey = "SfxOn";
    private const string MusicMixerParameter = "MusicVolume";
    private const string SfxMixerParameter = "SfxVolume";
    private const float EnabledVolumeDb = 0f;
    private const float MutedVolumeDb = -80f;

    private readonly IAudioSettingsStorage storage;

    public static GameAudioSettings Shared { get; } =
        new(new PlayerPrefsAudioSettingsStorage());

    public GameAudioSettings(IAudioSettingsStorage storage)
    {
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public bool IsEnabled(GameAudioChannel channel)
    {
        return storage.GetInt(GetPrefsKey(channel), 1) == 1;
    }

    public void SetEnabled(GameAudioChannel channel, bool isEnabled)
    {
        storage.SetInt(GetPrefsKey(channel), isEnabled ? 1 : 0);
        storage.Save();
    }

    public bool Toggle(GameAudioChannel channel)
    {
        bool isEnabled = !IsEnabled(channel);
        SetEnabled(channel, isEnabled);

        return isEnabled;
    }

    public float GetVolumeDb(GameAudioChannel channel)
    {
        return IsEnabled(channel) ? EnabledVolumeDb : MutedVolumeDb;
    }

    public static string GetMixerParameter(GameAudioChannel channel)
    {
        return channel == GameAudioChannel.Music
            ? MusicMixerParameter
            : SfxMixerParameter;
    }

    private static string GetPrefsKey(GameAudioChannel channel)
    {
        return channel == GameAudioChannel.Music ? MusicPrefsKey : SfxPrefsKey;
    }
}
