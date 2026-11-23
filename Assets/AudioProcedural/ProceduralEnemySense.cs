using UnityEngine;

/// <summary>
/// Va en el mismo objeto que el script Observer (PointOfView de gargolas y fantasmas).
/// Cuando el jugador entra en el cono de vision (el trigger del Observer):
///   1) dispara el "sting" de deteccion (ProceduralSfx.Spotted) en la posicion del enemigo,
///   2) avisa al TensionManager para que la musica salte al estado de peligro,
///   3) sube una capa continua de "piedra que vibra / respiracion" sintetizada aqui mismo.
///
/// Sintesis de la capa continua: ruido marron (granulado, como piedra rozando) modulado en amplitud
/// por un LFO lento + subarmonico de 41 Hz con batido. 'alertness' (0..1) sube mientras el jugador
/// esta dentro del rango y se apaga despacio al salir: el sonido depende del estado del juego.
/// </summary>
public class ProceduralEnemySense : ProceduralVoice
{
    public float gain = 0.6f;
    public float riseSpeed = 1.5f;   // por segundo
    public float fallSpeed = 0.4f;
    public float stingCooldown = 4f;

    Transform player;
    bool inRange;
    float alertnessMain, lastSting = -99f;
    volatile float alertness;

    uint seed = 3141592653u;
    float brown, lfoPh, subPh, subPh2, sm;

    float Noise()
    {
        seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
        return (seed & 0xFFFFFF) / 8388608f - 1f;
    }

    void Reset() { Configure3D(1f, 12f, 1f); }

    void Start()
    {
        var obs = GetComponent<StealthGame.Observer>();
        if (obs != null) player = obs.player;
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
        }
        seed = (uint)Random.Range(1, int.MaxValue);
    }

    void OnTriggerEnter(Collider other)
    {
        if (player == null || other.transform != player) return;
        inRange = true;
        if (Time.time - lastSting > stingCooldown)
        {
            lastSting = Time.time;
            ProceduralSfx.Spotted(transform.position);
        }
        if (TensionManager.Instance != null) TensionManager.Instance.ReportSighting(1f);
    }

    void OnTriggerExit(Collider other)
    {
        if (player != null && other.transform == player) inRange = false;
    }

    void Update()
    {
        alertnessMain = Mathf.MoveTowards(alertnessMain, inRange ? 1f : 0f,
                                          Time.deltaTime * (inRange ? riseSpeed : fallSpeed));
        alertness = alertnessMain;
        if (inRange && TensionManager.Instance != null) TensionManager.Instance.ReportSighting(0.9f);
    }

    protected override void Generate(float[] d)
    {
        float inv = 1f / sampleRate, tp = 2f * Mathf.PI;
        for (int i = 0; i < d.Length; i++)
        {
            sm += (alertness - sm) * 0.0005f;
            if (sm < 0.0005f) { d[i] = 0f; continue; }

            brown = Mathf.Clamp(brown + Noise() * 0.04f, -1f, 1f) * 0.999f; // ruido marron
            lfoPh += (0.6f + 2.5f * sm) * inv; if (lfoPh > 1f) lfoPh -= 1f;
            subPh += 41f * inv; if (subPh > 1f) subPh -= 1f;
            subPh2 += 41.7f * inv; if (subPh2 > 1f) subPh2 -= 1f;     // batido de 0.7 Hz

            float grind = brown * (0.5f + 0.5f * Mathf.Sin(tp * lfoPh));
            float sub = (Mathf.Sin(tp * subPh) + Mathf.Sin(tp * subPh2)) * 0.35f;
            d[i] = Mathf.Clamp((grind * 0.8f + sub) * sm * gain, -1f, 1f);
        }
    }
}
