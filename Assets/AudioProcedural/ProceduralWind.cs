using UnityEngine;

/// <summary>
/// Viento y atmosfera: ruido blanco -> filtro pasa-banda (State Variable Filter) cuya frecuencia
/// se modula con tres LFO lentos (rafagas) + rumor grave. 'intensity' se controla desde el juego.
/// </summary>
public class ProceduralWind : ProceduralVoice
{
    [Range(0f, 1f)] public float intensity = 0.5f;
    public float gain = 0.7f;

    uint seed = 2463534242u;
    float low, band, smoothI, rumble, ph1, ph2, ph3;

    float Noise()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 8388608f - 1f;
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            smoothI += (intensity - smoothI) * 0.00005f; // suaviza cambios (~0.4 s)

            ph1 += 0.13f * inv; if (ph1 > 1f) ph1 -= 1f;
            ph2 += 0.31f * inv; if (ph2 > 1f) ph2 -= 1f;
            ph3 += 0.071f * inv; if (ph3 > 1f) ph3 -= 1f;
            float gust = 0.5f + 0.25f * Mathf.Sin(tp * ph1) + 0.15f * Mathf.Sin(tp * ph2) + 0.10f * Mathf.Sin(tp * ph3);

            float fc = 250f + 900f * gust * (0.4f + smoothI);
            float f = 2f * Mathf.Sin(Mathf.PI * fc * inv);
            float x = Noise();
            float high = x - low - 0.35f * band; // resonancia
            band += f * high;
            low += f * band;

            rumble += 0.002f * (x - rumble);
            float s = band * 0.45f * (0.2f + gust) * smoothI + rumble * 7f * smoothI;
            d[i] = Mathf.Clamp(s * gain, -1f, 1f);
        }
    }
}
