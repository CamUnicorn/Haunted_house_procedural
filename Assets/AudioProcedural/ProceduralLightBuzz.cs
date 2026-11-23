using UnityEngine;

/// <summary>
/// Zumbido electrico de las lamparas, sincronizado con el parpadeo VISUAL de la luz.
/// Sintesis aditiva: hum de red (60 Hz) con 8 armonicos (los impares mas fuertes, tipo transformador)
/// + chisporroteo: impulsos de ruido filtrado cuya probabilidad depende de cuanto cambia la luz.
///
/// Parametros del juego que lo controlan (se leen cada frame en el hilo principal):
///  - Light.intensity de la lampara (normalizada con lightIntensityMin/Max de LightFlicker) -> volumen y brillo.
///  - Derivada de la intensidad (|cambio| por segundo) -> densidad de chispazos.
/// Va en un hijo de cada objeto con LightFlicker (lo crea el menu "Audio Procedural/Configurar escena").
/// </summary>
public class ProceduralLightBuzz : ProceduralVoice
{
    public Light targetLight;
    public float minIntensity = 1.25f;
    public float maxIntensity = 2.25f;
    public float humHz = 60f;
    public float gain = 0.25f;

    volatile float level;      // 0..1 brillo normalizado
    volatile float crackle;    // 0..1 probabilidad de chispazo

    uint seed = 1442695041u;
    float ph, smooth, sparkEnv, sparkLp, lastIntensity;

    float Rand01()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 16777215f;
    }

    void Reset() { Configure3D(0.5f, 6f, 1f); }

    void Start()
    {
        if (targetLight == null)
        {
            var lf = GetComponentInParent<StealthGame.LightFlicker>();
            if (lf != null)
            {
                targetLight = lf.flickeringLight;
                minIntensity = lf.lightIntensityMin;
                maxIntensity = lf.lightIntensityMax;
            }
            if (targetLight == null) targetLight = GetComponentInParent<Light>();
        }
        if (targetLight != null) lastIntensity = targetLight.intensity;
        seed = (uint)Random.Range(1, int.MaxValue); // cada lampara suena distinto
        humHz *= Random.Range(0.995f, 1.005f);
    }

    void Update()
    {
        if (targetLight == null) { level = 0f; return; }
        float it = targetLight.enabled ? targetLight.intensity : 0f;
        level = Mathf.Clamp01(Mathf.InverseLerp(minIntensity * 0.5f, maxIntensity, it));
        float dIt = Mathf.Abs(it - lastIntensity) / Mathf.Max(Time.deltaTime, 0.001f);
        lastIntensity = it;
        crackle = Mathf.Clamp01(dIt / (maxIntensity * 10f));
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            smooth += (level - smooth) * 0.002f;

            ph += humHz * inv; if (ph > 1f) ph -= 1f;
            float hum = 0f;
            // Brillo espectral: con mas intensidad pesan mas los armonicos altos
            for (int h = 1; h <= 8; h++)
            {
                float a = ((h & 1) == 1 ? 1f : 0.45f) / h * (h > 3 ? smooth : 1f);
                hum += Mathf.Sin(tp * ph * h) * a;
            }

            // Chispazos: proceso de Poisson cuya tasa sube con 'crackle'
            if (Rand01() < crackle * 0.004f + 0.00002f) sparkEnv = 0.6f + 0.4f * Rand01();
            sparkLp += 0.5f * ((Rand01() * 2f - 1f) - sparkLp);
            float spark = sparkLp * sparkEnv;
            sparkEnv *= 0.9985f;

            float s = hum * 0.12f * (0.25f + 0.75f * smooth) + spark * 0.5f;
            d[i] = Mathf.Clamp(s * gain, -1f, 1f);
        }
    }
}
