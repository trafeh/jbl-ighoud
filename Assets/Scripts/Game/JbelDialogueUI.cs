using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using JbelIghoud.Audio;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Cinematic UI layer: black fade, title card, letterbox bars, typewriter dialogue box
    /// (tap / Space / Enter to continue), the "what is your name?" panel and the SKIP button.
    /// </summary>
    public class JbelDialogueUI : MonoBehaviour
    {
        public static JbelDialogueUI Instance { get; private set; }

        [Header("--- FADE & TITLE ---")]
        [SerializeField] private CanvasGroup blackFade;
        [SerializeField] private CanvasGroup titleCard;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;

        [Header("--- LETTERBOX ---")]
        [SerializeField] private RectTransform letterboxTop;
        [SerializeField] private RectTransform letterboxBottom;
        [SerializeField] private float letterboxHeight = 120f;

        [Header("--- DIALOGUE BOX ---")]
        [SerializeField] private CanvasGroup dialogueBox;
        [SerializeField] private RectTransform dialogueBoxRT;
        [SerializeField] private TextMeshProUGUI speakerText;
        [SerializeField] private TextMeshProUGUI bodyText;
        [SerializeField] private RectTransform nextArrow;
        [SerializeField] private Button tapCatcher;
        [SerializeField] private float charsPerSecond = 40f;

        [Header("--- NAME ENTRY ---")]
        [SerializeField] private CanvasGroup namePanel;
        [SerializeField] private RectTransform namePanelCard;
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI nameHint;

        [Header("--- SKIP ---")]
        [SerializeField] private CanvasGroup skipGroup;
        [SerializeField] private Button skipButton;

        public bool IsOpen => boxOpen || nameOpen;
        public event Action OnSkipPressed;

        private bool boxOpen, nameOpen, tapped, nameSubmitted;
        private Coroutine boxFade, skipFade;
        private Vector2 boxHome;

        private void Awake()
        {
            Instance = this;
            if (dialogueBoxRT != null) boxHome = dialogueBoxRT.anchoredPosition;
            if (tapCatcher != null) tapCatcher.onClick.AddListener(() => tapped = true);
            if (skipButton != null) skipButton.onClick.AddListener(() => { if (skipGroup != null && skipGroup.interactable) OnSkipPressed?.Invoke(); });
            if (confirmButton != null) confirmButton.onClick.AddListener(SubmitName);
            if (nameInput != null)
            {
                nameInput.characterLimit = JbelPlayerProfile.MaxNameLength;
                nameInput.onSubmit.AddListener(_ => SubmitName());
                nameInput.onValueChanged.AddListener(_ => ValidateLive());
            }

            SetCG(blackFade, 0f, false);
            SetCG(titleCard, 0f, false);
            SetCG(dialogueBox, 0f, false);
            SetCG(namePanel, 0f, false);
            SetCG(skipGroup, 0f, false);
            SetTapCatcher(false);
            SetLetterbox01(0f);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            if (boxOpen)
            {
                var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) tapped = true;
                if (nextArrow != null) nextArrow.anchoredPosition = new Vector2(nextArrow.anchoredPosition.x, -6f + Mathf.Sin(Time.unscaledTime * 6f) * 5f);
            }
        }

        // ------------------------------------------------------------ fade / title / letterbox

        public void SetBlack(float a) { SetCG(blackFade, a, a > 0.5f); }

        public IEnumerator Fade(float target, float duration)
        {
            if (blackFade == null) yield break;
            blackFade.blocksRaycasts = true;
            float from = blackFade.alpha, t = 0f;
            while (t < duration) { t += Time.unscaledDeltaTime; blackFade.alpha = Mathf.Lerp(from, target, Smooth(t / duration)); yield return null; }
            blackFade.alpha = target;
            blackFade.blocksRaycasts = target > 0.5f;
        }

        public IEnumerator ShowTitle(string title, string subtitle, float hold)
        {
            if (titleCard == null) yield break;
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            var rt = (RectTransform)titleCard.transform;
            float t = 0f;
            while (t < 1.1f)
            {
                t += Time.unscaledDeltaTime;
                float p = Smooth(t / 1.1f);
                titleCard.alpha = p;
                rt.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, p);
                if (titleText != null) titleText.characterSpacing = Mathf.Lerp(28f, 14f, p);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(hold);
            t = 0f;
            while (t < 0.8f) { t += Time.unscaledDeltaTime; titleCard.alpha = 1f - Smooth(t / 0.8f); yield return null; }
            titleCard.alpha = 0f;
        }

        public IEnumerator Letterbox(bool on, float duration)
        {
            float from = letterboxTop != null ? letterboxTop.sizeDelta.y / Mathf.Max(1f, letterboxHeight) : 0f;
            float to = on ? 1f : 0f, t = 0f;
            while (t < duration) { t += Time.unscaledDeltaTime; SetLetterbox01(Mathf.Lerp(from, to, Smooth(t / duration))); yield return null; }
            SetLetterbox01(to);
        }

        private void SetLetterbox01(float k)
        {
            if (letterboxTop != null) letterboxTop.sizeDelta = new Vector2(0f, letterboxHeight * k);
            if (letterboxBottom != null) letterboxBottom.sizeDelta = new Vector2(0f, letterboxHeight * k);
        }

        // ------------------------------------------------------------ dialogue

        /// <summary>Types a line; first tap finishes the line, second tap continues.</summary>
        public IEnumerator Say(string speaker, string text)
        {
            OpenBox();
            if (speakerText != null) speakerText.text = speaker;
            if (bodyText != null) { bodyText.text = text; bodyText.maxVisibleCharacters = 0; bodyText.ForceMeshUpdate(); }
            if (nextArrow != null) nextArrow.gameObject.SetActive(false);

            int total = bodyText != null ? bodyText.textInfo.characterCount : 0;
            float shown = 0f;
            tapped = false;
            yield return null;

            while (bodyText != null && shown < total)
            {
                if (tapped) { tapped = false; break; }
                char c = (int)shown < text.Length ? text[Mathf.Clamp((int)shown, 0, text.Length - 1)] : ' ';
                float speed = (c == '.' || c == ',' || c == '?' || c == '!') ? charsPerSecond * 0.35f : charsPerSecond;
                shown += speed * Time.unscaledDeltaTime;
                bodyText.maxVisibleCharacters = Mathf.Min(total, (int)shown);
                yield return null;
            }
            if (bodyText != null) bodyText.maxVisibleCharacters = 99999;
            if (nextArrow != null) nextArrow.gameObject.SetActive(true);

            tapped = false;
            while (!tapped) yield return null;
            tapped = false;
            PlayClick();
        }

        private void OpenBox()
        {
            if (boxOpen) return;
            boxOpen = true;
            SetTapCatcher(true);
            if (boxFade != null) StopCoroutine(boxFade);
            boxFade = StartCoroutine(BoxRoutine(true));
        }

        public void HideBox()
        {
            if (!boxOpen) return;
            boxOpen = false;
            SetTapCatcher(false);
            if (boxFade != null) StopCoroutine(boxFade);
            boxFade = StartCoroutine(BoxRoutine(false));
        }

        private IEnumerator BoxRoutine(bool show)
        {
            if (dialogueBox == null) yield break;
            float from = dialogueBox.alpha, to = show ? 1f : 0f, t = 0f, d = 0.25f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float p = Smooth(t / d);
                dialogueBox.alpha = Mathf.Lerp(from, to, p);
                if (dialogueBoxRT != null) dialogueBoxRT.anchoredPosition = boxHome + new Vector2(0f, Mathf.Lerp(-30f, 0f, dialogueBox.alpha));
                yield return null;
            }
            dialogueBox.alpha = to;
            if (dialogueBoxRT != null) dialogueBoxRT.anchoredPosition = boxHome;
            boxFade = null;
        }

        private void SetTapCatcher(bool on)
        {
            if (tapCatcher == null) return;
            tapCatcher.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------ name entry

        public IEnumerator AskName(Action<string> onDone)
        {
            nameOpen = true;
            nameSubmitted = false;
            if (nameInput != null) { nameInput.text = ""; }
            if (nameHint != null) { nameHint.text = "2 - 14 LETTERS"; nameHint.color = new Color(0.91f, 0.87f, 0.77f, 0.6f); }
            if (confirmButton != null) confirmButton.interactable = false;

            SetCG(namePanel, 0f, true);
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.unscaledDeltaTime;
                float p = Smooth(t / 0.35f);
                namePanel.alpha = p;
                if (namePanelCard != null) namePanelCard.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, p);
                yield return null;
            }
            if (nameInput != null) { nameInput.Select(); nameInput.ActivateInputField(); }

            while (!nameSubmitted) yield return null;

            string final = JbelPlayerProfile.SanitizeName(nameInput != null ? nameInput.text : "");
            PlayClick();
            if (namePanelCard != null) namePanelCard.localScale = Vector3.one * 1.04f;
            t = 0f;
            while (t < 0.3f)
            {
                t += Time.unscaledDeltaTime;
                float p = Smooth(t / 0.3f);
                namePanel.alpha = 1f - p;
                if (namePanelCard != null) namePanelCard.localScale = Vector3.one * Mathf.Lerp(1.04f, 0.95f, p);
                yield return null;
            }
            SetCG(namePanel, 0f, false);
            nameOpen = false;
            onDone?.Invoke(final);
        }

        private bool IsNameValid(out string reason)
        {
            string raw = nameInput != null ? nameInput.text.Trim() : "";
            int letters = 0;
            foreach (char c in raw) if (char.IsLetterOrDigit(c)) letters++;
            if (letters < 2) { reason = "AT LEAST 2 LETTERS"; return false; }
            reason = "";
            return true;
        }

        private void ValidateLive()
        {
            bool ok = IsNameValid(out string reason);
            if (confirmButton != null) confirmButton.interactable = ok;
            if (nameHint != null)
            {
                nameHint.text = ok ? "TAP CONFIRM" : (nameInput.text.Length == 0 ? "2 - 14 LETTERS" : reason);
                nameHint.color = ok ? new Color(0.96f, 0.78f, 0.36f, 0.9f) : new Color(0.91f, 0.87f, 0.77f, 0.6f);
            }
        }

        private void SubmitName()
        {
            if (!nameOpen) return;
            if (!IsNameValid(out string reason))
            {
                if (nameHint != null) { nameHint.text = reason; nameHint.color = new Color(1f, 0.55f, 0.4f, 1f); }
                if (namePanelCard != null) StartCoroutine(Shake(namePanelCard));
                if (nameInput != null) nameInput.ActivateInputField();
                return;
            }
            nameSubmitted = true;
        }

        private IEnumerator Shake(RectTransform rt)
        {
            Vector2 home = rt.anchoredPosition;
            float t = 0f;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * 70f) * 12f * (1f - t / 0.3f), 0f); yield return null; }
            rt.anchoredPosition = home;
        }

        // ------------------------------------------------------------ skip

        public void SetSkipVisible(bool on)
        {
            if (skipGroup == null) return;
            if (skipFade != null) StopCoroutine(skipFade);
            skipFade = StartCoroutine(SkipRoutine(on));
        }

        private IEnumerator SkipRoutine(bool on)
        {
            skipGroup.blocksRaycasts = on;
            skipGroup.interactable = on;
            float from = skipGroup.alpha, to = on ? 1f : 0f, t = 0f;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; skipGroup.alpha = Mathf.Lerp(from, to, t / 0.3f); yield return null; }
            skipGroup.alpha = to;
        }

        // ------------------------------------------------------------ helpers

        private static void SetCG(CanvasGroup cg, float a, bool raycasts)
        {
            if (cg == null) return;
            cg.alpha = a;
            cg.blocksRaycasts = raycasts;
            cg.interactable = raycasts;
        }

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static void PlayClick()
        {
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayProceduralClick();
        }
    }
}
