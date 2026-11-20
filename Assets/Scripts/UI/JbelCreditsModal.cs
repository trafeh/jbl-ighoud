using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using JbelIghoud.Audio;

namespace JbelIghoud.UI
{
    /// <summary>
    /// End-of-game style credits roll (TEC GROUP).
    /// The credit lines rise from the bottom of the screen and fade out at the top, then loop.
    /// Hold your finger / mouse button anywhere to fast-forward.
    /// Closes with the X button or Escape / Android Back.
    /// </summary>
    public class JbelCreditsModal : MonoBehaviour
    {
        [Header("--- REFERENCES ---")]
        [SerializeField] private CanvasGroup modalCG;
        [SerializeField] private RectTransform viewportRT;
        [SerializeField] private RectTransform scrollerRT;
        [Tooltip("Every credit line (top-anchored children of the scroller). They fade near the screen edges.")]
        [SerializeField] private RectTransform[] lines;
        [SerializeField] private Button closeButton;

        [Header("--- SCROLL ---")]
        [Tooltip("Roll speed in canvas pixels per second.")]
        [SerializeField] private float scrollSpeed = 95f;
        [Tooltip("Speed multiplier while holding a finger / mouse button down.")]
        [SerializeField] private float fastForwardMultiplier = 3.5f;
        [Tooltip("Distance from the top / bottom edge over which lines fade in and out.")]
        [SerializeField] private float edgeFade = 170f;
        [Tooltip("Extra distance after the last line leaves the screen before the roll restarts.")]
        [SerializeField] private float endPadding = 60f;
        [Tooltip("Pause in seconds before the roll restarts from the bottom.")]
        [SerializeField] private float loopPause = 1.5f;
        [SerializeField] private float fadeDuration = 0.35f;

        private bool isOpen;
        private float scrollY;
        private float pauseTimer;
        private CanvasGroup[] lineGroups;
        private Coroutine fade;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (modalCG == null) modalCG = GetComponent<CanvasGroup>();
            if (closeButton != null) closeButton.onClick.AddListener(Hide);

            if (lines != null)
            {
                lineGroups = new CanvasGroup[lines.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] == null) continue;
                    var cg = lines[i].GetComponent<CanvasGroup>();
                    if (cg == null) cg = lines[i].gameObject.AddComponent<CanvasGroup>();
                    lineGroups[i] = cg;
                }
            }
            HideImmediate();
        }

        private void Update()
        {
            if (!isOpen) return;

            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Hide(); return; }

            float dt = Time.unscaledDeltaTime;
            float vh = viewportRT != null ? viewportRT.rect.height : 1080f;

            if (pauseTimer > 0f)
            {
                pauseTimer -= dt;
                if (pauseTimer <= 0f) scrollY = -vh;
            }
            else
            {
                bool held = Pointer.current != null && Pointer.current.press.isPressed;
                scrollY += scrollSpeed * (held ? fastForwardMultiplier : 1f) * dt;

                float total = scrollerRT != null ? scrollerRT.rect.height : 0f;
                if (scrollY > total + endPadding) pauseTimer = loopPause;
            }

            ApplyScroll(vh);
        }

        public void Show()
        {
            if (isOpen) return;
            isOpen = true;
            transform.SetAsLastSibling();

            modalCG.blocksRaycasts = true;
            modalCG.interactable = true;

            float vh = viewportRT != null ? viewportRT.rect.height : 1080f;
            scrollY = -vh;
            pauseTimer = 0f;
            ApplyScroll(vh);

            PlayClick();
            StartFade(1f);
        }

        public void Hide()
        {
            if (!isOpen) return;
            isOpen = false;
            modalCG.blocksRaycasts = false;
            modalCG.interactable = false;
            PlayClick();
            StartFade(0f);
        }

        public void HideImmediate()
        {
            isOpen = false;
            if (fade != null) { StopCoroutine(fade); fade = null; }
            if (modalCG != null)
            {
                modalCG.alpha = 0f;
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
            }
        }

        private void StartFade(float target)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(FadeRoutine(target));
        }

        private IEnumerator FadeRoutine(float target)
        {
            float from = modalCG.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                modalCG.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / fadeDuration));
                yield return null;
            }
            modalCG.alpha = target;
            fade = null;
        }

        private void ApplyScroll(float viewportHeight)
        {
            if (scrollerRT != null) scrollerRT.anchoredPosition = new Vector2(0f, scrollY);
            if (lines == null || lineGroups == null) return;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null || lineGroups[i] == null) continue;

                // Lines are top-anchored: anchoredPosition.y is minus the distance from the scroller's top.
                float topY = -lines[i].anchoredPosition.y;
                float centre = scrollY - (topY + lines[i].rect.height * 0.5f); // 0 = top edge, negative = down the screen
                float fromTop = -centre;
                float fromBottom = centre + viewportHeight;

                lineGroups[i].alpha = Mathf.Clamp01(Mathf.Min(fromTop, fromBottom) / Mathf.Max(1f, edgeFade));
            }
        }

        private void PlayClick()
        {
            if (GameAudioManager.Instance != null) GameAudioManager.Instance.PlayProceduralClick();
        }
    }
}
