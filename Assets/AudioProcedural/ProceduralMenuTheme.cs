using UnityEngine;

/// <summary>
/// Fondo sonoro del MENU PRINCIPAL, generado en tiempo real (nunca se repite igual).
/// Capas:
///  - Drone: 4 sierras desafinadas (La1 y Mi2) por un pasa-bajos SVF cuyo corte "respira" con un LFO de 12 s.
///  - Melodia de caja de musica: generacion algoritmica (random walk sobre La menor armonica), notas cada
///    0.9-1.7 s con 20 % de silencios y a veces una octava abajo; mismo timbre que el hover del menu.
///    Pasa por un eco (delay de 0.42 s con realimentacion).
///  - Viento: ruido por pasa-banda que se mueve lentamente.
///  - Trueno lejano: rumor grave cada 18-35 s.
/// Entra con un fundido de 3 s. Va en un objeto con AudioSource 2D en la escena MainMenu
/// (lo crea "Audio Procedural -> Crear todo").
/// </summary>
public class ProceduralMenuTheme : ProceduralVoice
{
    public float gain = 0.3f;
    public float fadeInSeconds = 3f;

    static readonly float[] Scale = { 440f, 493.88f, 523.25f, 587.33f, 659.25f, 698.46f, 830.61f, 880f };
    static readonly float[] DroneHz = { 55f, 55.3f, 82.41f, 82f };
    static readonly int[] Steps = { -2, -1, -1, 1, 1, 2 };

    uint seed = 2463534242u;
    long n;
    readonly float[] dph = new float[4];
    float dlo, dband, wlo, wband;
    int idx = 4, nextNote = -1, nextBoom = -1;
    float nph, nf, nt = 99f, namp;
    float[] delay; int dp;
    float bt = 99f, blo, blo2;

    float R01()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 16777215f;
    }

    protected override void Awake()
    {
        // preparar estado antes de que arranque la reproduccion (base.Awake llama a Play)
        int sr = AudioSettings.outputSampleRate;
        delay = new float[(int)(sr * 0.42f)];
        nextNote = sr;           // primera nota al segundo
        nextBoom = sr * 12;      // primer trueno a los 12 s
        seed = (uint)Random.Range(1, int.MaxValue);
        base.Awake();
    }

    protected override void Generate(float[] d)
    {
        if (delay == null || sampleRate <= 0) { System.Array.Clear(d, 0, d.Length); return; }
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            n++;
            float t = n * inv, o = 0f;

            // --- drone ---
            float saw = 0f;
            for (int k = 0; k < 4; k++)
            {
                dph[k] += DroneHz[k] * inv; if (dph[k] > 1f) dph[k] -= 1f;
                saw += 2f * dph[k] - 1f;
            }
            float fc = 300f + 250f * (0.5f + 0.5f * Mathf.Sin(tp * t / 12f));
            float g = 2f * Mathf.Sin(Mathf.PI * fc * inv);
            float h = saw / 4f - dlo - 0.8f * dband; dband += g * h; dlo += g * dband;
            o += dlo * 0.28f;

            // --- melodia de caja de musica ---
            if (--nextNote <= 0)
            {
                nextNote = (int)(sampleRate * (0.9f + 0.8f * R01()));
                if (R01() > 0.2f)
                {
                    int step = Steps[(int)(R01() * 5.999f)];
                    idx = Mathf.Clamp(idx + step, 0, Scale.Length - 1);
                    nf = Scale[idx] * (R01() < 0.25f ? 0.5f : 1f);
                    nt = 0f; namp = 0.6f + 0.4f * R01();
                }
            }
            float note = 0f;
            if (nt < 4f)
            {
                nt += inv;
                nph += nf * inv; if (nph > 1f) nph -= 1f;
                float env = Mathf.Min(1f, nt / 0.002f) * Mathf.Exp(-nt * 3.5f) * namp;
                float p = tp * nph;
                note = (Mathf.Sin(p) + 0.3f * Mathf.Sin(2f * p) + 0.1f * Mathf.Sin(3f * p)) * env * 0.4f;
            }
            float echo = delay[dp];
            delay[dp] = note + echo * 0.4f;
            dp = (dp + 1) % delay.Length;
            o += note + echo * 0.35f;

            // --- viento ---
            float x = R01() * 2f - 1f;
            float wfc = 500f + 300f * Mathf.Sin(tp * t / 7f);
            float wg = 2f * Mathf.Sin(Mathf.PI * wfc * inv);
            float wh = x - wlo - wband; wband += wg * wh; wlo += wg * wband;
            o += wband * 0.06f * (0.6f + 0.4f * Mathf.Sin(tp * t / 9f));

            // --- trueno lejano ---
            if (--nextBoom <= 0) { nextBoom = (int)(sampleRate * (18f + 17f * R01())); bt = 0f; }
            if (bt < 4f)
            {
                bt += inv;
                blo += 0.02f * ((R01() * 2f - 1f) - blo);
                blo2 += 0.05f * (blo - blo2);
                o += blo2 * Mathf.Min(1f, bt / 0.3f) * Mathf.Exp(-bt * 1.2f) * 4f;
            }

            float fade = fadeInSeconds > 0f ? Mathf.Min(1f, t / fadeInSeconds) : 1f;
            d[i] = (float)System.Math.Tanh(o * gain * fade * 1.5f);
        }
    }
}

/// <summary>
/// Autoinstalacion: cada vez que se carga una escena que tiene el menu principal (StealthGame.MainMenu),
/// se asegura de que existan el fondo sonoro y los sonidos de botones, aunque no se haya usado "Crear todo".
/// </summary>
static class ProceduralMenuBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        Ensure();   // la escena inicial ya esta cargada cuando corre esto
    }

    static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => Ensure();

    static void Ensure()
    {
        var menu = Object.FindAnyObjectByType<StealthGame.MainMenu>();
        if (menu == null) return;

        if (menu.GetComponent<ProceduralMenuAudio>() == null && menu.GetComponent<UnityEngine.UIElements.UIDocument>() != null)
            menu.gameObject.AddComponent<ProceduralMenuAudio>();

        if (Object.FindAnyObjectByType<ProceduralMenuTheme>() == null)
        {
            var go = new GameObject("Menu Theme (Procedural)");
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 0f;
            src.volume = 1f;
            go.AddComponent<ProceduralMenuTheme>();
        }
        Debug.Log("[Audio Procedural] Menu principal: fondo sonoro y sonidos de botones activos.");
    }
}
