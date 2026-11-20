using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using JbelIghoud.Audio;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Title / loading screen theme. Fades in, loops, follows the MUSIC volume from Settings,
    /// and fades out gently after the game scene takes over (survives the scene switch to do so).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class JbelMenuMusic : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
        [SerializeField] private float fadeInTime = 2.5f;
        [SerializeField] private float fadeOutTime = 2.5f;

        private AudioSource src;
        private float fade;
        private bool leaving;
        private string homeScene;

        private void Awake()
        {
            // claim the music before anything (e.g. GameAudioManager.Start) can start the in-game theme
            GameAudioManager.SuppressMusicCycle = true;
            homeScene = gameObject.scene.name;
            src = GetComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            DontDestroyOnLoad(gameObject);
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        private void Start()
        {
            // the in-game theme cycle must not play over the menu song
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.StopMusicCycle();
            if (src.clip != null) src.Play();
        }

        private void OnDestroy() { SceneManager.activeSceneChanged -= OnSceneChanged; }

        private void OnSceneChanged(Scene from, Scene to)
        {
            // ignore the initial activation of the menu scene itself; only leave for another scene
            if (to.name == homeScene) return;
            if (!leaving) StartCoroutine(FadeOutAndDie());
        }

        private void Update()
        {
            if (!leaving) fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInTime));
            float music = GameAudioManager.Instance != null ? GameAudioManager.Instance.MusicVolume : 1f;
            src.volume = volume * music * fade;
        }

        private IEnumerator FadeOutAndDie()
        {
            leaving = true;
            float start = fade, t = 0f;
            while (t < fadeOutTime)
            {
                t += Time.unscaledDeltaTime;
                fade = Mathf.Lerp(start, 0f, t / fadeOutTime);
                yield return null;
            }
            // hand music back to the game's theme cycle
            GameAudioManager.SuppressMusicCycle = false;
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.StartMusicCycle();
            Destroy(gameObject);
        }
    }
}
