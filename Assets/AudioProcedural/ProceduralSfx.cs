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
    public static void Click() => Play2D(Get("click", MakeClick), 1f, 1f);
    public static void Alert() => Play2D(Get("alert", MakeAlert), 0.7f, 1f);
    public static void Win() => Play2D(Get("win", MakeWin), 0.8f, 1f);
    public static void Lose() => Play2D(Get("lose", MakeLose), 0.9f, 1f);
    public static void Pickup(Vector3 pos) => PlayAt(Get("pickup", MakePickup), pos, 0.8f, Random.Range(0.95f, 1.05f));
    public static void Creak(Vector3 pos)
    {
        int v = Random.Range(0, CreakVariations);
        PlayAt(Get("creak" + v, () => MakeCreak(v)), pos, Random.Range(0.5f, 0.9f), Random.Range(0.8f, 1.25f));
    }

    // --- Interfaz ---
    public static void Hover() => Play2D(Get("hover", MakeHover), 0.9f, Random.Range(0.97f, 1.03f));

    // --- Interaccion (llave y puertas) ---
    /// <summary>Llave: sintesis modal (parciales inarmonicos de metal) con dos golpes = tintineo.</summary>
    public static void KeyPickup(Vector3 pos) => PlayAt(Get("key", MakeKeyJingle), pos, 0.9f, Random.Range(0.96f, 1.04f));
    /// <summary>Puerta que se abre: manija, pestillo y chirrido de bisagra (stick-slip + resonadores).</summary>
    public static void DoorOpen(Vector3 pos) => PlayAt(Get("doorOpen", MakeDoorOpen), pos, 1f, Random.Range(0.92f, 1.06f));
    /// <summary>Puerta cerrada: un "toc" de nudillo sobre madera (sintesis modal), 3 variaciones.</summary>
    public static void DoorLocked(Vector3 pos)
    {
        int v = Random.Range(0, 3);
        PlayAt(Get("doorLocked" + v, () => MakeDoorLocked(v)), pos, 0.9f, Random.Range(0.93f, 1.07f));
    }

    // --- Eventos ---
    /// <summary>El jugador entra en el campo de vision de un enemigo: "sting" de cluster disonante con crescendo.</summary>
    public static void Spotted(Vector3 pos) => PlayAt(Get("spotted", MakeSpotted), pos, 0.8f, Random.Range(0.97f, 1.03f));

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
        a.rolloffMode = AudioRolloffMode.Linear;
        a.Play();
        // Los sonidos 2D (UI, victoria/derrota) sobreviven al cambio de escena para no cortarse
        // (p. ej. el click de "Start" del menu o la derrota que reinicia el nivel).
        if (spatial <= 0f) Object.DontDestroyOnLoad(go);
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

    // DERROTA (atrapado): golpe de impacto + "stab" disonante de sierras desafinadas con filtro que se cierra
    // + chillido ascendente + reverberacion Schroeder (4 combs + 2 all-pass) para una cola enorme.
    static AudioClip MakeLose()
    {
        var r = new System.Random(7);
        float dur = 2.6f; int n = (int)(SR * dur), total = n + (int)(SR * 1.5f);
        var s = new float[total];
        var w = new float[n];
        for (int i = 0; i < n; i++) w[i] = (float)(r.NextDouble() * 2 - 1);

        // stab: 6 notas (cluster con tritono) x 3 voces desafinadas
        float[] notes = { 55f, 58.27f, 82.41f, 110f, 116.54f, 155.56f };
        float[] det = { -0.006f, 0f, 0.007f };
        var ph = new float[notes.Length * det.Length];
        for (int k = 0; k < ph.Length; k++) ph[k] = (float)r.NextDouble();

        float subPh = 0f, bodyLp = 0f, lo = 0f, band = 0f, prevOut = 0f, scPh = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            float x = 0f;

            // impacto: seno con caida rapida de tono (138 -> 48 Hz)
            float f = 48f + 90f * Mathf.Exp(-t * 9f);
            subPh += f / SR; if (subPh > 1f) subPh -= 1f;
            x += Mathf.Sin(2f * Mathf.PI * subPh) * Mathf.Min(1f, t * 500f) * Mathf.Exp(-t * 3.5f) * 0.45f;

            // golpe (ruido grave) + crack (ruido diferenciado)
            if (t < 0.5f) { bodyLp += 0.25f * (w[i] - bodyLp); x += bodyLp * Mathf.Exp(-t * 9f) * 2.5f; }
            x += (w[i] - (i > 0 ? w[i - 1] : 0f)) * Mathf.Exp(-t * 60f) * 0.5f;

            // stab disonante
            float stab = 0f; int idx = 0;
            for (int a = 0; a < notes.Length; a++)
                for (int b = 0; b < det.Length; b++, idx++)
                {
                    ph[idx] += notes[a] * (1f + det[b]) / SR; if (ph[idx] > 1f) ph[idx] -= 1f;
                    stab += 2f * ph[idx] - 1f;
                }
            stab /= ph.Length;
            float fc = 600f + 3400f * Mathf.Exp(-t * 2.5f);
            float g = 2f * Mathf.Sin(Mathf.PI * fc / SR);
            float h = stab - lo - 0.7f * band; band += g * h; lo += g * band;
            x += (lo - 0.9f * prevOut) * Mathf.Min(1f, t * 200f) * Mathf.Exp(-t * 1.1f) * 4f;
            prevOut = lo;

            // chillido ascendente con vibrato
            float fs = 700f + 2600f * Mathf.Pow(Mathf.Min(1f, t / 0.9f), 0.8f);
            scPh += fs * (1f + 0.02f * Mathf.Sin(2f * Mathf.PI * 7f * t)) / SR; if (scPh > 1f) scPh -= 1f;
            float sn = Mathf.Sin(2f * Mathf.PI * scPh);
            x += Mathf.Sign(sn) * Mathf.Sqrt(Mathf.Abs(sn)) * Mathf.Min(1f, t * 30f) * Mathf.Exp(-t * 2.8f) * 0.30f;

            s[i] = x;
        }

        Reverb(s, 0.35f);
        Normalize(s, 1f);
        for (int i = 0; i < s.Length; i++) s[i] = (float)System.Math.Tanh(s[i] * 1.6f);
        Normalize(s, 0.95f);
        return Make("lose", s);
    }

    // Reverberacion de Schroeder in-place (el arreglo debe incluir espacio para la cola).
    static void Reverb(float[] x, float wet)
    {
        int[] combs = { 1557, 1617, 1491, 1422 };
        int[] aps = { 225, 556 };
        var acc = new float[x.Length];
        foreach (int d in combs)
        {
            var buf = new float[d]; int p = 0; float lp = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float y = buf[p];
                lp = y * 0.75f + lp * 0.25f;          // amortiguacion de agudos en la cola
                buf[p] = x[i] + lp * 0.84f;
                p = (p + 1) % d;
                acc[i] += y;
            }
        }
        foreach (int d in aps)
        {
            var buf = new float[d]; int p = 0;
            for (int i = 0; i < acc.Length; i++)
            {
                float b = buf[p];
                float y = -acc[i] + b;
                buf[p] = acc[i] + b * 0.5f;
                p = (p + 1) % d;
                acc[i] = y;
            }
        }
        for (int i = 0; i < x.Length; i++) x[i] = x[i] * (1f - wet) + acc[i] * wet * 0.25f;
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

    // ---------- Nuevos efectos ----------

    // Sintesis modal: suma de modos (frecuencia, amplitud, decaimiento) excitados por un impulso.
    // Los cocientes inarmonicos (1, 2.76, 5.40, 8.93) son los de una barra metalica libre.
    static void AddModalStrike(float[] s, int start, float f0, float amp, System.Random r)
    {
        float[] ratio = { 1f, 2.76f, 5.40f, 8.93f, 13.34f };
        float[] gain = { 1f, 0.6f, 0.45f, 0.3f, 0.15f };
        float[] decay = { 9f, 14f, 22f, 30f, 45f };
        for (int m = 0; m < ratio.Length; m++)
        {
            float f = f0 * ratio[m] * (1f + 0.01f * (float)(r.NextDouble() * 2 - 1));
            if (f > SR * 0.45f) continue;
            float ph0 = (float)r.NextDouble();
            for (int i = start; i < s.Length; i++)
            {
                float t = (i - start) / (float)SR;
                float e = Mathf.Exp(-t * decay[m]);
                if (e < 0.0005f) break;
                s[i] += Mathf.Sin(2f * Mathf.PI * (f * t + ph0)) * e * gain[m] * amp * 0.18f;
            }
        }
        // transitorio de contacto: clic de ruido muy corto
        for (int i = start; i < Mathf.Min(s.Length, start + SR / 200); i++)
            s[i] += (float)(r.NextDouble() * 2 - 1) * amp * 0.25f * (1f - (i - start) / (SR / 200f));
    }

    // =====================================================================
    //  EFECTOS DE TERROR (todos con eco de casa vacia: reverberacion Schroeder)
    // =====================================================================

    static float[] Tail(float[] s, float seconds)
    {
        var o = new float[s.Length + (int)(SR * seconds)];
        System.Array.Copy(s, o, s.Length);
        return o;
    }

    // Filtro de estado variable (Chamberlin). mode 0 = pasa-banda, 1 = pasa-bajos. fc por muestra opcional.
    static float[] Svf(float[] x, System.Func<int, float> fc, float damp, int mode)
    {
        var y = new float[x.Length]; float lo = 0f, band = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float g = 2f * Mathf.Sin(Mathf.PI * fc(i) / SR);
            float h = x[i] - lo - damp * band; band += g * h; lo += g * band;
            y[i] = mode == 0 ? band : lo;
        }
        return y;
    }

    static float[] NoiseArr(int n, System.Random r)
    {
        var a = new float[n];
        for (int i = 0; i < n; i++) a[i] = (float)(r.NextDouble() * 2 - 1);
        return a;
    }

    static float[] Finish(float[] s, float tailSec, float wet, float peak)
    {
        var o = Tail(s, tailSec);
        Reverb(o, wet);
        Normalize(o, peak);
        return o;
    }

    // HOVER: nota corta y suave, tipo caja de musica (Mi5 con 2 armonicos), con un poco de eco.
    // Es breve para que no moleste al recorrer los botones rapido; el pitch varia un 3 % en cada uso.
    static AudioClip MakeHover()
    {
        int n = (int)(SR * 0.15f);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, att = Mathf.Min(1f, t / 0.002f);
            s[i] = (Mathf.Sin(2f * Mathf.PI * 659.25f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 1318.5f * t)
                  + 0.1f * Mathf.Sin(2f * Mathf.PI * 1977.75f * t)) * Mathf.Exp(-t * 25f) * att;
        }
        return Make("hover", Drive(Finish(s, 0.3f, 0.2f, 1f), 2.2f, 0.85f));
    }

    // CLICK (confirmar): golpe suave grave + intervalo de tercera menor (La3 + Do4) tipo campana oscura
    // + un brillo agudo corto que le da definicion. Claro como "confirmado", pero sombrio.
    static AudioClip MakeClick()
    {
        int n = (int)(SR * 0.6f);
        var s = new float[n];
        float[] notes = { 220f, 261.63f };
        float[] p = { 1f, 2f, 3.01f }, d = { 0.5f, 0.3f, 0.2f }, g = { 1f, 0.4f, 0.2f };
        float ph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, att = Mathf.Min(1f, t / 0.002f);
            ph += (100f + 40f * Mathf.Exp(-t * 40f)) / SR;
            float x = Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Exp(-t / 0.05f) * 0.8f;
            for (int a = 0; a < notes.Length; a++)
                for (int k = 0; k < p.Length; k++)
                    x += Mathf.Sin(2f * Mathf.PI * notes[a] * p[k] * t) * Mathf.Exp(-t / d[k]) * g[k] * 0.5f;
            x += Mathf.Sin(2f * Mathf.PI * 1760f * t) * Mathf.Exp(-t / 0.08f) * 0.3f;
            s[i] = x * att;
        }
        return Make("click", Drive(Finish(s, 0.6f, 0.25f, 1f), 2.5f, 0.95f));
    }

    // ALERTA (tension alta): latido "lub-dub" + riser de ruido + tremolo de cuerdas disonantes que crece.
    static AudioClip MakeAlert()
    {
        var r = new System.Random(5);
        int n = (int)(SR * 1.4f);
        var s = new float[n];
        float[] beatT = { 0f, 0.28f }, beatA = { 1f, 0.7f };
        for (int b = 0; b < 2; b++)
        {
            int k = (int)(beatT[b] * SR); float ph = 0f;
            for (int i = k; i < n; i++)
            {
                float tt = (i - k) / (float)SR;
                ph += (80f + 60f * Mathf.Exp(-tt * 30f)) / SR;
                s[i] += Mathf.Sin(2f * Mathf.PI * ph) * Mathf.Min(1f, tt * 300f) * Mathf.Exp(-tt * 14f) * beatA[b];
            }
        }
        System.Func<int, float> env = i =>
        {
            float t = i / (float)SR, u = Mathf.Min(1f, t / 1.3f);
            return u * u * (t < 1.3f ? 1f : Mathf.Exp(-(t - 1.3f) * 40f));
        };
        var riser = Svf(NoiseArr(n, r), i => 300f + 2200f * Mathf.Min(1f, i / (float)SR / 1.3f), 0.6f, 0);

        float[] cf = { 440f, 466.16f, 659.26f };
        var cl = new float[n];
        for (int c = 0; c < cf.Length; c++)
        {
            float ph = (float)r.NextDouble();
            for (int i = 0; i < n; i++) { ph += cf[c] / SR; if (ph > 1f) ph -= 1f; cl[i] += (2f * ph - 1f) / 3f; }
        }
        cl = Svf(cl, i => 2500f, 0.9f, 1);

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, e = env(i);
            float trem = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            s[i] += riser[i] * e * 0.5f + cl[i] * trem * e * 0.5f;
        }
        return Make("alert", Finish(s, 0.6f, 0.3f, 0.75f));
    }

    // DETECTADO: chirrido de cuerdas agudas en cluster (estilo "Psicosis") con vibrato, ruido de arco
    // y un golpe grave, con eco largo.
    static AudioClip MakeSpotted()
    {
        var r = new System.Random(9);
        int n = (int)(SR * 1.4f);
        var s = new float[n];
        float[] fr = { 1046.5f, 1108.73f, 1174.66f, 523.25f, 554.37f };
        for (int v = 0; v < fr.Length; v++)
        {
            float rate = 6.5f + (float)r.NextDouble(), off = (float)r.NextDouble() * 6.28f, ph = (float)r.NextDouble();
            float gain = fr[v] < 1000f ? 0.6f : 1f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                ph += fr[v] * (1f + 0.015f * Mathf.Sin(2f * Mathf.PI * rate * t + off)) / SR;
                ph -= Mathf.Floor(ph);
                s[i] += (2f * ph - 1f) * gain / 5f;
            }
        }
        s = Svf(s, i => 5000f, 0.8f, 1);
        var bow = Svf(NoiseArr(n, r), i => 3000f, 1.2f, 0);
        float bph = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            s[i] = (s[i] + bow[i] * 0.15f) * Mathf.Min(1f, t / 0.01f) * Mathf.Exp(-t * 1.8f);
            bph += (60f + 50f * Mathf.Exp(-t * 8f)) / SR;
            s[i] += Mathf.Sin(2f * Mathf.PI * bph) * Mathf.Min(1f, t * 400f) * Mathf.Exp(-t * 5f) * 0.2f;
        }
        return Make("spotted", Finish(s, 1.2f, 0.35f, 0.85f));
    }

    // VICTORIA: no es alegre; una campana lejana que dobla dos veces y un acorde de alivio que aparece despacio.
    static AudioClip MakeWin()
    {
        var r = new System.Random(21);
        int n = (int)(SR * 4.2f);
        var s = new float[n];
        float[] rat = { 0.5f, 1f, 1.2f, 1.5f, 2f, 2.5f, 3f, 4.2f };
        float[] gn = { 0.6f, 1f, 0.6f, 0.5f, 0.4f, 0.25f, 0.2f, 0.1f };
        float[] dc = { 3f, 2.5f, 2f, 1.6f, 1.2f, 0.9f, 0.7f, 0.4f };
        float[] tollT = { 0f, 1.6f }, tollA = { 1f, 0.75f };
        for (int b = 0; b < 2; b++)
        {
            int k = (int)(tollT[b] * SR);
            for (int m = 0; m < rat.Length; m++)
            {
                float ph = (float)r.NextDouble();
                for (int i = k; i < n; i++)
                {
                    float tt = (i - k) / (float)SR;
                    s[i] += gn[m] * Mathf.Exp(-tt / dc[m]) * Mathf.Sin(2f * Mathf.PI * 220f * rat[m] * tt + ph)
                          * Mathf.Min(1f, tt / 0.002f) * tollA[b] * 0.3f;
                }
            }
        }
        float[] pad = { 110f, 220f, 277.18f, 329.63f };
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, p = 0f;
            for (int c = 0; c < pad.Length; c++)
                p += Mathf.Sin(2f * Mathf.PI * pad[c] * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * pad[c] * 2.003f * t);
            s[i] += p * Mathf.Min(1f, t / 1.2f) * Mathf.Min(1f, (4.2f - t) / 1f) * 0.08f;
        }
        return Make("win", Finish(s, 1.5f, 0.4f, 0.8f));
    }

    // LLAVE: llave vieja (modal mas grave) + brillo disonante (segunda menor) que se desvanece, con eco.
    static AudioClip MakeKeyJingle()
    {
        var r = new System.Random(42);
        int n = (int)(SR * 1.4f);
        var s = new float[n];
        AddModalStrike(s, 0, 900f, 1f, r);
        AddModalStrike(s, (int)(SR * 0.09f), 1150f, 0.6f, r);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR, e = Mathf.Min(1f, t / 0.3f) * Mathf.Exp(-t / 0.6f) * 0.08f;
            s[i] += (Mathf.Sin(2f * Mathf.PI * 1760f * t) + Mathf.Sin(2f * Mathf.PI * 1864.66f * t)) * e;
        }
        return Make("key", Finish(s, 1f, 0.35f, 0.8f));
    }

    // PUERTA QUE SE ABRE (puerta embrujada): manija + pestillo -> bisagra que chirria LENTO (~2.7 s):
    // pulsos de friccion stick-slip irregulares -> banco de resonadores del panel de madera -> aire -> tope. Con eco.
    static AudioClip MakeDoorOpen()
    {
        var r = new System.Random(11);
        int n = (int)(SR * 3.4f);
        var s = new float[n];
        AddModalStrike(s, 0, 1800f, 0.45f, r);                 // manija
        AddModalStrike(s, (int)(SR * 0.15f), 1100f, 0.8f, r);  // pestillo

        var exc = new float[n];
        int c0 = (int)(SR * 0.3f), c1 = (int)(SR * 3.0f);
        double pos = c0; float drift = 0f;
        while (pos < c1)
        {
            float u = (float)((pos - c0) / (c1 - c0));
            float rate = 14f + 30f * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f) + 10f * Mathf.Sin(2f * Mathf.PI * 1.1f * u);
            drift += 0.15f * ((float)(r.NextDouble() * 2 - 1) - drift);
            rate *= 1f + 0.25f * drift;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.6f) * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1.7f * u + 1f));
            exc[(int)pos] += env * (0.6f + 0.4f * (float)r.NextDouble());
            pos += SR / Mathf.Max(8f, rate) * (1f + 0.12f * (float)(r.NextDouble() * 2 - 1));
        }
        float[] mf = { 260f, 470f, 760f, 1190f, 1850f };
        float[] md = { 0.035f, 0.03f, 0.03f, 0.025f, 0.025f };
        for (int m = 0; m < mf.Length; m++)
        {
            float f = mf[m];
            var y = Svf(exc, i => f, md[m], 0);
            float gain = (f < 1000f ? 1f : 0.6f) * 0.25f;
            for (int i = 0; i < n; i++) s[i] += y[i] * gain;
        }
        var air = Svf(NoiseArr(n, r), i => 450f, 1.2f, 0);
        for (int i = 0; i < n; i++)
        {
            float u = Mathf.Clamp01((i / (float)SR - 0.3f) / 2.7f);
            s[i] += air[i] * Mathf.Sin(Mathf.PI * u) * 0.12f;
        }
        int k = (int)(SR * 3.05f);
        for (int i = k; i < n; i++)
        {
            float tt = (i - k) / (float)SR;
            s[i] += Mathf.Sin(2f * Mathf.PI * 120f * tt) * Mathf.Exp(-tt * 25f) * 0.25f;
        }
        return Make("doorOpen", Finish(s, 1.2f, 0.3f, 0.85f));
    }

    // PUERTA CERRADA = "TOC" grave y hueco (puerta vieja y pesada), sintesis modal + eco. 3 variaciones.
    static AudioClip MakeDoorLocked(int variation)
    {
        var r = new System.Random(100 + variation * 31);
        int n = (int)(SR * 0.35f);
        var s = new float[n];
        float k = (0.95f + 0.1f * (float)r.NextDouble()) * 0.85f;
        float[] f = { 160f, 340f, 580f, 920f, 1400f, 2300f };
        float[] d = { 0.09f, 0.06f, 0.045f, 0.03f, 0.02f, 0.012f };
        float[] gn = { 1f, 0.8f, 0.6f, 0.45f, 0.3f, 0.15f };
        for (int m = 0; m < f.Length; m++)
        {
            float ph = (float)r.NextDouble();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                s[i] += gn[m] * Mathf.Exp(-t / d[m]) * Mathf.Sin(2f * Mathf.PI * f[m] * k * t + ph) * Mathf.Min(1f, t / 0.0008f);
            }
        }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)SR;
            s[i] += Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t / 0.06f) * 0.6f * Mathf.Min(1f, t / 0.0008f);
        }
        for (int i = 0; i < (int)(SR * 0.003f); i++) s[i] += (float)(r.NextDouble() * 2 - 1) * 0.4f;
        return Make("doorLocked" + variation, Finish(s, 0.8f, 0.3f, 0.8f));
    }

    // Saturacion suave (tanh) + normalizacion: sube el volumen percibido sin recortar en seco.
    static float[] Drive(float[] s, float drive, float peak)
    {
        for (int i = 0; i < s.Length; i++) s[i] = (float)System.Math.Tanh(s[i] * drive);
        Normalize(s, peak);
        return s;
    }

    static void Normalize(float[] s, float peak)
    {
        float m = 0.0001f;
        for (int i = 0; i < s.Length; i++) m = Mathf.Max(m, Mathf.Abs(s[i]));
        float g = peak / m;
        for (int i = 0; i < s.Length; i++) s[i] *= g;
    }
}
