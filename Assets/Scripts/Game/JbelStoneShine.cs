using System.Collections.Generic;
using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// "Mystery item" shine on a collectible stone.
    /// Far away the stone is a glowing, sparkling shape (you can't tell what it is).
    /// Within <see cref="revealRadius"/> the glow melts away and the real stone appears, keeping a soft gleam.
    /// Sparkles twinkle on the stone's own surface (mesh vertices), plus a soft halo behind it.
    /// Halo + sparkles only show within <see cref="radius"/> metres of the player.
    /// </summary>
    [DisallowMultipleComponent]
    public class JbelStoneShine : MonoBehaviour
    {
        public float radius = 50f;
        [Tooltip("Closer than this, the stone's real look is revealed.")]
        public float revealRadius = 5f;
        public Color glowColor = new Color(1f, 0.93f, 0.75f);
        public int sparkleCount = 6;

        private static Sprite starSprite, haloSprite;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        private Renderer rend;
        private MeshFilter mf;
        private MaterialPropertyBlock mpb;
        private SpriteRenderer halo;
        private readonly List<SpriteRenderer> sparkles = new List<SpriteRenderer>();
        private readonly List<Vector3> sparkleLocal = new List<Vector3>();
        private readonly List<float> sparklePhase = new List<float>();
        private Transform player;
        private Color realColor = Color.gray;
        private bool colorCaptured;
        private int startFrame;
        private float mystery = 1f, shown;
        private float seed;

        private void Awake()
        {
            rend = GetComponent<Renderer>();
            mf = GetComponent<MeshFilter>();
            mpb = new MaterialPropertyBlock();
            seed = Random.value * 10f;
            startFrame = Time.frameCount;

            halo = MakeSprite("StoneHalo", GetHalo(), 49);
            for (int i = 0; i < sparkleCount; i++)
            {
                sparkles.Add(MakeSprite("StoneSparkle", GetStar(), 51));
                sparklePhase.Add(Random.value * 20f);
            }
        }

        private SpriteRenderer MakeSprite(string name, Sprite s, int order)
        {
            var go = new GameObject(name); // world space: independent from the rock's big mesh scale
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        private void PickSparklePoints()
        {
            sparkleLocal.Clear();
            Mesh m = mf != null ? mf.sharedMesh : null;
            Vector3[] v = (m != null && m.isReadable) ? m.vertices : null;
            for (int i = 0; i < sparkles.Count; i++)
            {
                if (v != null && v.Length > 0) sparkleLocal.Add(v[Random.Range(0, v.Length)]);
                else if (m != null) sparkleLocal.Add(Vector3.Scale(Random.onUnitSphere * 0.5f, m.bounds.size) + m.bounds.center);
                else sparkleLocal.Add(Random.onUnitSphere * 0.5f);
            }
        }

        private void OnEnable() { SetSpritesVisible(true); }
        private void OnDisable() { SetSpritesVisible(false); ClearBlock(); }

        private void OnDestroy()
        {
            ClearBlock();
            if (halo != null) Destroy(halo.gameObject);
            foreach (var s in sparkles) if (s != null) Destroy(s.gameObject);
        }

        private void SetSpritesVisible(bool on)
        {
            if (halo != null) halo.enabled = on;
            foreach (var s in sparkles) if (s != null) s.enabled = on;
        }

        private void ClearBlock()
        {
            if (rend != null) rend.SetPropertyBlock(null);
        }

        private void LateUpdate()
        {
            // RockItem sets the real colour in Start(): read it once that has happened
            if (!colorCaptured && Time.frameCount > startFrame + 1 && rend != null)
            {
                var mat = rend.sharedMaterial;
                if (mat != null) realColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : mat.color;
                colorCaptured = true;
                PickSparklePoints();
            }

            if (player == null)
            {
                var p = TopDownPlayerController.Instance;
                if (p != null) player = p.transform;
            }
            Camera cam = Camera.main;
            if (player == null || cam == null || rend == null) return;

            float d = Vector3.Distance(player.position, transform.position);
            float dt = Time.deltaTime;

            // 1 = unknown glowing object, 0 = revealed real stone
            float mysteryTarget = Mathf.Clamp01((d - revealRadius) / 2.5f);
            mystery = Mathf.MoveTowards(mystery, mysteryTarget, dt * 1.2f);
            float inRange = Mathf.Clamp01((radius - d) / 5f);
            shown = Mathf.MoveTowards(shown, inRange, dt * 1.5f);

            float t = Time.time + seed;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.4f);

            // ---- the stone itself
            Color baseCol = Color.Lerp(realColor, Color.white, mystery * 0.9f);
            float emis = Mathf.Lerp(0.22f * pulse, 3.0f * pulse, mystery) * Mathf.Max(shown, mystery * 0.6f);
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, baseCol);
            mpb.SetColor(EmissionId, glowColor * emis);
            rend.SetPropertyBlock(mpb);

            // ---- halo behind the stone
            Bounds b = rend.bounds;
            float size = b.extents.magnitude;
            Vector3 camPos = cam.transform.position;
            float camDist = Vector3.Distance(camPos, b.center);
            float readable = Mathf.Clamp(camDist * 0.03f, 0f, 2.5f); // keeps the shine readable far away
            Vector3 toCam = (camPos - b.center).normalized;
            halo.transform.position = b.center - toCam * (size * 0.4f);
            halo.transform.rotation = Quaternion.LookRotation(-toCam);
            float haloSize = Mathf.Max(size * 4.5f, readable * 2.2f) * (0.9f + 0.1f * pulse);
            halo.transform.localScale = Vector3.one * haloSize;
            halo.color = new Color(glowColor.r, glowColor.g, glowColor.b, shown * Mathf.Lerp(0.2f, 0.85f, mystery) * pulse);

            // ---- sparkles twinkling on the stone's surface
            if (sparkleLocal.Count == sparkles.Count)
            {
                Matrix4x4 m = transform.localToWorldMatrix;
                for (int i = 0; i < sparkles.Count; i++)
                {
                    var s = sparkles[i];
                    Vector3 wp = m.MultiplyPoint3x4(sparkleLocal[i]);
                    wp += (camPos - wp).normalized * 0.04f; // just in front of the surface
                    float tw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (1.6f + i * 0.37f) + sparklePhase[i])), 10f);
                    float sz = Mathf.Max(size * 1.1f, readable * 1.1f) * (0.35f + 0.9f * tw);
                    s.transform.position = wp;
                    s.transform.rotation = Quaternion.LookRotation(wp - camPos) * Quaternion.Euler(0f, 0f, t * 30f + i * 40f);
                    s.transform.localScale = Vector3.one * sz;
                    s.color = new Color(1f, 0.97f, 0.88f, shown * tw * Mathf.Lerp(0.55f, 1f, mystery));
                }
            }
        }

        // ------------------------------------------------------------ procedural sprites
        private static Sprite GetStar()
        {
            if (starSprite != null) return starSprite;
            starSprite = BuildSprite(64, (u, v, r) =>
            {
                float core = Mathf.Exp(-r * r * 40f);
                float rays = (Mathf.Exp(-Mathf.Abs(u) * 30f) + Mathf.Exp(-Mathf.Abs(v) * 30f)) * Mathf.Clamp01(1f - r) * 0.9f;
                float diag = (Mathf.Exp(-Mathf.Abs(u - v) * 40f) + Mathf.Exp(-Mathf.Abs(u + v) * 40f)) * Mathf.Clamp01(1f - r * 1.6f) * 0.35f;
                return Mathf.Clamp01(core + rays + diag);
            });
            return starSprite;
        }

        private static Sprite GetHalo()
        {
            if (haloSprite != null) return haloSprite;
            haloSprite = BuildSprite(64, (u, v, r) => Mathf.Clamp01(Mathf.Exp(-r * r * 5f) - 0.02f));
            return haloSprite;
        }

        private static Sprite BuildSprite(int n, System.Func<float, float, float, float> alpha)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                px[y * n + x] = new Color(1f, 1f, 1f, alpha(u, v, Mathf.Sqrt(u * u + v * v)));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }
    }
}
