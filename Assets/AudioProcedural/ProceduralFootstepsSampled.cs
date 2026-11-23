using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PASOS EN MADERA NATURALES: grabacion real + procesamiento procedural (tecnica de "granular / sample slicing").
///
/// 1) ANALISIS (al iniciar): ecualiza (quita graves inaudibles, realza el 'toc') y toma una o varias grabaciones de pasos (p. ej. de Pixabay, o el
///    SFXFootstepsLooping.wav del proyecto) y DETECTA AUTOMATICAMENTE cada pisada (deteccion de onsets por
///    energia RMS en ventanas de 5 ms). Cada pisada se recorta, se le quita la cola en silencio y se le
///    aplican fundidos -> banco de "granos" de pasos.
/// 2) SINTESIS (en cada paso, hilo de audio): elige un grano al azar sin repetir los dos ultimos y lo
///    reproduce con remuestreo (pitch aleatorio), ganancia aleatoria, filtro pasa-bajos variable
///    (pisada mas o menos firme) y reflexiones tempranas de habitacion. Ningun paso suena dos veces igual.
/// 3) ADAPTACION: la intensidad del paso y la tension del juego (sigilo) bajan el volumen y oscurecen el
///    filtro; varias voces permiten que un paso se solape con la cola del anterior.
///
/// Hereda de ProceduralFootsteps: ProceduralFootstepDriver lo usa sin cambios.
/// IMPORTANTE: los clips deben tener Load Type = "Decompress On Load" (el valor por defecto) para poder leerlos.
/// </summary>
public class ProceduralFootstepsSampled : ProceduralFootsteps
{
    [Header("Grabaciones de pasos (madera)")]
    [Tooltip("Uno o varios archivos con pasos en madera (Pixabay, etc.). Pueden ser pasos sueltos o una caminata larga.")]
    public AudioClip[] recordings;

    [Header("Variacion procedural")]
    public float gain = 0.7f;
    public Vector2 pitchRange = new Vector2(0.93f, 1.07f);
    public Vector2 gainRange = new Vector2(0.75f, 1f);
    [Range(0f, 1f)] public float brightnessVariation = 0.55f;
    [Range(0f, 0.4f)] public float room = 0.15f;
    [Range(0f, 1f)] public float sneakWithTension = 0.3f;

    [Header("Ecualizacion (para que se oiga en parlantes de portatil/celular)")]
    [Tooltip("Pasa-altos: quita el retumbo grave que los parlantes pequenos no reproducen.")]
    public float lowCutHz = 150f;
    [Tooltip("Realce de presencia en 2.5 kHz: el 'toc' de la madera.")]
    [Range(0f, 12f)] public float presenceDb = 6f;

    [Header("Deteccion de pisadas")]
    [Range(0.05f, 0.6f)] public float onsetThreshold = 0.18f;
    public float minStepGap = 0.22f;
    public float maxStepLength = 0.55f;

    // Banco de granos (se publica completo una vez construido; el hilo de audio solo lo lee)
    volatile float[][] bank;
    volatile float[] bankRate;   // frecuencia de muestreo original / salida

    // Peticion de paso (hilo principal -> audio)
    volatile bool pending;
    volatile int pIdx;
    volatile float pRate, pGain, pLp;

    class Voice { public float[] buf; public double pos; public float rate, gain, lpc, lp; public bool on; }
    readonly Voice[] voices = { new Voice(), new Voice(), new Voice() };
    int nextVoice;

    float[] early; int earlyPos;
    int last1 = -1, last2 = -1;

    void Start()
    {
        early = new float[(int)(sampleRate * 0.05f)];
        BuildBank();
    }

