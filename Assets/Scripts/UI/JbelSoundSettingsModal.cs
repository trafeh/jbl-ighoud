using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Audio;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Sound Settings Dialog Modal matching the authentic reference design:
    /// - Rich dark brown gradient backdrop (Assets/UI/settings/settings/bg.png)
    /// - Top-left pill badge "SOUND" (Assets/UI/settings/settings/sound bg.png)
    /// - Top-right circular close button with 'X'
    /// - Three precise sliders (SOUND EFFECT, MUSIC, VOICE) with custom groove, fill & knob
    /// - SUBTITLES label with circular radio button toggle with 'X'
    /// - Real-time synchronization with GameAudioManager and PlayerPrefs persistence
    /// </summary>
    public class JbelSoundSettingsModal : MonoBehaviour
    {
        private static JbelSoundSettingsModal _instance;
        public static JbelSoundSettingsModal Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<JbelSoundSettingsModal>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [Header("========================================")]
        [Header("--- MODAL CONTAINERS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup modalCG;
        [SerializeField] private RectTransform dialogCardRT;
        [SerializeField] private Button closeButton;

        [Header("========================================")]
        [Header("--- SLIDERS ---")]
        [Header("========================================")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider voiceSlider;

        [Header("========================================")]
        [Header("--- SUBTITLES TOGGLE ---")]
        [Header("========================================")]
        [SerializeField] private Button subtitleToggleButton;
        [SerializeField] private GameObject subtitleCheckmarkX;
        [SerializeField] private GameObject subtitleRadioDot;

        [Header("========================================")]
        [Header("--- ANIMATION ---")]
        [Header("========================================")]
        [SerializeField] private float animDuration = 0.2f;

        private Coroutine animRoutine;
        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private System.Action onCloseCallback;

        private void Awake()
        {
            _instance = this;
            AutoWire();
            WireListeners();
        }

        private void Start()
        {
            AutoWire();
            WireListeners();
            SyncUIFromAudioSettings();

            // Hidden by default
            if (!isOpen)
            {
                HideImmediate();
            }
        }

        public void AutoWire()
        {
            if (modalCG == null) modalCG = GetComponent<CanvasGroup>();
            if (dialogCardRT == null)
            {
                var card = transform.Find("DialogCard");
                dialogCardRT = card != null ? card.GetComponent<RectTransform>() : GetComponent<RectTransform>();
            }

            if (closeButton == null && dialogCardRT != null)
            {
                var cb = dialogCardRT.Find("CloseButton");
                if (cb != null) closeButton = cb.GetComponent<Button>();
            }

            if (sfxSlider == null && dialogCardRT != null)
            {
                var s = dialogCardRT.Find("Row_SFX/Slider_SFX");
                if (s != null) sfxSlider = s.GetComponent<Slider>();
            }

            if (musicSlider == null && dialogCardRT != null)
            {
                var s = dialogCardRT.Find("Row_Music/Slider_Music");
                if (s != null) musicSlider = s.GetComponent<Slider>();
            }

            if (voiceSlider == null && dialogCardRT != null)
            {
                var s = dialogCardRT.Find("Row_Voice/Slider_Voice");
                if (s != null) voiceSlider = s.GetComponent<Slider>();
            }

            if (subtitleToggleButton == null && dialogCardRT != null)
            {
                var btn = dialogCardRT.Find("Row_Voice/SubtitlesGroup/SubtitleRadioBtn");
                if (btn != null) subtitleToggleButton = btn.GetComponent<Button>();
            }

            if (subtitleToggleButton != null)
            {
                if (subtitleRadioDot == null)
                {
                    var dot = subtitleToggleButton.transform.Find("RadioDot");
                    if (dot != null) subtitleRadioDot = dot.gameObject;
                }

                if (subtitleCheckmarkX == null)
                {
                    var x = subtitleToggleButton.transform.Find("CheckX");
                    if (x != null) subtitleCheckmarkX = x.gameObject;
                }
            }
        }

        private void WireListeners()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseSoundModal);
                closeButton.onClick.AddListener(CloseSoundModal);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSFXSliderChanged);
                sfxSlider.onValueChanged.AddListener(OnSFXSliderChanged);
            }

            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveListener(OnMusicSliderChanged);
                musicSlider.onValueChanged.AddListener(OnMusicSliderChanged);
            }

            if (voiceSlider != null)
            {
                voiceSlider.onValueChanged.RemoveListener(OnVoiceSliderChanged);
                voiceSlider.onValueChanged.AddListener(OnVoiceSliderChanged);
            }

            if (subtitleToggleButton != null)
            {
                subtitleToggleButton.onClick.RemoveListener(OnSubtitleToggleClicked);
                subtitleToggleButton.onClick.AddListener(OnSubtitleToggleClicked);
            }
        }

        public void SyncUIFromAudioSettings()
        {
            if (GameAudioManager.Instance == null) return;

            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(GameAudioManager.Instance.SFXVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(GameAudioManager.Instance.MusicVolume);
            if (voiceSlider != null) voiceSlider.SetValueWithoutNotify(GameAudioManager.Instance.VoiceVolume);

            UpdateSubtitleVisual(GameAudioManager.Instance.SubtitlesEnabled);
        }

        private void OnSFXSliderChanged(float val)
        {
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.SetSFXVolume(val);
            }
        }

        private void OnMusicSliderChanged(float val)
        {
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.SetMusicVolume(val);
            }
        }

        private void OnVoiceSliderChanged(float val)
        {
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.SetVoiceVolume(val);
            }
        }

        private void OnSubtitleToggleClicked()
        {
            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.ToggleSubtitles();
                UpdateSubtitleVisual(GameAudioManager.Instance.SubtitlesEnabled);
                GameAudioManager.Instance.PlayProceduralClick();
            }
        }

        private void UpdateSubtitleVisual(bool enabled)
        {
            if (subtitleCheckmarkX != null)
            {
                subtitleCheckmarkX.SetActive(false);
            }
            if (subtitleRadioDot != null)
            {
                subtitleRadioDot.SetActive(enabled);
            }
            if (subtitleToggleButton != null)
            {
                var img = subtitleToggleButton.GetComponent<Image>();
                if (img != null)
                {
                    img.color = Color.white;
                }
            }
        }

        public void OpenSoundModal(System.Action onClose = null)
        {
            isOpen = true;
            onCloseCallback = onClose;

            // Activate GameObject FIRST so coroutines and rendering are active
            gameObject.SetActive(true);
            if (modalCG != null)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
            }

            AutoWire();
            WireListeners();
            SyncUIFromAudioSettings();

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(true));
        }

        public void CloseSoundModal()
        {
            if (!isOpen) return;
            isOpen = false;

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayProceduralClick();
            }

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(false));
        }

        private IEnumerator AnimateModal(bool opening)
        {
            if (modalCG == null) yield break;

            if (opening)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
            }

            float t = 0f;
            float startAlpha = opening ? 0f : modalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = dialogCardRT != null ? (opening ? Vector3.one * 0.94f : dialogCardRT.localScale) : Vector3.one;
            Vector3 targetScale = opening ? Vector3.one : Vector3.one * 0.94f;

            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / animDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                modalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);
                if (dialogCardRT != null)
                {
                    dialogCardRT.localScale = Vector3.Lerp(startScale, targetScale, smoothP);
                }
                yield return null;
            }

            modalCG.alpha = targetAlpha;

            if (!opening)
            {
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
                gameObject.SetActive(false);
                onCloseCallback?.Invoke();
                onCloseCallback = null;
            }

            animRoutine = null;
        }

        private void LateUpdate()
        {
            if (!isOpen) return;
            SquareHandle(sfxSlider);
            SquareHandle(musicSlider);
            SquareHandle(voiceSlider);
        }

        // The card scales with the screen, so keep each knob a circle as tall as its slider area.
        private static void SquareHandle(Slider s)
        {
            if (s == null || s.handleRect == null) return;
            var area = s.handleRect.parent as RectTransform;
            if (area == null) return;
            float h = area.rect.height;
            Vector2 sd = s.handleRect.sizeDelta;
            if (!Mathf.Approximately(sd.x, h) || sd.y != 0f)
            {
                s.handleRect.sizeDelta = new Vector2(h, 0f);
            }
        }

        public void HideImmediate()
        {
            if (animRoutine != null) { StopCoroutine(animRoutine); animRoutine = null; }
            if (modalCG != null)
            {
                modalCG.alpha = 0f;
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
            }
            if (dialogCardRT != null)
            {
                dialogCardRT.localScale = Vector3.one * 0.94f;
            }
            gameObject.SetActive(false);
        }
    }
}
