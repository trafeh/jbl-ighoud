using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JbelIghoud.Audio
{
    /// <summary>
    /// Master Audio Manager for Jbel Ighoud:
    /// - Manages Master, SFX, Music, and Voice volume channels.
    /// - Saves & loads audio settings persistently via PlayerPrefs.
    /// - Plays the theme song periodically (every ~3 minutes) with smooth fade-in/fade-out.
    /// - Controls voice playback with automatic music ducking.
    /// - Manages subtitles enable/disable state.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudioManager : MonoBehaviour
    {
        private static GameAudioManager _instance;
        public static GameAudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<GameAudioManager>(FindObjectsInactive.Include);
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[GameAudioManager]");
                        _instance = go.AddComponent<GameAudioManager>();
                    }
                }
                return _instance;
            }
        }

        private const string PREF_MASTER = "JBEL_AUDIO_MASTER";
        private const string PREF_MUSIC = "JBEL_AUDIO_MUSIC";
        private const string PREF_VOICE = "JBEL_AUDIO_VOICE";
        private const string PREF_SFX = "JBEL_AUDIO_SFX";
        private const string PREF_SUBTITLES = "JBEL_AUDIO_SUBTITLES";

        [Header("========================================")]
        [Header("--- AUDIO SOURCES ---")]
        [Header("========================================")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource voiceSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("========================================")]
        [Header("--- MUSIC SETTINGS ---")]
        [Header("========================================")]
        [SerializeField] private AudioClip themeMusicClip;
        [Tooltip("Interval in seconds between music playthroughs (e.g. 60s pause + 120s song = 180s / 3 minutes total).")]
        [SerializeField] private float musicPauseInterval = 60f;
        [SerializeField] private float musicFadeDuration = 2.5f;

        [Header("========================================")]
        [Header("--- CURRENT VOLUMES ---")]
        [Header("========================================")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.85f;
        [SerializeField] private bool subtitlesEnabled = true;

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float VoiceVolume => voiceVolume;
        public float SFXVolume => sfxVolume;
        public bool SubtitlesEnabled => subtitlesEnabled;

        public event System.Action<float, float, float, float> OnVolumesChanged;
        public event System.Action<bool> OnSubtitlesToggled;
        public event System.Action<AudioClip> OnVoiceStarted;
        public event System.Action OnVoiceEnded;

        private Coroutine musicCycleCoroutine;
        private Coroutine voiceDuckingCoroutine;
        private float currentMusicFade = 1f;
        private float currentDuckFactor = 1f;
        private bool isVoiceActive = false;

        // Universal Button Audio Feedback
        private readonly HashSet<UnityEngine.UI.Button> registeredButtons = new HashSet<UnityEngine.UI.Button>();
        private UnityEngine.Events.UnityAction globalButtonClickListener;
        private float buttonScanTimer = 0f;
        private float lastClickSoundTime = -1f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            InitializeAudioSources();
            LoadSettings();

            globalButtonClickListener = OnGlobalButtonClicked;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            registeredButtons.Clear();
            RegisterAllButtonsInScene();
        }

        private void Start()
        {
            ApplyAllVolumes();

            if (themeMusicClip == null)
            {
                themeMusicClip = Resources.Load<AudioClip>("Tranquil Exploration");
                if (themeMusicClip == null)
                {
#if UNITY_EDITOR
                    themeMusicClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Tranquil Exploration.wav");
#endif
                }
            }

            StartMusicCycle();
            RegisterAllButtonsInScene();
        }

        private void Update()
        {
            buttonScanTimer += Time.unscaledDeltaTime;
            if (buttonScanTimer >= 0.4f)
            {
                buttonScanTimer = 0f;
                RegisterAllButtonsInScene();
            }
        }

        private void InitializeAudioSources()
        {
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = false;
                musicSource.spatialBlend = 0f; // Pure 2D
                musicSource.priority = 64;
            }

            if (voiceSource == null)
            {
                voiceSource = gameObject.AddComponent<AudioSource>();
                voiceSource.playOnAwake = false;
                voiceSource.loop = false;
                voiceSource.spatialBlend = 0f; // Clean 2D dialogue
                voiceSource.priority = 32;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.loop = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.priority = 128;
            }
        }

        // =========================================================
        // --- VOLUME SETTINGS & PERSISTENCE ---
        // =========================================================

        public void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat(PREF_MASTER, 1.0f);
            musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC, 0.75f);
            voiceVolume = PlayerPrefs.GetFloat(PREF_VOICE, 1.0f);
            sfxVolume = PlayerPrefs.GetFloat(PREF_SFX, 0.85f);
            subtitlesEnabled = PlayerPrefs.GetInt(PREF_SUBTITLES, 1) == 1;

            ApplyAllVolumes();
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat(PREF_MASTER, masterVolume);
            PlayerPrefs.SetFloat(PREF_MUSIC, musicVolume);
            PlayerPrefs.SetFloat(PREF_VOICE, voiceVolume);
            PlayerPrefs.SetFloat(PREF_SFX, sfxVolume);
            PlayerPrefs.SetInt(PREF_SUBTITLES, subtitlesEnabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = masterVolume;
            ApplyAllVolumes();
            SaveSettings();
            OnVolumesChanged?.Invoke(masterVolume, musicVolume, voiceVolume, sfxVolume);
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            UpdateMusicSourceVolume();
            SaveSettings();
            OnVolumesChanged?.Invoke(masterVolume, musicVolume, voiceVolume, sfxVolume);
        }

        public void SetVoiceVolume(float volume)
        {
            voiceVolume = Mathf.Clamp01(volume);
            UpdateVoiceSourceVolume();
            SaveSettings();
            OnVolumesChanged?.Invoke(masterVolume, musicVolume, voiceVolume, sfxVolume);
        }

        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            UpdateSFXSourceVolume();
            SaveSettings();
            OnVolumesChanged?.Invoke(masterVolume, musicVolume, voiceVolume, sfxVolume);
        }

        public void SetSubtitlesEnabled(bool enabled)
        {
            subtitlesEnabled = enabled;
            SaveSettings();
            OnSubtitlesToggled?.Invoke(subtitlesEnabled);
        }

        public void ToggleSubtitles()
        {
            SetSubtitlesEnabled(!subtitlesEnabled);
        }

        private void ApplyAllVolumes()
        {
            AudioListener.volume = masterVolume;
            UpdateMusicSourceVolume();
            UpdateVoiceSourceVolume();
            UpdateSFXSourceVolume();
        }

        private void UpdateMusicSourceVolume()
        {
            if (musicSource != null)
            {
                musicSource.volume = musicVolume * masterVolume * currentMusicFade * currentDuckFactor;
            }
        }

        private void UpdateVoiceSourceVolume()
        {
            if (voiceSource != null)
            {
                voiceSource.volume = voiceVolume * masterVolume;
            }
        }

        private void UpdateSFXSourceVolume()
        {
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume * masterVolume;
            }
        }

        // =========================================================
        // --- MUSIC PERIODIC PLAYBACK (Every ~3 Minutes) ---
        // =========================================================

        /// <summary>True while a menu/loading screen owns the music (the in-game theme must stay silent).</summary>
        public static bool SuppressMusicCycle;

        public void StartMusicCycle()
        {
            if (SuppressMusicCycle) { StopMusicCycle(); return; }
            if (musicCycleCoroutine != null) StopCoroutine(musicCycleCoroutine);
            musicCycleCoroutine = StartCoroutine(MusicPeriodicCycleRoutine());
        }

        public void StopMusicCycle()
        {
            if (musicCycleCoroutine != null)
            {
                StopCoroutine(musicCycleCoroutine);
                musicCycleCoroutine = null;
            }
            if (musicSource != null) musicSource.Stop();
        }

        private IEnumerator MusicPeriodicCycleRoutine()
        {
            // Initial short delay on scene load for smooth startup
            yield return new WaitForSeconds(1.5f);

            while (!SuppressMusicCycle)
            {
                if (themeMusicClip != null && musicSource != null)
                {
                    musicSource.clip = themeMusicClip;
                    musicSource.time = 0f;
                    currentMusicFade = 0f;
                    UpdateMusicSourceVolume();
                    musicSource.Play();

                    // Fade in
                    float fadeInTimer = 0f;
                    while (fadeInTimer < musicFadeDuration)
                    {
                        fadeInTimer += Time.unscaledDeltaTime;
                        currentMusicFade = Mathf.Clamp01(fadeInTimer / musicFadeDuration);
                        UpdateMusicSourceVolume();
                        yield return null;
                    }
                    currentMusicFade = 1f;
                    UpdateMusicSourceVolume();

                    // Play track while checking for end
                    while (musicSource.isPlaying && musicSource.time < themeMusicClip.length - musicFadeDuration)
                    {
                        yield return new WaitForSeconds(0.5f);
                    }

                    // Fade out near end
                    float fadeOutTimer = 0f;
                    while (fadeOutTimer < musicFadeDuration && musicSource.isPlaying)
                    {
                        fadeOutTimer += Time.unscaledDeltaTime;
                        currentMusicFade = Mathf.Clamp01(1f - (fadeOutTimer / musicFadeDuration));
                        UpdateMusicSourceVolume();
                        yield return null;
                    }

                    musicSource.Stop();
                    currentMusicFade = 1f;
                }

                // Pause interval: user specified "from time to time like 3min to 3min"
                // Music length ~ 120s + 60s pause = ~180s (3 minutes) per full cycle
                yield return new WaitForSeconds(musicPauseInterval);
            }
        }

        // =========================================================
        // --- VOICE PLAYBACK & MUSIC DUCKING ---
        // =========================================================

        public bool IsVoicePlaying => isVoiceActive || (voiceSource != null && voiceSource.isPlaying);
        public float VoiceCurrentTime => voiceSource != null ? voiceSource.time : 0f;

        public void PlayVoice(AudioClip clip)
        {
            if (clip == null || voiceSource == null) return;

            isVoiceActive = true;
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.time = 0f;
            UpdateVoiceSourceVolume();
            voiceSource.Play();

            OnVoiceStarted?.Invoke(clip);

            if (voiceDuckingCoroutine != null) StopCoroutine(voiceDuckingCoroutine);
            voiceDuckingCoroutine = StartCoroutine(VoiceDuckingAndMonitorRoutine(clip.length));
        }

        public void StopVoice()
        {
            isVoiceActive = false;
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }
            if (voiceDuckingCoroutine != null)
            {
                StopCoroutine(voiceDuckingCoroutine);
                voiceDuckingCoroutine = null;
            }
            currentDuckFactor = 1f;
            UpdateMusicSourceVolume();
            OnVoiceEnded?.Invoke();
        }

        private IEnumerator VoiceDuckingAndMonitorRoutine(float duration)
        {
            // Smoothly duck music to 30% for crystal clear voice
            float duckSpeed = 0.5f;
            float t = 0f;
            while (t < duckSpeed)
            {
                t += Time.unscaledDeltaTime;
                currentDuckFactor = Mathf.Lerp(1f, 0.3f, t / duckSpeed);
                UpdateMusicSourceVolume();
                yield return null;
            }
            currentDuckFactor = 0.3f;
            UpdateMusicSourceVolume();

            // Wait while playing
            while (voiceSource != null && voiceSource.isPlaying)
            {
                yield return null;
            }

            // Restore music volume smoothly
            t = 0f;
            while (t < duckSpeed)
            {
                t += Time.unscaledDeltaTime;
                currentDuckFactor = Mathf.Lerp(0.3f, 1f, t / duckSpeed);
                UpdateMusicSourceVolume();
                yield return null;
            }
            currentDuckFactor = 1f;
            UpdateMusicSourceVolume();

            isVoiceActive = false;
            OnVoiceEnded?.Invoke();
        }

        // =========================================================
        // --- SFX ONE-SHOT PLAYBACK ---
        // =========================================================

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip, volumeScale * sfxVolume * masterVolume);
        }

        public void PlayProceduralClick()
        {
            if (sfxSource == null) return;

            // Debounce multiple triggers within 35ms (e.g. from simultaneous listeners)
            if (Time.unscaledTime - lastClickSoundTime < 0.035f) return;
            lastClickSoundTime = Time.unscaledTime;

            // Lightweight procedural UI click
            AudioClip click = CreateClickClip();
            sfxSource.PlayOneShot(click, 0.5f * sfxVolume * masterVolume);
        }

        // =========================================================
        // --- UNIVERSAL BUTTON CLICK REGISTRATION ---
        // =========================================================

        public void RegisterAllButtonsInScene()
        {
            if (globalButtonClickListener == null)
            {
                globalButtonClickListener = OnGlobalButtonClicked;
            }

            var buttons = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsInactive.Include);
            for (int i = 0; i < buttons.Length; i++)
            {
                var btn = buttons[i];
                if (btn == null) continue;
#if UNITY_EDITOR
                if (UnityEditor.EditorUtility.IsPersistent(btn)) continue;
#endif
                RegisterButton(btn);
            }
        }

        public void RegisterButton(UnityEngine.UI.Button btn)
        {
            if (btn == null || registeredButtons.Contains(btn)) return;
            registeredButtons.Add(btn);

            if (globalButtonClickListener == null)
            {
                globalButtonClickListener = OnGlobalButtonClicked;
            }

            btn.onClick.RemoveListener(globalButtonClickListener);
            btn.onClick.AddListener(globalButtonClickListener);
        }

        private void OnGlobalButtonClicked()
        {
            PlayProceduralClick();
        }

        private static AudioClip cachedClickClip;
        private static AudioClip CreateClickClip()
        {
            if (cachedClickClip != null) return cachedClickClip;
            int rate = 22050;
            float dur = 0.04f;
            int samples = (int)(rate * dur);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(1200f, 400f, t);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / rate)) * (1f - t) * 0.3f;
            }
            cachedClickClip = AudioClip.Create("UI_Click", samples, 1, rate, false);
            cachedClickClip.SetData(data, 0);
            return cachedClickClip;
        }
    }
}