    // ---------- Analisis: troceado automatico de las grabaciones ----------
    void BuildBank()
    {
        var grains = new List<float[]>();
        var rates = new List<float>();
        if (recordings != null)
        {
            foreach (var clip in recordings)
            {
                if (clip == null) continue;
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var raw = new float[clip.samples * clip.channels];
                if (!clip.GetData(raw, 0))
                {
                    Debug.LogWarning($"[Pasos] No se pudo leer '{clip.name}'. Ponle Load Type = Decompress On Load.");
                    continue;
                }
                var mono = new float[clip.samples];
                for (int i = 0; i < mono.Length; i++)
                {
                    float sum = 0f;
                    for (int c = 0; c < clip.channels; c++) sum += raw[i * clip.channels + c];
                    mono[i] = sum / clip.channels;
                }
                Equalize(mono, clip.frequency);
                int before = grains.Count;
                Slice(mono, clip.frequency, grains);
                for (int g = before; g < grains.Count; g++) rates.Add(clip.frequency / (float)sampleRate);
                Debug.Log($"[Pasos] '{clip.name}': {grains.Count - before} pisadas detectadas.");
            }
        }
        if (grains.Count == 0) Debug.LogWarning("[Pasos] No hay grabaciones asignadas en 'recordings'.");
        bankRate = rates.ToArray();
        bank = grains.ToArray();
    }

    // EQ con biquads (formulas RBJ): pasa-altos de 4o orden + campana de presencia
    void Equalize(float[] x, int sr)
    {
        if (lowCutHz > 0f)
        {
            Biquad(x, sr, lowCutHz, 0.707f, 0f, true);
            Biquad(x, sr, lowCutHz, 0.707f, 0f, true);
        }
        if (presenceDb > 0f) Biquad(x, sr, 2500f, 0.8f, presenceDb, false);
    }

    static void Biquad(float[] x, int sr, float f, float q, float gainDb, bool highPass)
    {
        float w = 2f * Mathf.PI * f / sr, cs = Mathf.Cos(w), al = Mathf.Sin(w) / (2f * q);
        float b0, b1, b2, a0, a1, a2;
        if (highPass)
        {
            b0 = (1f + cs) / 2f; b1 = -(1f + cs); b2 = b0;
            a0 = 1f + al; a1 = -2f * cs; a2 = 1f - al;
        }
        else
        {
            float A = Mathf.Pow(10f, gainDb / 40f);
            b0 = 1f + al * A; b1 = -2f * cs; b2 = 1f - al * A;
            a0 = 1f + al / A; a1 = -2f * cs; a2 = 1f - al / A;
        }
        b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
        for (int i = 0; i < x.Length; i++)
        {
            float y = b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x[i]; y2 = y1; y1 = y; x[i] = y;
        }
    }

    void Slice(float[] x, int sr, List<float[]> outGrains)
    {
        int hop = Mathf.Max(1, (int)(sr * 0.005f));
        int n = x.Length / hop;
        if (n < 4) return;
        var rms = new float[n];
        float peak = 1e-6f;
        for (int i = 0; i < n; i++)
        {
            float s = 0f;
            for (int j = i * hop; j < (i + 1) * hop; j++) s += x[j] * x[j];
            rms[i] = Mathf.Sqrt(s / hop);
            peak = Mathf.Max(peak, rms[i]);
        }

        // Onsets: energia sobre umbral y subida brusca respecto a los ~40 ms anteriores
        var onsets = new List<int>();
        int gap = (int)(minStepGap / 0.005f), lastOn = -9999;
        for (int i = 1; i < n; i++)
        {
            float avg = 1e-6f; int cnt = 0;
            for (int j = Mathf.Max(0, i - 8); j < i; j++) { avg += rms[j]; cnt++; }
            avg /= Mathf.Max(1, cnt);
            if (rms[i] > onsetThreshold * peak && rms[i] > avg * 1.8f && i - lastOn > gap)
            {
                onsets.Add(i); lastOn = i;
            }
        }
        if (onsets.Count == 0) onsets.Add(0); // archivo de un solo paso

        int pre = (int)(sr * 0.012f), maxLen = (int)(sr * maxStepLength);
        for (int k = 0; k < onsets.Count; k++)
        {
            int s0 = Mathf.Max(0, onsets[k] * hop - pre);
            int e0 = k + 1 < onsets.Count ? onsets[k + 1] * hop - pre : x.Length;
            e0 = Mathf.Min(e0, s0 + maxLen, x.Length);
            if (e0 - s0 < sr * 0.04f) continue;

            // quitar cola en silencio
            float p = 1e-6f;
            for (int j = s0; j < e0; j++) p = Mathf.Max(p, Mathf.Abs(x[j]));
            int end = e0;
            for (int j = e0 - 1; j > s0; j--) if (Mathf.Abs(x[j]) > p * 0.03f) { end = j; break; }
            end = Mathf.Min(e0, end + (int)(sr * 0.03f));

            int len = end - s0;
            var g = new float[len + 2];
            int fi = Mathf.Max(1, (int)(sr * 0.003f));
            int fo = Mathf.Max(1, Mathf.Min(len / 3, (int)(sr * 0.03f)));
            float norm = 1f / p;
            for (int j = 0; j < len; j++)
            {
                float f = 1f;
                if (j < fi) f = j / (float)fi;
                if (j > len - fo) f = Mathf.Min(f, (len - j) / (float)fo);
                g[j] = x[s0 + j] * f * norm;
            }
            outGrains.Add(g);
        }
    }

