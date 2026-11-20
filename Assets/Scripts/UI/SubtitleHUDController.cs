using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Audio;

namespace JbelIghoud.UI
{
    [System.Serializable]
    public struct DialogueSubtitleCue
    {
        public float startTime;
        public float endTime;
        [TextArea(2, 4)]
        public string text;

        public DialogueSubtitleCue(float start, float end, string line)
        {
            startTime = start;
            endTime = end;
            text = line;
        }
    }

    /// <summary>
    /// Displays synchronized cinematic dialogue subtitles at the bottom of the screen.
    /// Fully respects the Subtitles toggle from Sound Settings in real time.
    /// </summary>
    public class SubtitleHUDController : MonoBehaviour
    {
        private static SubtitleHUDController _instance;
        public static SubtitleHUDController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<SubtitleHUDController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [Header("========================================")]
        [Header("--- UI REFERENCES ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup subtitleCG;
        [SerializeField] private RectTransform containerRT;
        [SerializeField] private Image backdropImage;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueContentText;

        [Header("========================================")]
        [Header("--- ANIMATION TIMINGS ---")]
        [Header("========================================")]
        [SerializeField] private float fadeDuration = 0.25f;

        private Coroutine playbackRoutine;
        private Coroutine fadeRoutine;
        private List<DialogueSubtitleCue> currentCues;
        private string currentSpeaker;
        private bool isDisplaying = false;

        private void Awake()
        {
            _instance = this;
            AutoWire();
        }

        private void Start()
        {
            AutoWire();
            HideImmediate();

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.OnSubtitlesToggled += OnSubtitlesToggled;
                GameAudioManager.Instance.OnVoiceEnded += OnVoiceEnded;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.OnSubtitlesToggled -= OnSubtitlesToggled;
                GameAudioManager.Instance.OnVoiceEnded -= OnVoiceEnded;
            }
        }

        public void AutoWire()
        {
            if (subtitleCG == null) subtitleCG = GetComponent<CanvasGroup>();
            if (containerRT == null) containerRT = GetComponent<RectTransform>();
            if (backdropImage == null)
            {
                var img = GetComponent<Image>();
                if (img != null) backdropImage = img;
            }
            if (speakerNameText == null)
            {
                var s = transform.Find("SpeakerText");
                if (s != null) speakerNameText = s.GetComponent<TextMeshProUGUI>();
            }
            if (dialogueContentText == null)
            {
                var d = transform.Find("DialogueText");
                if (d != null) dialogueContentText = d.GetComponent<TextMeshProUGUI>();
            }
        }

        private void OnSubtitlesToggled(bool enabled)
        {
            if (!enabled)
            {
                HideImmediate();
            }
            else if (isDisplaying && playbackRoutine != null)
            {
                // Unhide with smooth fade
                Fade(true);
            }
        }

        private void OnVoiceEnded()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
            isDisplaying = false;
            Fade(false);
        }

        /// <summary>
        /// Starts displaying a synchronized sequence of dialogue cues.
        /// </summary>
        public void StartDialogueSequence(string speaker, List<DialogueSubtitleCue> cues)
        {
            if (playbackRoutine != null) StopCoroutine(playbackRoutine);

            currentSpeaker = speaker;
            currentCues = cues;
            isDisplaying = true;

            if (speakerNameText != null)
            {
                speakerNameText.text = speaker;
                speakerNameText.gameObject.SetActive(!string.IsNullOrEmpty(speaker));
            }

            playbackRoutine = StartCoroutine(SyncDialogueRoutine());
        }

        private IEnumerator SyncDialogueRoutine()
        {
            if (currentCues == null || currentCues.Count == 0) yield break;

            // Wait 1 frame so AudioSource starts and buffer is active
            yield return null;

            int cueIndex = 0;
            string lastDisplayedText = "";

            while (GameAudioManager.Instance != null && GameAudioManager.Instance.IsVoicePlaying)
            {
                float time = GameAudioManager.Instance.VoiceCurrentTime;

                // Find active cue for current playback time
                string activeText = "";
                for (int i = 0; i < currentCues.Count; i++)
                {
                    if (time >= currentCues[i].startTime && time <= currentCues[i].endTime)
                    {
                        activeText = currentCues[i].text;
                        break;
                    }
                }

                // If subtitle enabled setting is on
                bool enabled = GameAudioManager.Instance.SubtitlesEnabled;

                if (enabled && !string.IsNullOrEmpty(activeText))
                {
                    if (activeText != lastDisplayedText)
                    {
                        lastDisplayedText = activeText;
                        if (dialogueContentText != null) dialogueContentText.text = activeText;
                    }

                    if (subtitleCG != null && subtitleCG.alpha < 0.99f && (fadeRoutine == null))
                    {
                        Fade(true);
                    }
                }
                else
                {
                    // In a pause or between lines or subtitles disabled
                    if (enabled && string.IsNullOrEmpty(activeText))
                    {
                        // Slight dim during pause
                        if (subtitleCG != null && subtitleCG.alpha > 0.01f && fadeRoutine == null)
                        {
                            Fade(false);
                        }
                    }
                    else if (!enabled && subtitleCG != null && subtitleCG.alpha > 0f)
                    {
                        subtitleCG.alpha = 0f;
                    }
                }

                yield return null;
            }

            // End of voice
            isDisplaying = false;
            Fade(false);
            playbackRoutine = null;
        }

        public void StopDialogueSequence()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
            isDisplaying = false;
            Fade(false);
        }

        private void Fade(bool visible)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            fadeRoutine = StartCoroutine(FadeRoutine(visible));
        }

        private IEnumerator FadeRoutine(bool visible)
        {
            if (subtitleCG == null) yield break;

            float startAlpha = subtitleCG.alpha;
            float targetAlpha = visible ? 1f : 0f;
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                subtitleCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
                yield return null;
            }

            subtitleCG.alpha = targetAlpha;
            fadeRoutine = null;
        }

        private void HideImmediate()
        {
            if (fadeRoutine != null) { StopCoroutine(fadeRoutine); fadeRoutine = null; }
            if (subtitleCG != null)
            {
                subtitleCG.alpha = 0f;
                subtitleCG.blocksRaycasts = false;
                subtitleCG.interactable = false;
            }
        }
    }
}
