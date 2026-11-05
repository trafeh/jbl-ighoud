using UnityEngine;

namespace JbelIghoud.Survival
{
    /// <summary>
    /// Generates lightweight procedural audio clips for survival events
    /// (inhale/breath gasp, underwater bubbles, eating, hurt, sleep chime).
    /// </summary>
    public static class ProceduralSurvivalAudio
    {
        private static AudioClip inhaleBreathClip;
        private static AudioClip drownGaspClip;
        private static AudioClip eatClip;
        private static AudioClip hurtClip;
        private static AudioClip wakeChimeClip;

        public static AudioClip GetInhaleBreathClip()
        {
            if (inhaleBreathClip == null)
            {
                inhaleBreathClip = GenerateInhaleBreath();
            }
            return inhaleBreathClip;
        }

        public static AudioClip GetDrownGaspClip()
        {
            if (drownGaspClip == null)
            {
                drownGaspClip = GenerateDrownGasp();
            }
            return drownGaspClip;
        }

        public static AudioClip GetEatClip()
        {
            if (eatClip == null)
            {
                eatClip = GenerateEatSound();
            }
            return eatClip;
        }

        public static AudioClip GetHurtClip()
        {
            if (hurtClip == null)
            {
                hurtClip = GenerateHurtSound();
            }
            return hurtClip;
        }

        public static AudioClip GetWakeChimeClip()
        {
            if (wakeChimeClip == null)
            {
                wakeChimeClip = GenerateWakeChime();
            }
            return wakeChimeClip;
        }

        private static AudioClip GenerateInhaleBreath()
        {
            int sampleRate = 22050;
            float duration = 0.85f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            System.Random rand = new System.Random(42);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / numSamples;
                // Inhale envelope: crescendo then release
                float env = Mathf.Sin(t * Mathf.PI * 0.9f);
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.25f;
                // Resonant airy tone
                float airTone = Mathf.Sin(2f * Mathf.PI * (350f + t * 250f) * ((float)i / sampleRate)) * 0.15f;
                samples[i] = (noise + airTone) * env * 0.75f;
            }

            AudioClip clip = AudioClip.Create("Procedural_InhaleBreath", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateDrownGasp()
        {
            int sampleRate = 22050;
            float duration = 0.5f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            System.Random rand = new System.Random(88);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / numSamples;
                float env = (1f - t) * Mathf.Sin(t * Mathf.PI);
                float noise = ((float)rand.NextDouble() * 2f - 1f) * 0.35f;
                float bubble = Mathf.Sin(2f * Mathf.PI * (180f + Mathf.Sin(t * 30f) * 60f) * ((float)i / sampleRate)) * 0.25f;
                samples[i] = (noise + bubble) * env;
            }

            AudioClip clip = AudioClip.Create("Procedural_DrownGasp", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateEatSound()
        {
            int sampleRate = 22050;
            float duration = 0.4f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            System.Random rand = new System.Random(123);
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / numSamples;
                float crunchMod = Mathf.Sin(t * Mathf.PI * 16f);
                float env = Mathf.Pow(1f - t, 1.8f);
                float noise = ((float)rand.NextDouble() * 2f - 1f);
                float tone = Mathf.Sin(2f * Mathf.PI * 520f * ((float)i / sampleRate)) * 0.3f;
                samples[i] = (noise * 0.6f + tone) * env * (0.5f + crunchMod * 0.5f) * 0.65f;
            }

            AudioClip clip = AudioClip.Create("Procedural_EatSound", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateHurtSound()
        {
            int sampleRate = 22050;
            float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / numSamples;
                float env = Mathf.Exp(-t * 6f);
                float thud = Mathf.Sin(2f * Mathf.PI * (120f - t * 60f) * ((float)i / sampleRate)) * 0.7f;
                samples[i] = thud * env;
            }

            AudioClip clip = AudioClip.Create("Procedural_HurtThud", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateWakeChime()
        {
            int sampleRate = 22050;
            float duration = 1.6f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6 chord
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / numSamples;
                float total = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    float noteOffset = f * 0.15f;
                    float localT = t * duration - noteOffset;
                    if (localT > 0f)
                    {
                        float env = Mathf.Exp(-localT * 2.2f);
                        total += Mathf.Sin(2f * Mathf.PI * freqs[f] * ((float)i / sampleRate)) * env * 0.22f;
                    }
                }
                samples[i] = Mathf.Clamp(total, -1f, 1f) * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Procedural_WakeChime", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
