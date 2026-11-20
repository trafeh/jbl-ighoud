using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Shared stone-impact feedback, scaled by hardness:
///  - particles: hot sparks + stone chips for hard stones, a dust puff in the stone's colour for soft ones
///  - procedural sound: sharp "tick/crack" for hard stones, dull "thud" for soft ones (no audio assets needed)
///  - phone haptics: short & strong for hard, longer & soft for soft (Android VibrationEffect, iOS fallback)
///  - camera impulse (Cinemachine) + a tiny hit-stop
/// Replaces the old primitive sphere / cube placeholders.
/// </summary>
public static class JbelImpactFX
{
    private static Material sparkMat, dustMat;
    private static AudioClip[] hardClips, midClips, softClips;
    private static CinemachineImpulseSource impulse;
    private static bool hitStopRunning;

    // ================================================================== PUBLIC API

    /// <summary>Full strike feedback between two stones. Hardness on the RockData 1..20 scale.</summary>
    public static void Strike(Vector3 pos, float hardnessA, float hardnessB, Color dustColor, float intensity = 1f)
    {
        float hard = Mathf.Min(hardnessA, hardnessB);   // the softer stone decides the character of the hit
        float hardest = Mathf.Max(hardnessA, hardnessB);
        float h01 = Mathf.InverseLerp(5f, 20f, hard);

        Particles(pos, h01, hardest >= 17f && hard >= 15f, dustColor, intensity);
        Sound(h01, intensity);
        Haptics(h01, intensity);
        Shake(Mathf.Lerp(0.35f, 0.9f, h01) * intensity);
        HitStop(Mathf.Lerp(0.035f, 0.07f, h01) * Mathf.Clamp01(intensity));
    }

    /// <summary>Small hit with no partner stone (single hit on a rock).</summary>
    public static void Hit(Vector3 pos, float hardness, Color dustColor)
    {
        float h01 = Mathf.InverseLerp(5f, 20f, hardness);
        Particles(pos, h01, hardness >= 17f, dustColor, 0.6f);
        Sound(h01, 0.7f);
    }