    // ---------- Disparo de paso (hilo principal) ----------
    public override void Step(float intensity = 1f, Surface surface = Surface.Wood)
    {
        var b = bank;
        if (b == null || b.Length == 0) return;

        int idx, tries = 0;
        do { idx = Random.Range(0, b.Length); }
        while (b.Length > 2 && (idx == last1 || idx == last2) && ++tries < 10);
        last2 = last1; last1 = idx;

        float t = TensionManager.Instance != null ? TensionManager.Instance.Tension : 0f;
        float k = Mathf.Clamp01(intensity) * (1f - sneakWithTension * t);

        pIdx = idx;
        pRate = bankRate[idx] * Random.Range(pitchRange.x, pitchRange.y);
        pGain = Random.Range(gainRange.x, gainRange.y) * Mathf.Lerp(0.6f, 1f, k);
        // pasa-bajos de 1 polo: 1 = sin filtrar; menor = pisada mas suave/oscura
        pLp = Mathf.Clamp(1f - brightnessVariation * Random.value * (1.2f - k), 0.15f, 1f);
        pending = true;
    }

    // ---------- Reproduccion (hilo de audio) ----------
    protected override void Generate(float[] d)
    {
        var b = bank;
        for (int i = 0; i < d.Length; i++)
        {
            if (pending && b != null && pIdx < b.Length)
            {
                pending = false;
                var v = voices[nextVoice];
                nextVoice = (nextVoice + 1) % voices.Length;
                v.buf = b[pIdx]; v.pos = 0; v.rate = pRate; v.gain = pGain; v.lpc = pLp; v.lp = 0f; v.on = true;
            }

            float s = 0f;
            for (int n = 0; n < voices.Length; n++)
            {
                var v = voices[n];
                if (!v.on) continue;
                int a = (int)v.pos;
                if (a >= v.buf.Length - 1) { v.on = false; continue; }
                float f = (float)(v.pos - a);
                float smp = v.buf[a] + (v.buf[a + 1] - v.buf[a]) * f;   // interpolacion lineal (cambio de pitch)
                v.lp += v.lpc * (smp - v.lp);
                s += v.lp * v.gain;
                v.pos += v.rate;
            }

            // reflexiones tempranas (habitacion de madera): 23 ms y 37 ms
            if (early != null && early.Length > 0)
            {
                int L = early.Length;
                int t1 = (earlyPos - (int)(sampleRate * 0.023f) + L) % L;
                int t2 = (earlyPos - (int)(sampleRate * 0.037f) + L) % L;
                float wet = early[t1] * room + early[t2] * room * 0.66f;
                early[earlyPos] = s;
                earlyPos = (earlyPos + 1) % L;
                s += wet;
            }

            d[i] = (float)System.Math.Tanh(s * gain);
        }
    }
}
