using UnityEngine;

/// <summary>
/// Musica generativa y adaptativa (sin archivos). Capas:
///  - Drone: dos sierras desafinadas + filtro pasa-bajos que se abre con la tension.
///  - Secuenciador: notas aleatorias (random walk) sobre escala pentatonica menor; tempo y densidad suben con la tension.
///  - Latido (lub-dub) que aparece y se acelera con tension alta.
///  - Tritono disonante que entra con tension > 0.5.
///  - Eco (delay con realimentacion) para ambiente.
/// 'tension' (0..1) la fija TensionManager segun la cercania de los enemigos.
/// </summary>
public class AdaptiveMusic : ProceduralVoice
{
    [Range(0f, 1f)] public float tension;
    public float masterGain = 0.35f;
    public float rootHz = 55f; // La1

    static readonly int[] scale = { 0, 3, 5, 7, 10, 12, 15 };

    uint seed = 362436069u;
    float cur;
    float pA, pB, lpA, lpB, tritPh;
    int stepLeft, noteIdx = 2;
    float noteFreq = 220f, notePh, noteEnv, noteDecay;
    int beatClock, dubCountdown;
    float thumpPh, thumpF = 60f, thumpEnv, thumpDecay;
    float[] delay; int dp;

    float Rand01()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 16777215f;
    }

    protected override void Awake()
    {
        delay = new float[(int)(AudioSettings.outputSampleRate * 0.38f)];
        base.Awake();
        noteDecay = Mathf.Exp(-1f / (0.28f * sampleRate));
        thumpDecay = Mathf.Exp(-1f / (0.05f * sampleRate));
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            cur += (tension - cur) * 0.00003f; // transicion suave (~1 s)

            // --- Secuenciador de notas ---
            if (--stepLeft <= 0)
            {
                float bpm = 70f + 60f * cur;
                stepLeft = (int)(sampleRate * 60f / bpm / 2f);
                if (Rand01() < 0.3f + 0.55f * cur)
                {
                    int dir = Rand01() < 0.5f ? -1 : 1;
                    noteIdx = Mathf.Clamp(noteIdx + dir * (1 + (int)(Rand01() * 2f)), 0, scale.Length - 1);
                    float oct = Rand01() < 0.3f ? 8f : 4f;
                    noteFreq = rootHz * oct * Mathf.Pow(2f, scale[noteIdx] / 12f);
                    noteEnv = 0.6f + 0.4f * Rand01();
                }
            }
            notePh += noteFreq * inv; if (notePh > 1f) notePh -= 1f;
            float note = (Mathf.Sin(tp * notePh) + 0.3f * Mathf.Sin(2f * tp * notePh)) * noteEnv * 0.22f;
            noteEnv *= noteDecay;

            // --- Drone ---
            pA += rootHz * inv; if (pA > 1f) pA -= 1f;
            pB += rootHz * 1.006f * inv; if (pB > 1f) pB -= 1f;
            float a = 0.01f + 0.08f * cur;
            lpA += a * ((2f * pA - 1f) - lpA);
            lpB += a * ((2f * pB - 1f) - lpB);
            // El drone solo aparece cuando sube la tension (en calma no hay tono continuo)
            float droneLevel = Mathf.Clamp01((cur - 0.25f) / 0.5f) * 0.6f;
            float drone = (lpA + lpB) * 0.5f * droneLevel;

            // --- Tritono disonante ---
            tritPh += rootHz * 4f * 1.4142f * inv; if (tritPh > 1f) tritPh -= 1f;
            float trit = Mathf.Sin(tp * tritPh) * Mathf.Max(0f, cur - 0.5f) * 0.25f;

            // --- Latido ---
            float heart = 0f;
            if (cur > 0.35f)
            {
                int interval = (int)(sampleRate * 60f / (55f + 85f * cur));
                if (++beatClock >= interval)
                {
                    beatClock = 0; thumpEnv = 1f; thumpF = 60f; dubCountdown = (int)(sampleRate * 0.22f);
                }
                if (dubCountdown > 0 && --dubCountdown == 0) { thumpEnv = 0.7f; thumpF = 52f; }
                thumpPh += thumpF * inv; if (thumpPh > 1f) thumpPh -= 1f;
                thumpF *= 0.99995f;
                float gate = Mathf.Clamp01((cur - 0.35f) / 0.3f);
                heart = Mathf.Sin(tp * thumpPh) * thumpEnv * 0.9f * gate;
                thumpEnv *= thumpDecay;
            }

            float dry = drone + note + trit + heart;
            float del = delay[dp];
            delay[dp] = dry + del * 0.45f;
            if (++dp >= delay.Length) dp = 0;
            float s = dry + del * 0.35f;
            d[i] = (float)System.Math.Tanh(s * masterGain * 2.2f);
        }
    }
}