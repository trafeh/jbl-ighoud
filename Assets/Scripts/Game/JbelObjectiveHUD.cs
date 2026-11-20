using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JbelIghoud.Game
{
    /// <summary>
    /// Survivor card (name, avatar initial, NIVEAU, XP bar) + the objective banner + on-screen waypoint.
    /// Banner: slides in with a gold flash + shine sweep on a new objective, shows live progress,
    /// stamps a check on completion, flies "+XP" into the XP bar and celebrates level-ups.
    /// </summary>
    public class JbelObjectiveHUD : MonoBehaviour
    {
        [Header("--- SURVIVOR CARD ---")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI avatarInitial;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image xpFill;
        [SerializeField] private RectTransform xpBarRT;
        [SerializeField] private RectTransform levelRT;
        [SerializeField] private string levelFormat = "NIVEAU {0}";

        [Header("--- OBJECTIVE BANNER ---")]
        [SerializeField] private RectTransform bannerRT;
        [SerializeField] private CanvasGroup bannerCG;
        [SerializeField] private Image glow;
        [SerializeField] private RectTransform iconRT;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressFill;
        [SerializeField] private RectTransform shineRT;
        [SerializeField] private Image checkImage;

        [Header("--- POPUPS ---")]
        [SerializeField] private TextMeshProUGUI xpPopupTemplate;
        [SerializeField] private TextMeshProUGUI levelUpText;

        [Header("--- WAYPOINT ---")]
        [SerializeField] private RectTransform waypointRT;
        [SerializeField] private CanvasGroup waypointCG;
        [SerializeField] private RectTransform waypointArrowRT;
        [SerializeField] private RectTransform waypointMarkerRT;
        [SerializeField] private TextMeshProUGUI waypointDistance;
        [SerializeField] private float hideWaypointWithin = 5f;

        private static readonly Color Gold = new Color(0.96f, 0.78f, 0.36f, 1f);
        private static readonly Color Cream = new Color(0.98f, 0.95f, 0.88f, 1f);
        private static readonly Color Green = new Color(0.62f, 0.9f, 0.48f, 1f);

        private JbelObjectiveManager mgr;
        private readonly Queue<IEnumerator> queue = new Queue<IEnumerator>();
        private bool running;
        private Vector2 bannerHome;
        private float shownXP01;
        private bool bannerVisible;
        private float idleTime;
        private RectTransform canvasRT;
        private Transform playerT;

        private void Awake()
        {
            if (bannerRT != null) bannerHome = bannerRT.anchoredPosition;
            canvasRT = GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
            if (bannerCG != null) bannerCG.alpha = 0f;
            if (checkImage != null) checkImage.gameObject.SetActive(false);
            if (shineRT != null) shineRT.gameObject.SetActive(false);
            if (xpPopupTemplate != null) xpPopupTemplate.gameObject.SetActive(false);
            if (levelUpText != null) levelUpText.alpha = 0f;
            if (waypointCG != null) waypointCG.alpha = 0f;
            if (glow != null) SetAlpha(glow, 0f);
        }

        private void Start()
        {
            mgr = JbelObjectiveManager.Instance != null ? JbelObjectiveManager.Instance : FindAnyObjectByType<JbelObjectiveManager>();
            if (mgr != null)
            {
                mgr.OnObjectiveStarted += (o, fresh) => Enqueue(ShowObjective(o.title, fresh));
                mgr.OnProgress += OnProgress;
                mgr.OnObjectiveCompleted += (o, xp, lv) => Enqueue(CompleteObjective(xp, lv));
                mgr.OnAllCompleted += () => Enqueue(ShowObjective(mgr.AllDoneTitle, false, true));
            }
            RefreshProfile(true);
        }

        /// <summary>Re-reads name / level / XP from the saved profile.</summary>
        public void RefreshProfile(bool snapXP)
        {
            string n = JbelPlayerProfile.PlayerName;
            if (nameText != null) nameText.text = n;
            if (avatarInitial != null) avatarInitial.text = n.Length > 0 ? n.Substring(0, 1) : "?";
            if (levelText != null && !running) levelText.text = string.Format(levelFormat, JbelPlayerProfile.Level);
            if (snapXP) { shownXP01 = JbelPlayerProfile.XPProgress01; if (xpFill != null) xpFill.fillAmount = shownXP01; }
        }

        private void LateUpdate()
        {
            if (nameText != null && nameText.text != JbelPlayerProfile.PlayerName) RefreshProfile(false);
            if (xpFill != null && !running) xpFill.fillAmount = shownXP01;

            if (!running && bannerVisible && glow != null)
            {
                idleTime += Time.unscaledDeltaTime;
                float pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(idleTime * 1.6f)), 6f);
                SetAlpha(glow, 0.12f + 0.38f * pulse);
                if (iconRT != null) iconRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(idleTime * 0.7f) * 6f);
            }

            UpdateWaypoint();
        }

        // ================================================================== QUEUE

        private void Enqueue(IEnumerator routine)
        {
            queue.Enqueue(routine);
            if (!running) StartCoroutine(RunQueue());
        }

        private IEnumerator RunQueue()
        {
            running = true;
            while (queue.Count > 0) yield return queue.Dequeue();
            running = false;
            idleTime = 0f;
        }

        private void OnProgress(JbelObjectiveManager.Objective o, int p, int goal, string label)
        {
            bool collect = o.type == JbelObjectiveManager.ObjectiveType.CollectStones;
            if (progressText != null && progressText.text != label)
            {
                bool countUp = collect && !string.IsNullOrEmpty(progressText.text);
                progressText.text = label;
                if (countUp) StartCoroutine(Punch(progressText.rectTransform, 1.35f, 0.3f));
            }
            if (progressFill != null)
            {
                progressFill.transform.parent.gameObject.SetActive(collect);
                float target = collect ? p / (float)Mathf.Max(1, goal) : 0f;
                if (Mathf.Abs(progressFill.fillAmount - target) > 0.001f) StartCoroutine(FillTo(progressFill, target, 0.35f));
            }
        }

        // ================================================================== BANNER ANIMS

        private IEnumerator ShowObjective(string title, bool fresh, bool finale = false)
        {
            if (bannerRT == null) yield break;
            while (JbelIntroCutscene.IsPlaying) yield return null;

            if (checkImage != null) checkImage.gameObject.SetActive(false);
            if (titleText != null) { titleText.text = title; titleText.color = Cream; }
            if (headerText != null) { headerText.text = finale ? "CHAPTER COMPLETE" : (fresh ? "NEW OBJECTIVE" : "OBJECTIVE"); headerText.color = Gold; }
            if (progressText != null) progressText.gameObject.SetActive(!finale);
            if (progressFill != null && finale) progressFill.transform.parent.gameObject.SetActive(false);
            if (progressFill != null && fresh) progressFill.fillAmount = 0f;

            float t = 0f, d = 0.55f;
            Vector2 from = bannerHome + new Vector2(-560f, 0f);
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float p = EaseOutBack(Mathf.Clamp01(t / d));
                bannerRT.anchoredPosition = Vector2.LerpUnclamped(from, bannerHome, p);
                bannerCG.alpha = Mathf.Clamp01(t / (d * 0.5f));
                if (iconRT != null) iconRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-200f, 0f, Mathf.Clamp01(t / d)));
                yield return null;
            }
            bannerRT.anchoredPosition = bannerHome;
            bannerCG.alpha = 1f;
            bannerVisible = true;

            if (fresh || finale)
            {
                StartCoroutine(Shine());
                yield return GlowFlash(2, 0.9f);
                yield return new WaitForSecondsRealtime(1.6f);
                if (headerText != null && !finale) headerText.text = "OBJECTIVE";
            }
        }

        private IEnumerator CompleteObjective(int xp, int levelsGained)
        {
            if (bannerRT == null) yield break;
            if (headerText != null) { headerText.text = "COMPLETE"; headerText.color = Green; }
            if (titleText != null) titleText.color = Green;
            if (progressFill != null && progressFill.transform.parent.gameObject.activeSelf) StartCoroutine(FillTo(progressFill, 1f, 0.2f));

            if (checkImage != null)
            {
                checkImage.gameObject.SetActive(true);
                var crt = checkImage.rectTransform;
                float t = 0f;
                while (t < 0.28f)
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / 0.28f);
                    crt.localScale = Vector3.one * Mathf.Lerp(2.4f, 1f, p * p);
                    SetAlpha(checkImage, p);
                    yield return null;
                }
                StartCoroutine(Punch(bannerRT, 1.06f, 0.25f));
            }
            StartCoroutine(GlowFlash(1, 0.6f));

            yield return FlyXP(xp);

            float target = JbelPlayerProfile.XPProgress01;
            int finalLevel = JbelPlayerProfile.Level;
            if (levelsGained > 0)
            {
                yield return FillXP(1f, 0.6f);
                for (int i = levelsGained - 1; i >= 0; i--)
                {
                    yield return LevelUp(finalLevel - i);
                    shownXP01 = 0f; if (xpFill != null) xpFill.fillAmount = 0f;
                    if (i > 0) yield return FillXP(1f, 0.4f);
                }
            }
            yield return FillXP(target, 0.7f);
            if (levelText != null) levelText.text = string.Format(levelFormat, finalLevel);

            yield return new WaitForSecondsRealtime(0.9f);

            float tt = 0f, d = 0.4f;
            Vector2 to = bannerHome + new Vector2(-560f, 0f);
            while (tt < d)
            {
                tt += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(tt / d); p *= p;
                bannerRT.anchoredPosition = Vector2.Lerp(bannerHome, to, p);
                bannerCG.alpha = 1f - p;
                yield return null;
            }
            bannerVisible = false;
            if (glow != null) SetAlpha(glow, 0f);
        }

        private IEnumerator FlyXP(int xp)
        {
            if (xpPopupTemplate == null || xpBarRT == null) yield break;
            var pop = Instantiate(xpPopupTemplate, xpPopupTemplate.transform.parent);
            pop.gameObject.SetActive(true);
            pop.text = $"+{xp} XP";
            var rt = pop.rectTransform;
            Vector3 start = bannerRT.TransformPoint(new Vector3(bannerRT.rect.width * 0.62f, 0f, 0f));
            Vector3 end = xpBarRT.TransformPoint(xpBarRT.rect.center);
            rt.position = start;

            float t = 0f;
            while (t < 0.35f) { t += Time.unscaledDeltaTime; float p = Mathf.Clamp01(t / 0.35f); rt.localScale = Vector3.one * EaseOutBack(p); pop.alpha = p; yield return null; }
            yield return new WaitForSecondsRealtime(0.35f);
            t = 0f;
            Vector3 ctrl = (start + end) * 0.5f + Vector3.right * 160f * canvasRT.lossyScale.x;
            while (t < 0.55f)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / 0.55f), e = p * p;
                rt.position = Vector3.Lerp(Vector3.Lerp(start, ctrl, e), Vector3.Lerp(ctrl, end, e), e);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, e);
                pop.alpha = 1f - Mathf.Max(0f, (p - 0.75f) / 0.25f);
                yield return null;
            }
            Destroy(pop.gameObject);
            StartCoroutine(Punch(xpBarRT, 1.12f, 0.25f));
        }

        private IEnumerator FillXP(float to, float d)
        {
            float from = shownXP01, t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                shownXP01 = Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - Mathf.Clamp01(t / d), 3f));
                if (xpFill != null) xpFill.fillAmount = shownXP01;
                yield return null;
            }
            shownXP01 = to;
            if (xpFill != null) xpFill.fillAmount = to;
        }

        private IEnumerator LevelUp(int newLevel)
        {
            if (levelText != null) levelText.text = string.Format(levelFormat, newLevel);
            if (levelRT != null) StartCoroutine(Punch(levelRT, 1.5f, 0.45f));
            if (levelText != null) StartCoroutine(FlashColor(levelText, Gold, 1.2f));
            if (levelUpText == null) yield break;

            levelUpText.text = $"LEVEL UP!  <size=70%>{string.Format(levelFormat, newLevel)}</size>";
            var rt = levelUpText.rectTransform;
            Vector2 home = rt.anchoredPosition;
            float t = 0f;
            while (t < 1.5f)
            {
                t += Time.unscaledDeltaTime;
                float p = t / 1.5f;
                levelUpText.alpha = p < 0.15f ? p / 0.15f : (p > 0.7f ? 1f - (p - 0.7f) / 0.3f : 1f);
                rt.localScale = Vector3.one * (p < 0.15f ? EaseOutBack(p / 0.15f) : 1f);
                rt.anchoredPosition = home + new Vector2(0f, p * 26f);
                levelUpText.characterSpacing = Mathf.Lerp(4f, 12f, p);
                yield return null;
            }
            levelUpText.alpha = 0f;
            rt.anchoredPosition = home;
        }

        private IEnumerator Shine()
        {
            if (shineRT == null || bannerRT == null) yield break;
            shineRT.gameObject.SetActive(true);
            float w = bannerRT.rect.width, t = 0f;
            while (t < 0.7f)
            {
                t += Time.unscaledDeltaTime;
                shineRT.anchoredPosition = new Vector2(Mathf.Lerp(-120f, w + 120f, t / 0.7f), 0f);
                yield return null;
            }
            shineRT.gameObject.SetActive(false);
        }

        private IEnumerator GlowFlash(int times, float duration)
        {
            if (glow == null) yield break;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(glow, Mathf.Abs(Mathf.Sin(t / duration * Mathf.PI * times)));
                yield return null;
            }
            SetAlpha(glow, 0.15f);
        }

        // ================================================================== WAYPOINT

        private void UpdateWaypoint()
        {
            if (waypointRT == null || waypointCG == null) return;
            Camera cam = Camera.main;
            bool show = false;
            Vector3 world = Vector3.zero;

            if (cam != null && mgr != null && !JbelIntroCutscene.IsPlaying &&
                (JbelDialogueUI.Instance == null || !JbelDialogueUI.Instance.IsOpen) && mgr.TryGetMarkerPosition(out world))
            {
                var pt = FindPlayer();
                float dist = pt != null ? Vector3.Distance(Flat(pt.position), Flat(world)) : 999f;
                show = dist > hideWaypointWithin || mgr.Current.type == JbelObjectiveManager.ObjectiveType.CollectStones;
                if (waypointDistance != null) waypointDistance.text = $"{Mathf.RoundToInt(dist)} M";
            }

            waypointCG.alpha = Mathf.MoveTowards(waypointCG.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            if (cam == null || (!show && waypointCG.alpha <= 0f)) return;
            if (!show) return;

            Vector3 sp = cam.WorldToScreenPoint(world);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

            float margin = 70f * canvasRT.lossyScale.x;
            Rect safe = new Rect(margin, margin * 1.6f, Screen.width - margin * 2f, Screen.height - margin * 3.2f);
            bool onScreen = !behind && safe.Contains(new Vector2(sp.x, sp.y));

            Vector2 clamped = new Vector2(sp.x, sp.y);
            if (!onScreen)
            {
                Vector2 c = new Vector2(Screen.width, Screen.height) * 0.5f;
                Vector2 d = new Vector2(sp.x, sp.y) - c;
                if (d.sqrMagnitude < 1f) d = Vector2.down;
                float sx = (safe.width * 0.5f) / Mathf.Max(0.001f, Mathf.Abs(d.x));
                float sy = (safe.height * 0.5f) / Mathf.Max(0.001f, Mathf.Abs(d.y));
                clamped = c + d * Mathf.Min(sx, sy);
                if (waypointArrowRT != null) waypointArrowRT.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)waypointRT.parent, clamped, null, out Vector2 local);
            waypointRT.anchoredPosition = local;
            if (waypointArrowRT != null) waypointArrowRT.gameObject.SetActive(!onScreen);
            if (waypointMarkerRT != null)
            {
                waypointMarkerRT.localScale = Vector3.one * (onScreen ? 1f : 0.75f);
                waypointMarkerRT.anchoredPosition = new Vector2(0f, Mathf.Sin(Time.unscaledTime * 3f) * 5f);
            }
        }

        private Transform FindPlayer()
        {
            if (playerT == null) { var p = FindAnyObjectByType<TopDownPlayerController>(); if (p != null) playerT = p.transform; }
            return playerT;
        }

        // ================================================================== HELPERS

        private IEnumerator Punch(RectTransform rt, float scale, float d)
        {
            if (rt == null) yield break;
            float t = 0f;
            while (t < d) { t += Time.unscaledDeltaTime; float p = Mathf.Clamp01(t / d); rt.localScale = Vector3.one * Mathf.Lerp(scale, 1f, 1f - Mathf.Pow(1f - p, 3f)); yield return null; }
            rt.localScale = Vector3.one;
        }

        private IEnumerator FillTo(Image img, float to, float d)
        {
            float from = img.fillAmount, t = 0f;
            while (t < d) { t += Time.unscaledDeltaTime; img.fillAmount = Mathf.Lerp(from, to, t / d); yield return null; }
            img.fillAmount = to;
        }

        private IEnumerator FlashColor(TextMeshProUGUI txt, Color flash, float d)
        {
            Color orig = txt.color; float t = 0f;
            while (t < d) { t += Time.unscaledDeltaTime; txt.color = Color.Lerp(flash, orig, t / d); yield return null; }
            txt.color = orig;
        }

        private static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }
        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        private static float EaseOutBack(float x) { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f); }
    }
}