    /// <summary>A stone breaking apart: chunks + big dust puff.</summary>
    public static void Shatter(Vector3 pos, Color color, float size = 1f)
    {
        var ps = MakeSystem("StoneShatter", pos, Dust(), 1.6f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * size, 2.2f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f * size, 0.2f * size);
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.35f));
        main.gravityModifier = 0.6f;
        Burst(ps, 18);
        Sound(0.4f, 1f);
        Haptics(0.6f, 1f);
        Shake(0.9f);
    }

    // ================================================================== PARTICLES

    private static void Particles(Vector3 pos, float h01, bool sparks, Color dustColor, float intensity)
    {
        // dust: more and bigger for soft stones
        var dust = MakeSystem("StoneDust", pos, Dust(), 1.2f);
        var dm = dust.main;
        dm.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
        dm.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, Mathf.Lerp(0.9f, 0.45f, h01));
        dm.startSize = new ParticleSystem.MinMaxCurve(Mathf.Lerp(0.05f, 0.025f, h01), Mathf.Lerp(0.16f, 0.07f, h01));
        Color dc = dustColor; dc.a = Mathf.Lerp(0.85f, 0.45f, h01);
        dm.startColor = new ParticleSystem.MinMaxGradient(dc, Color.Lerp(dc, Color.white, 0.3f));
        dm.gravityModifier = -0.05f;
        var col = dust.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = dust.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        Burst(dust, Mathf.RoundToInt(Mathf.Lerp(14f, 5f, h01) * intensity));

        // chips: small stone fragments flying out, more for hard stones
        var chips = MakeSystem("StoneChips", pos, Spark(), 1f);
        var cm = chips.main;
        cm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        cm.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, Mathf.Lerp(1.6f, 3.2f, h01));
        cm.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
        cm.startColor = Color.Lerp(dustColor, Color.gray, 0.4f);
        cm.gravityModifier = 1.4f;
        Burst(chips, Mathf.RoundToInt(Mathf.Lerp(3f, 10f, h01) * intensity));

        if (!sparks) return;
        // sparks: only hard-on-hard (flint, basalt, sandstone)
        var sp = MakeSystem("StoneSparks", pos, Spark(), 0.6f);
        var sm = sp.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        sm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
        sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.75f), new Color(1f, 0.62f, 0.2f));
        sm.gravityModifier = 0.8f;
        var r = sp.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.03f;
        r.lengthScale = 1.5f;
        Burst(sp, Mathf.RoundToInt(Random.Range(10, 20) * intensity));
    }

    private static ParticleSystem MakeSystem(string name, Vector3 pos, Material mat, float life)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        var em = ps.emission; em.rateOverTime = 0f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.02f;
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        Object.Destroy(go, life + 0.5f);
        return ps;
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        if (count <= 0) { Object.Destroy(ps.gameObject); return; }
        ps.Emit(count);
    }

    private static Material Spark()
    {
        if (sparkMat == null) sparkMat = Resources.Load<Material>("FX/M_ImpactSpark");
        return sparkMat;
    }

    private static Material Dust()
    {
        if (dustMat == null) dustMat = Resources.Load<Material>("FX/M_ImpactDust");
        return dustMat;
    }

    // ================================================================== SOUND (procedural)

    private static void Sound(float h01, float intensity)
    {
        if (hardClips == null) BuildClips();
        AudioClip[] set = h01 > 0.66f ? hardClips : (h01 > 0.33f ? midClips : softClips);
        AudioClip clip = set[Random.Range(0, set.Length)];
        float vol = Mathf.Clamp01(0.55f + 0.45f * intensity);
        var am = JbelIghoud.Audio.GameAudioManager.Instance;
        if (am != null) am.PlaySFX(clip, vol);
        else
        {
            var cam = Camera.main;
            AudioSource.PlayClipAtPoint(clip, cam != null ? cam.transform.position : Vector3.zero, vol);
        }
    }

    private static void BuildClips()
    {
        hardClips = new AudioClip[3];
        midClips = new AudioClip[3];
        softClips = new AudioClip[3];
        for (int i = 0; i < 3; i++)
        {
            float v = 1f + (i - 1) * 0.07f;
            hardClips[i] = Synth("stone_hard_" + i, 0.16f, v, 1f);
            midClips[i] = Synth("stone_mid_" + i, 0.2f, v, 0.5f);
            softClips[i] = Synth("stone_soft_" + i, 0.26f, v, 0f);
        }
    }

    /// <summary>Stone impact: noise transient + resonant body. hardness 1 = bright crack, 0 = dull thud.</summary>
    private static AudioClip Synth(string name, float seconds, float pitch, float hardness)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(seconds * rate);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float lp = 0f;
        float lpK = Mathf.Lerp(0.08f, 0.65f, hardness);          // brighter noise for hard stones
        float f1 = Mathf.Lerp(150f, 2600f, hardness) * pitch;   // main resonance
        float f2 = Mathf.Lerp(260f, 4100f, hardness) * pitch;
        float fBody = Mathf.Lerp(95f, 210f, hardness) * pitch;
        float decayNoise = Mathf.Lerp(28f, 160f, hardness);
        float decayRing = Mathf.Lerp(22f, 55f, hardness);
        float decayBody = Mathf.Lerp(16f, 40f, hardness);
        float peak = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (noise - lp) * lpK;
            float attack = Mathf.Clamp01(t / 0.0015f);
            float s = lp * Mathf.Exp(-t * decayNoise) * 1.1f
                    + Mathf.Sin(2f * Mathf.PI * f1 * t) * Mathf.Exp(-t * decayRing) * 0.35f * hardness
                    + Mathf.Sin(2f * Mathf.PI * f2 * t) * Mathf.Exp(-t * decayRing * 1.4f) * 0.2f * hardness
                    + Mathf.Sin(2f * Mathf.PI * fBody * t) * Mathf.Exp(-t * decayBody) * Mathf.Lerp(0.9f, 0.35f, hardness);
            s *= attack;
            data[i] = s;
            peak = Mathf.Max(peak, Mathf.Abs(s));
        }
        float norm = peak > 0.0001f ? 0.9f / peak : 1f;
        for (int i = 0; i < n; i++) data[i] *= norm;
        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ================================================================== HAPTICS

    public static void Haptics(float h01, float intensity)
    {
        if (h01 > 0.66f)
        {
            Vibrate(18, 255);                                   // sharp tick
            if (h01 > 0.9f) Runner.Instance.Delay(0.045f, () => Vibrate(12, 200)); // flint "ring"
        }
        else if (h01 > 0.33f) Vibrate(32, 190);
        else Vibrate(65, 90);                                   // soft, dull
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject vibrator;
    private static int sdk = -1;
#endif

    public static void Vibrate(int milliseconds, int amplitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (sdk < 0) using (var ver = new AndroidJavaClass("android.os.Build$VERSION")) sdk = ver.GetStatic<int>("SDK_INT");
            if (vibrator == null)
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            if (vibrator == null) { Handheld.Vibrate(); return; }
            if (sdk >= 26)
            {
                using (var ve = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = ve.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    vibrator.Call("vibrate", effect);
            }
            else vibrator.Call("vibrate", (long)milliseconds);
        }
        catch { Handheld.Vibrate(); }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    // ================================================================== CAMERA

    private static void Shake(float force)
    {
        if (impulse == null)
        {
            var go = new GameObject("[JbelImpactShake]");
            Object.DontDestroyOnLoad(go);
            impulse = go.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = 0.18f;
        }
        Vector3 v = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.6f, -1f), 0f).normalized * 0.06f * force;
        impulse.GenerateImpulseWithVelocity(v);
    }

    /// <summary>Make sure a camera reacts to the impulse.</summary>
    public static void EnsureListener(CinemachineCamera cam)
    {
        if (cam != null && cam.GetComponent<CinemachineImpulseListener>() == null)
            cam.gameObject.AddComponent<CinemachineImpulseListener>();
    }

    private static void HitStop(float seconds)
    {
        if (seconds <= 0f || hitStopRunning || Time.timeScale < 0.5f) return;
        Runner.Instance.StartCoroutine(HitStopRoutine(seconds));
    }

    private static IEnumerator HitStopRoutine(float seconds)
    {
        hitStopRunning = true;
        float prev = Time.timeScale;
        Time.timeScale = 0.06f;
        yield return new WaitForSecondsRealtime(seconds);
        if (Time.timeScale < 0.1f) Time.timeScale = prev;
        hitStopRunning = false;
    }

    /// <summary>Tiny coroutine host for the static helpers.</summary>
    private class Runner : MonoBehaviour
    {
        private static Runner inst;
        public static Runner Instance
        {
            get
            {
                if (inst == null)
                {
                    var go = new GameObject("[JbelImpactFX]");
                    Object.DontDestroyOnLoad(go);
                    inst = go.AddComponent<Runner>();
                }
                return inst;
            }
        }

        public void Delay(float seconds, System.Action a) { StartCoroutine(DelayRoutine(seconds, a)); }
        private IEnumerator DelayRoutine(float s, System.Action a) { yield return new WaitForSecondsRealtime(s); a(); }
    }
}
