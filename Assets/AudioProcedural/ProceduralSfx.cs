using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efectos de evento sintetizados en tiempo de ejecucion (sin archivos de audio):
/// UI click, alerta de deteccion, victoria, derrota, recoger objeto y crujidos/puertas.
/// Los clips se generan por codigo y se cachean; los crujidos tienen varias variaciones.
/// </summary>
public static class ProceduralSfx
{
    const int SR = 44100;
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    const int CreakVariations = 6;

    // ---------- API publica ----------
    public static void Click() => Play2D(Get("click", MakeClick), 0.6f, 1f);
    public static void Alert() => Play2D(Get("alert", MakeAlert), 0.7f, 1f);
    public static void Win() => Play2D(Get("win", MakeWin), 0.8f, 1f);
    public static void Lose() => Play2D(Get("lose", MakeLose), 0.9f, 1f);
    public static void Pickup(Vector3 pos) => PlayAt(Get("pickup", MakePickup), pos, 0.8f, Random.Range(0.95f, 1.05f));
    public static void Creak(Vector3 pos)
    {
        int v = Random.Range(0, CreakVariations);
        PlayAt(Get("creak" + v, () => MakeCreak(v)), pos, Random.Range(0.5f, 0.9f), Random.Range(0.8f, 1.25f));
    }

    // ---------- Reproduccion ----------
    static void Play2D(AudioClip c, float vol, float pitch) => Spawn(c, Vector3.zero, vol, pitch, 0f);
    static void PlayAt(AudioClip c, Vector3 p, float vol, float pitch) => Spawn(c, p, vol, pitch, 1f);

    static void Spawn(AudioClip c, Vector3 pos, float vol, float pitch, float spatial)
    {
        var go = new GameObject("ProcSfx_" + c.name);
        go.transform.position = pos;
        var a = go.AddComponent<AudioSource>();
        a.clip = c; a.volume = vol; a.pitch = pitch; a.spatialBlend = spatial;
        a.minDistance = 2f; a.maxDistance = 25f;
        a.Play();
        Object.Destroy(go, c.length / Mathf.Max(0.1f, pitch) + 0.1f);
    }

    static AudioClip Get(string key, System.Func<AudioClip> make)
    {
        if (!cache.TryGetValue(key, out var c) || c == null) { c = make(); cache[key] = c; }
        return c;
    }

    static AudioClip Make(string name, float[] s)
    {
        var c = AudioClip.Create(name, s.Length, 1, SR, false);
        c.SetData(s, 0);
        return c;
    }

    // ---------- Sintesis ----------
    static AudioClip MakeClick()
    {
        var s = new float[(int)(SR * 0.07f)];
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            s[i] = Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 70f) * 0.6f;
        }
        return Make("click", s);
    }

    // Sirena de alarma: onda cuadrada con barrido ascendente y vibrato rapido.
    static AudioClip MakeAlert()
    {
        float dur = 0.9f; var s = new float[(int)(SR * dur)]; float ph = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float f = 600f + 700f * (t / dur) + 50f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            ph += f / SR; if (ph > 1f) ph -= 1f;
            float sq = ph < 0.5f ? 1f : -1f;
            float env = Mathf.Min(1f, t * 80f) * Mathf.Exp(-t * 1.5f);
            s[i] = sq * env * 0.22f;
        }
        return Make("alert", s);
    }

    // Victoria: arpegio mayor ascendente con armonicos y cola.
    static AudioClip MakeWin()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        var s = new float[(int)(SR * 1.6f)];
        for (int n = 0; n < notes.Length; n++)
        {
            int start = (int)(SR * 0.16f * n);
            for (int i = start; i < s.Length; i++)
            {
                float t = (i - start) / (float)SR;
                float e = Mathf.Exp(-t * 3.5f);
                if (e < 0.001f) break;
                float f = notes[n];
                s[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f * t)) * e * 0.22f;
            }
        }
        return Make("win", s);
    }

    // Derrota: barrido descendente grave con sierra desafinada + golpe de ruido.
    static AudioClip MakeLose()
    {
        float dur = 1.6f; var s = new float[(int)(SR * dur)]; float p1 = 0, p2 = 0, lp = 0;
        var r = new System.Random(7);
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            float f = Mathf.Lerp(330f, 45f, Mathf.Pow(t / dur, 0.6f));
            p1 += f / SR; if (p1 > 1f) p1 -= 1f;
            p2 += f * 1.015f / SR; if (p2 > 1f) p2 -= 1f;
            float saw = (2f * p1 - 1f + 2f * p2 - 1f) * 0.5f;
            lp += 0.08f * (saw - lp);
            float n = (float)(r.NextDouble() * 2 - 1) * Mathf.Exp(-t * 25f) * 0.5f;
            float env = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 1.6f);
            s[i] = (lp * 0.9f + n) * env;
        }
        return Make("lose", s);
    }

    static AudioClip MakePickup()
    {
        float dur = 0.22f; var s = new float[(int)(SR * dur)]; float ph = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR;
            ph += Mathf.Lerp(700f, 1500f, t / dur) / SR;
            s[i] = Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Exp(-t * 9f) * 0.5f;
        }
        return Make("pickup", s);
    }

    // Crujido de madera/puerta: tren de pulsos con tono que se desliza, jitter aleatorio y filtro.
    static AudioClip MakeCreak(int variation)
    {
        var r = new System.Random(variation * 7919 + 13);
        float dur = 0.7f + (float)r.NextDouble() * 0.7f;
        float f0 = 60f + (float)r.NextDouble() * 70f;
        float depth = 0.4f + (float)r.NextDouble() * 0.6f;
        var s = new float[(int)(SR * dur)];
        float ph = 0, lp = 0, jit = 0, nz = 0;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)SR, u = t / dur;
            jit += 0.02f * (float)((r.NextDouble() * 2 - 1) - jit); // jitter lento
            float f = f0 * (1f + depth * Mathf.Sin(Mathf.PI * u) + 0.25f * jit * 8f);
            ph += f / SR; if (ph > 1f) ph -= 1f;
            float pulse = ph < 0.18f ? 1f : -0.22f;                   // pulso estrecho = "stick-slip"
            lp += 0.14f * (pulse - lp);
            nz += 0.3f * ((float)(r.NextDouble() * 2 - 1) - nz);
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 1.4f);
            s[i] = (lp * 0.55f + nz * 0.08f) * env;
        }
        return Make("creak" + variation, s);
    }
}
