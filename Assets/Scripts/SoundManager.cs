using System.Collections;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class Sound
{
    [SerializeField] string m_name;
    [SerializeField] AudioClip m_clip;
    public string Name => m_name;
    public AudioClip Clip => m_clip;
}

public enum ESoundId
{
    Bg_Deafeat,
    Bg_MainGame,
    Bg_MainMenu,
    Bg_Victory,
    UI_Click_ButtonMain,
    UI_Click_ButtonNegative,
    UI_Click_Other,
    Ingame_BulletImpact,
    Ingame_CapyRun,
    Ingame_CrateCollision,
    Ingame_CrateExplode,
    Ingame_EnemyAttack,
    Ingame_EnemySplash,
    Ingame_GeneralBooster,
    Ingame_GunShot,
    Ingame_Overload,
    Voice_CapyFear,
    Voice_CapyHit,
    Voice_CapyWin
}

public class SoundManager : Singleton<SoundManager>
{
    [Header("Audio Source")]
    [SerializeField] AudioSource m_sfxSource;
    [SerializeField] AudioSource m_musicSource;
    [SerializeField] AudioSource m_voiceSource;

    [Header("Audio Clip")]
    [SerializeField] Sound[] m_sfxSounds;
    [SerializeField] Sound[] m_musicSounds;
    [SerializeField] Sound[] m_voiceSounds;

    private bool m_isPauseSound = false;
    private bool m_isPauseVoice = false;

    private IEnumerator Start()
    {
        yield return new WaitUntil(() => UserProfile.Instance.IsInitialized);

        SetMuteSFX(!UserProfile.Instance.OnSFX);
        SetMuteVoice(!UserProfile.Instance.OnSFX);
        SetMuteMusic(!UserProfile.Instance.OnMusic);
    }

    public void OnActiveBackgroundMusic(bool isActive)
    {
        if (isActive)
        {
            if (m_musicSource != null && !m_musicSource.isPlaying)
                m_musicSource.Play();
        }
        else
        {
            if (m_musicSource != null && m_musicSource.isPlaying)
                m_musicSource.Stop();
        }
    }
    public void PlayMusic(float volume)
    {
        m_musicSource.volume = volume;

        if (m_musicSource != null && !m_musicSource.isPlaying)
        {
            m_musicSource.Play();
        }
    }
    public void PlaySFX(string name)
    {
        var sound = System.Array.Find(m_sfxSounds, s => s.Name == name);

        if (sound != null)
        {
            //GameLog.Log(LogType.Log, $"[SoundManager] PlaySFX: {name}");
            m_sfxSource.PlayOneShot(sound.Clip);
        }
    }

    public void StopSFX()
    {
        m_sfxSource.Stop();//hnhp: this logic maybe dangerous
    }
    public void ChangeValueSound(float valueMusic, float valueSFX)
    {
        m_musicSource.volume = valueMusic;
        m_sfxSource.volume = valueSFX;
    }
    public float GetMusicVolume()
    {
        return m_musicSource.volume;
    }
    public float GetSFXVolume()
    {
        return m_sfxSource.volume;
    }

    public void SetMuteSFX(bool isMute)
    {
        m_sfxSource.mute = isMute;
    }

    public void SetMuteMusic(bool isMute)
    {
        m_musicSource.mute = isMute;
    }
    public void SetMuteVoice(bool isMute)
    {
        m_voiceSource.mute = isMute;
    }

    #region SFX
    public Sound GetSoundSfxAudioClip(ESoundId soundId)
    {
        string name = soundId.ToString();
        return m_sfxSounds.FirstOrDefault(clip => clip.Name == name);
    }

    public void OnPlaySfxAudio(ESoundId soundId)
    {
        OnPlaySfxAudio(GetSoundSfxAudioClip(soundId));
    }

    public void OnPlaySfxAudio(Sound soundData)
    {
        OnPlaySfxAudio(soundData.Clip);
    }

    public void OnPlaySfxAudio(AudioClip soundData)
    {
        //if (m_sfxSource.isPlaying)
        //    m_sfxSource.Stop();

        m_sfxSource.PlayOneShot(soundData);
    }
    #endregion //SFX

    #region Music
    public Sound GetSoundMusicAudioClip(ESoundId soundId)
    {
        string name = soundId.ToString();
        return m_musicSounds.FirstOrDefault(clip => clip.Name == name);
    }
    public void OnPlayMusic(Sound soundData, bool isLoop, float volume = 1.0f)
    {
        OnPlayMusic(soundData.Clip, isLoop, volume);
    }
    public void OnPlayMusic(ESoundId soundId, bool isLoop, float volume = 1.0f)
    {
        OnPlayMusic(GetSoundMusicAudioClip(soundId), isLoop, volume);
    }
    public void OnPlayMusic(AudioClip soundData, bool isLoop, float volume = 1.0f)
    {
        if (m_musicSource.isPlaying)
            m_musicSource.Stop();
        m_musicSource.volume = volume;
        m_musicSource.loop = isLoop;
        m_musicSource.clip = soundData;
        m_musicSource.Play();
        m_isPauseSound = false;
    }
    public void OnPauseMusic()
    {
        if (m_musicSource.isPlaying)
        {
            m_musicSource.Pause();
            m_isPauseSound = true;
        }
    }
    public void OnResumeMusic()
    {
        if (m_isPauseSound)
        {
            m_musicSource.UnPause();
            m_isPauseSound = false;
        }
    }
    #endregion //Music

    #region Voice
    public Sound GetSoundVoiceAudioClip(ESoundId soundId)
    {
        string name = soundId.ToString();
        return m_voiceSounds.FirstOrDefault(clip => clip.Name == name);
    }

    public void OnPlayVoiceAudio(ESoundId soundId)
    {
        OnPlayVoiceAudio(GetSoundVoiceAudioClip(soundId));
    }

    public void OnPlayVoiceAudio(Sound soundData)
    {
        OnPlayVoiceAudio(soundData.Clip);
    }

    public void OnPlayVoiceAudio(AudioClip soundData)
    {
        if (m_voiceSource.isPlaying)
            m_voiceSource.Stop();

        m_voiceSource.PlayOneShot(soundData);
    }
    #endregion //Voice
}
