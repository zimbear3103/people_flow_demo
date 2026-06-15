using UnityEngine;

namespace PeopleFlow
{
    /// <summary>
    /// Plays SFX and background music. All clips are <see cref="SerializeField"/> so you can drop
    /// your own (royalty-free) clips on the AudioManager object; if a clip is unassigned the call
    /// simply no-ops, so the game runs silent-but-fine out of the box. Respects saved settings.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("SFX clips (optional — assign your own)")]
        [SerializeField] AudioClip m_push;          // character stepping onto the runway
        [SerializeField] AudioClip m_fill;          // a runner drops into a hole
        [SerializeField] AudioClip m_holeComplete;  // a hole is fully filled
        [SerializeField] AudioClip m_win;
        [SerializeField] AudioClip m_lose;
        [SerializeField] AudioClip m_uiClick;

        [Header("Music (optional)")]
        [SerializeField] AudioClip m_music;
        [Range(0f, 1f)] [SerializeField] float m_musicVolume = 0.4f;
        [Range(0f, 1f)] [SerializeField] float m_sfxVolume = 0.9f;

        AudioSource m_sfxSource;
        AudioSource m_musicSource;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            m_sfxSource = gameObject.AddComponent<AudioSource>();
            m_sfxSource.playOnAwake = false;

            m_musicSource = gameObject.AddComponent<AudioSource>();
            m_musicSource.playOnAwake = false;
            m_musicSource.loop = true;
            m_musicSource.volume = m_musicVolume;

            if (m_music != null && SaveManager.MusicOn)
            {
                m_musicSource.clip = m_music;
                m_musicSource.Play();
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlayPush() => PlaySfx(m_push, 0.6f);
        public void PlayFill() => PlaySfx(m_fill);
        public void PlayHoleComplete() => PlaySfx(m_holeComplete);
        public void PlayWin() => PlaySfx(m_win);
        public void PlayLose() => PlaySfx(m_lose);
        public void PlayClick() => PlaySfx(m_uiClick);

        void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || m_sfxSource == null || !SaveManager.SfxOn) return;
            m_sfxSource.PlayOneShot(clip, m_sfxVolume * volumeScale);
        }

        public void SetMusicOn(bool on)
        {
            SaveManager.MusicOn = on;
            if (m_musicSource == null) return;
            if (on)
            {
                if (m_music != null) { m_musicSource.clip = m_music; m_musicSource.Play(); }
            }
            else m_musicSource.Stop();
        }
    }
}
