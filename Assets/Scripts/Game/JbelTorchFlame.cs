using UnityEngine;

namespace JbelIghoud.Game
{
    /// <summary>
    /// A burning torch tip: looping flame particles, a few rising embers and a flickering warm point light.
    /// <see cref="intensity01"/> fades the whole thing in/out (used when the Elder lights the cave).
    /// Put it on the torch object; <see cref="tipHeight"/> is measured from the torch's pivot (bottom).
    /// </summary>
    public class JbelTorchFlame : MonoBehaviour
    {
        [Range(0f, 1f)] public float intensity01 = 1f;
        public float tipHeight = 1.15f;
        public float lightIntensity = 5f;
        public float lightRange = 12f;
        public Color lightColor = new Color(1f, 0.58f, 0.24f);

        private Light lightComp;
        private ParticleSystem flame, embers;
        private Transform tip;
        private float seed;
        private float shown = -1f;

        private void Awake()
        {
            seed = Random.value * 100f;
            tip = new GameObject("FlameTip").transform;
            tip.SetParent(transform, false);
            tip.localPosition = Vector3.up * tipHeight;

            var lgo = new GameObject("TorchLight");
            lgo.transform.SetParent(tip, false);
            lgo.transform.localPosition = Vector3.up * 0.15f;
            lightComp = lgo.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = lightColor;
            lightComp.range = lightRange;
            lightComp.shadows = LightShadows.None;
            lightComp.intensity = 0f;

            flame = MakeFlame();
            embers = MakeEmbers();
        }

        private ParticleSystem MakeFlame()
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(tip, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.17f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.15f;
            main.maxParticles = 60;
            var em = ps.emission; em.rateOverTime = 26f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 0.035f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.93f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.45f), new GradientColorKey(new Color(0.75f, 0.18f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.15f)));
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("FX/M_ImpactDust");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private ParticleSystem MakeEmbers()
        {
            var go = new GameObject("Embers");
            go.transform.SetParent(tip, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.4f), new Color(1f, 0.45f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = -0.08f;
            main.maxParticles = 30;
            var em = ps.emission; em.rateOverTime = 5f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.04f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.35f; noise.frequency = 1.5f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Resources.Load<Material>("FX/M_ImpactSpark");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private void Update()
        {
            tip.localPosition = Vector3.up * tipHeight;
            float t = Time.time + seed;
            float flicker = 0.82f + 0.12f * Mathf.PerlinNoise(t * 7f, 0.3f) + 0.06f * Mathf.Sin(t * 23f);
            lightComp.intensity = lightIntensity * intensity01 * flicker;

            if (Mathf.Abs(shown - intensity01) > 0.01f)
            {
                shown = intensity01;
                var fe = flame.emission; fe.rateOverTime = 26f * intensity01;
                var ee = embers.emission; ee.rateOverTime = 5f * intensity01;
                var fm = flame.main; fm.startSizeMultiplier = Mathf.Lerp(0.4f, 1f, intensity01);
            }
        }
    }
}
