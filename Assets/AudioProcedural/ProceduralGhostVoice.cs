using UnityEngine;

/// <summary>
/// Voz sobrenatural para enemigos: lamento con armonicos, vibrato, tremolo y aliento (ruido).
/// La 'agitation' (0..1) sube tono, velocidad de vibrato y aspereza. Se calcula sola segun la
/// cercania al AudioListener, asi el enemigo "se enfurece" al acercarse.
/// </summary>
public class ProceduralGhostVoice : ProceduralVoice
{
    [Range(0f, 1f)] public float agitation;
    public float baseHz = 240f;
    public float maxDistance = 12f;
    public bool autoAgitation = true;

    uint seed = 123456789u;
    float ph, vibPh, tremPh, drift, breath, smoothA;

    float Noise()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 8388608f - 1f;
    }

    void Update()
    {
        if (!autoAgitation) return;
        var l = FindObjectOfType<AudioListener>();
        if (l == null) return;
        float dist = Vector3.Distance(l.transform.position, transform.position);
        agitation = 1f - Mathf.Clamp01(dist / maxDistance);
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            smoothA += (agitation - smoothA) * 0.0001f;
            drift += 0.00002f * Noise();                 // deriva lenta del tono (random walk)
            drift = Mathf.Clamp(drift, -0.25f, 0.25f);
            vibPh += (4f + 4f * smoothA) * inv; if (vibPh > 1f) vibPh -= 1f;
            tremPh += (2.2f + 3f * smoothA) * inv; if (tremPh > 1f) tremPh -= 1f;

            float f = baseHz * (1f + drift + 0.3f * smoothA) * (1f + 0.02f * Mathf.Sin(tp * vibPh));
            ph += f * inv; if (ph > 1f) ph -= 1f;

            float voice = Mathf.Sin(tp * ph) + (0.5f + 0.4f * smoothA) * Mathf.Sin(2f * tp * ph)
                        + 0.25f * Mathf.Sin(3f * tp * ph);
            breath += 0.25f * (Noise() - breath);
            float trem = 0.65f + 0.35f * Mathf.Sin(tp * tremPh);
            float s = (voice * 0.18f + breath * (0.12f + 0.2f * smoothA)) * trem * (0.25f + 0.75f * smoothA);
            d[i] = Mathf.Clamp(s, -1f, 1f);
        }
    }
}
