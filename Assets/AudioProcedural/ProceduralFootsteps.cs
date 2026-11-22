using UnityEngine;

/// <summary>
/// Pasos sintetizados: rafaga de ruido filtrado (roce/contacto) + golpe grave con caida de tono (impacto).
/// Cada paso es distinto (aleatoriedad) y depende de la superficie y la intensidad (velocidad).
/// </summary>
public class ProceduralFootsteps : ProceduralVoice
{
    public enum Surface { Wood, Stone, Carpet }

    volatile bool trigger;
    volatile float pIntensity = 1f;
    volatile int pSurface;

    uint seed = 88172645u;
    float noiseEnv, noiseDecay, lp, lpCoef;
    float thumpPh, thumpF, thumpEnv, thumpDecay;

    float Rand() // [-1,1], seguro en hilo de audio
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 8388608f - 1f;
    }

    /// <summary>Llamar desde el hilo principal cada vez que el personaje da un paso.</summary>
    public void Step(float intensity = 1f, Surface surface = Surface.Wood)
    {
        pIntensity = Mathf.Clamp01(intensity);
        pSurface = (int)surface;
        trigger = true;
    }

    protected override void Generate(float[] d)
    {
        for (int i = 0; i < d.Length; i++)
        {
            if (trigger)
            {
                trigger = false;
                float amp = pIntensity * (0.75f + 0.25f * Rand());
                float tauN, tauT, c, f0;
                switch (pSurface)
                {
                    case 1: tauN = 0.040f; tauT = 0.050f; c = 0.55f; f0 = 130f; break; // piedra: brillante
                    case 2: tauN = 0.030f; tauT = 0.060f; c = 0.07f; f0 = 90f; break;  // alfombra: sordo
                    default: tauN = 0.035f; tauT = 0.045f; c = 0.25f; f0 = 110f; break; // madera
                }
                c *= 1f + 0.3f * Rand();           // variacion paso a paso
                f0 *= 1f + 0.15f * Rand();
                lpCoef = Mathf.Clamp(c, 0.02f, 0.9f);
                noiseDecay = Mathf.Exp(-1f / (tauN * sampleRate));
                thumpDecay = Mathf.Exp(-1f / (tauT * sampleRate));
                noiseEnv = amp; thumpEnv = amp; thumpF = f0;
            }

            float n = Rand();
            lp += lpCoef * (n - lp);
            thumpPh += thumpF / sampleRate; if (thumpPh > 1f) thumpPh -= 1f;
            thumpF *= 0.99985f; // caida de tono

            float s = lp * noiseEnv * 1.2f + Mathf.Sin(2f * Mathf.PI * thumpPh) * thumpEnv * 0.6f;
            noiseEnv *= noiseDecay; thumpEnv *= thumpDecay;
            d[i] = Mathf.Clamp(s, -1f, 1f);
        }
    }
}
