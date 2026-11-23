using UnityEngine;

/// <summary>
/// PASOS SUAVES SOBRE ALFOMBRA (alternativa a ProceduralFootsteps).
///
/// Cada paso se sintetiza solo con ruido (sin componentes tonales), en dos fases talon -> punta:
///   - PISOTON: ruido blanco por un pasa-bajos de 3 polos (SVF 2 polos + 1 polo) con corte 260-380 Hz,
///     y un pasa-altos suave que quita el retumbo sub-grave. Ataque de 5 ms (sin clic) y caida de ~55 ms.
///   - FIBRAS: ruido pasa-banda ancho alrededor de 1.8 kHz, muy bajo, = roce del zapato con la alfombra.
///   - La punta llega 45-80 ms despues del talon, con ~30 % del nivel.
///
/// Variacion por paso: nivel, corte del filtro, separacion talon-punta y leve diferencia pie izq./der.
/// Adaptativo: la intensidad abre un poco el filtro y, con tension alta, el personaje pisa con mas cuidado.
///
/// Hereda de ProceduralFootsteps, asi ProceduralFootstepDriver lo usa sin cambios.
/// Menu: Audio Procedural -> Pasos: usar pasos suaves.
/// </summary>
public class ProceduralFootstepsSoft : ProceduralFootsteps
{
    [Header("Pasos suaves (alfombra)")]
    public float gain = 1f;
    [Range(0f, 1f)] public float sneakWithTension = 0.3f;
    [Tooltip("El nivel es todo alfombra: ignora la superficie detectada por raycast.")]
    public bool alwaysCarpet = true;

    volatile bool trig;
    volatile float tIntensity = 1f;
    volatile int tSurface;

    struct Grain
    {
        public float env, attackStep, decay, g, low, band, low2, hp, damp;
        public int attackLeft, delay;
        public bool active, fluff;
    }

    readonly Grain[] grains = new Grain[4];
    uint rs = 2654435761u;
    bool leftFoot;

    float R01()
    {
        rs ^= rs << 13; rs ^= rs >> 17; rs ^= rs << 5;
        return (rs & 0xFFFFFF) / 16777215f;
    }

    public override void Step(float intensity = 1f, Surface surface = Surface.Wood)
    {
        float t = TensionManager.Instance != null ? TensionManager.Instance.Tension : 0f;
        tIntensity = Mathf.Clamp01(intensity) * (1f - sneakWithTension * t);
        tSurface = alwaysCarpet ? (int)Surface.Carpet : (int)surface;
        trig = true;
    }

    void Start()
    {
        rs = (uint)Random.Range(1, int.MaxValue);
    }

    void Set(int idx, float amp, float attackSec, float decaySec, float cutoffHz, int delay, bool fluff)
    {
        var gr = new Grain();
        gr.attackLeft = Mathf.Max(1, (int)(attackSec * sampleRate));
        gr.attackStep = amp / gr.attackLeft;
        gr.decay = Mathf.Exp(-1f / (decaySec * sampleRate));
        gr.g = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoffHz, 6000f) / sampleRate);
        gr.damp = fluff ? 1.6f : 1.4f;
        gr.delay = delay;
        gr.fluff = fluff;
        gr.active = true;
        grains[idx] = gr;
    }

    protected override void Generate(float[] d)
    {
        for (int i = 0; i < d.Length; i++)
        {
            if (trig)
            {
                trig = false;
                leftFoot = !leftFoot;
                float k = tIntensity;
                float foot = leftFoot ? 0.95f : 1.05f;
                float amp = k * (0.85f + 0.15f * R01());

                // alfombra por defecto; piedra/madera solo si alwaysCarpet = false
                float baseCut = tSurface == 2 ? 260f : (tSurface == 1 ? 520f : 380f);
                float cut = (baseCut + 120f * k) * foot * (0.9f + 0.2f * R01());
                int gap = (int)(sampleRate * (0.045f + 0.035f * R01()));

                Set(0, amp,         0.005f, 0.055f, cut,                          0,                        false); // talon
                Set(1, amp * 0.30f, 0.009f, 0.060f, cut * 0.85f,                  gap,                      false); // punta
                Set(2, amp * 0.22f, 0.008f, 0.035f, 1800f * (0.9f + 0.2f * R01()), (int)(sampleRate * 0.004f), true); // fibras talon
                Set(3, amp * 0.14f, 0.012f, 0.040f, 1600f * (0.9f + 0.2f * R01()), gap,                      true); // fibras punta
            }

            float w = R01() * 2f - 1f;
            float s = 0f;
            for (int n = 0; n < grains.Length; n++)
            {
                if (!grains[n].active) continue;
                ref Grain gr = ref grains[n];
                if (gr.delay > 0) { gr.delay--; continue; }
                if (gr.attackLeft > 0) { gr.env += gr.attackStep; gr.attackLeft--; }
                else { gr.env *= gr.decay; if (gr.env < 0.0002f) { gr.active = false; continue; } }

                float high = w - gr.low - gr.damp * gr.band;
                gr.band += gr.g * high;
                gr.low += gr.g * gr.band;

                if (gr.fluff)
                {
                    s += gr.band * gr.env;
                }
                else
                {
                    gr.low2 += 0.25f * (gr.low - gr.low2);   // 3er polo: mas amortiguado
                    float o = gr.low2 - gr.hp;              // pasa-altos suave: quita retumbo
                    gr.hp += 0.012f * (gr.low2 - gr.hp);
                    s += o * gr.env * 2.2f;
                }
            }
            d[i] = (float)System.Math.Tanh(s * gain * 1.5f);
        }
    }
}
