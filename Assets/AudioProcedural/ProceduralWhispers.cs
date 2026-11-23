using UnityEngine;

/// <summary>
/// Susurros sobrenaturales sintetizados (sin archivos) por SINTESIS DE FORMANTES:
///  ruido blanco -> dos filtros pasa-banda (SVF) en paralelo = formantes F1/F2 de una "voz" sin tono.
///  Los formantes se deslizan entre vocales elegidas al azar (a, e, i, o, u) y la amplitud se trocea
///  en "silabas" de duracion aleatoria, asi cada frase es distinta (generacion algoritmica).
///
/// Adaptacion dinamica:
///  - 'presence' (0..1) la fija TensionManager: con mas tension hay frases mas frecuentes, largas y fuertes.
///  - En cada frase el objeto se reubica en un punto aleatorio alrededor del jugador (AudioSource 3D),
///    de modo que los susurros "rodean" al jugador y aparecen por la izquierda, derecha o detras.
/// </summary>
public class ProceduralWhispers : ProceduralVoice
{
    [Range(0f, 1f)] public float presence = 0.2f;
    public float gain = 0.5f;
    public Vector2 pauseSeconds = new Vector2(4f, 14f);   // pausa entre frases con presence = 0
    public Vector2 orbitRadius = new Vector2(2.5f, 6f);

    // Vocales: (F1, F2) en Hz, valores aproximados de voz susurrada
    static readonly float[] F1 = { 800f, 500f, 300f, 500f, 320f };
    static readonly float[] F2 = { 1250f, 1800f, 2300f, 900f, 800f };

    // --- Comunicacion hilo principal -> hilo de audio ---
    volatile bool startPhrase;
    volatile int phraseSyllables;
    volatile float phraseLevel;

    // --- Estado del hilo de audio ---
    uint seed = 521288629u;
    int syllablesLeft, sampLeft;
    float env, envTarget, f1, f2, f1T, f2T;
    float l1, b1, l2, b2, hiss;

    Transform listener;
    float nextPhrase;

    float Rand01()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 16777215f;
    }

    void Reset() { Configure3D(1f, 10f, 1f); }

    void Start()
    {
        var l = FindAnyObjectByType<AudioListener>();
        if (l != null) listener = l.transform;
        nextPhrase = Time.time + Random.Range(2f, 5f);
    }

    void Update()
    {
        if (Time.time < nextPhrase || presence <= 0.001f) return;

        // Mas presencia => pausas mas cortas
        float k = Mathf.Lerp(1f, 0.25f, presence);
        nextPhrase = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y) * k;

        if (listener == null)
        {
            var l = FindAnyObjectByType<AudioListener>();
            if (l == null) return;
            listener = l.transform;
        }

        // Reubicar la fuente 3D alrededor del jugador
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(orbitRadius.x, orbitRadius.y);
        transform.position = listener.position + new Vector3(c.x, Random.Range(0.5f, 1.8f), c.y);

        phraseSyllables = Random.Range(2, 4 + Mathf.RoundToInt(presence * 6f));
        phraseLevel = Mathf.Lerp(0.35f, 1f, presence) * Random.Range(0.8f, 1.1f);
        startPhrase = true;
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate;
        for (int i = 0; i < d.Length; i++)
        {
            if (startPhrase)
            {
                startPhrase = false;
                syllablesLeft = phraseSyllables;
                sampLeft = 0;
            }

            // Secuenciador de silabas: cada silaba = vocal nueva + duracion + hueco aleatorios
            if (--sampLeft <= 0)
            {
                if (syllablesLeft > 0)
                {
                    syllablesLeft--;
                    int v = (int)(Rand01() * 4.999f);
                    f1T = F1[v] * (0.9f + 0.2f * Rand01());
                    f2T = F2[v] * (0.9f + 0.2f * Rand01());
                    // silaba sonora o pequeno silencio (consonante)
                    envTarget = Rand01() < 0.8f ? phraseLevel * (0.6f + 0.4f * Rand01()) : 0f;
                    sampLeft = (int)(sampleRate * (0.09f + 0.18f * Rand01()));
                }
                else
                {
                    envTarget = 0f;
                    sampLeft = sampleRate / 10;
                }
            }

            // Deslizamiento de formantes (coarticulacion) y envolvente suavizada
            f1 += (f1T - f1) * 0.0015f;
            f2 += (f2T - f2) * 0.0015f;
            env += (envTarget - env) * (envTarget > env ? 0.004f : 0.0012f);

            float x = Rand01() * 2f - 1f;

            // Formante 1 (SVF pasa-banda, Q alto)
            float g1 = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(f1, 4000f) * inv);
            float h1 = x - l1 - 0.18f * b1; b1 += g1 * h1; l1 += g1 * b1;
            // Formante 2
            float g2 = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(f2, 5000f) * inv);
            float h2 = x - l2 - 0.22f * b2; b2 += g2 * h2; l2 += g2 * b2;

            // Siseo agudo (fricativas "s"/"sh") = diferencia de ruido (pasa-altos simple)
            float sh = x - hiss; hiss = x;

            float s = (b1 * 0.5f + b2 * 0.35f + sh * 0.05f) * env;
            d[i] = Mathf.Clamp(s * gain, -1f, 1f);
        }
    }
}
