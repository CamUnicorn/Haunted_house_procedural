using UnityEngine;

/// <summary>
/// Crujidos y sonidos de interaccion.
/// - Modo ambiente (randomAroundListener): crujidos de la casa en posiciones aleatorias alrededor del jugador;
///   la frecuencia aumenta con el parametro 'frequency'.
/// - Modo zona (OnTriggerEnter con el jugador): puerta/crujido, recoger objeto o alerta.
/// </summary>
public class ProceduralCreaksAndTriggers : MonoBehaviour
{
    public enum Kind { Creak, Pickup, Alert }

    [Header("Ambiente aleatorio")]
    public bool randomAroundListener = true;
    public Vector2 intervalSeconds = new Vector2(6f, 18f);
    public Vector2 radius = new Vector2(5f, 12f);

    [Header("Zona de interaccion")]
    public bool triggerMode = false;
    public Kind kind = Kind.Creak;
    public float cooldown = 2f;

    float next, lastTrigger = -99f;
    Transform listener;

    void Start() { next = Time.time + Random.Range(intervalSeconds.x, intervalSeconds.y); }

    void Update()
    {
        if (!randomAroundListener || Time.time < next) return;
        float t = TensionManager.Instance != null ? TensionManager.Instance.Tension : 0f;
        next = Time.time + Random.Range(intervalSeconds.x, intervalSeconds.y) * Mathf.Lerp(1f, 0.4f, t); // mas tension = mas crujidos
        if (listener == null)
        {
            var l = FindAnyObjectByType<AudioListener>();
            if (l == null) return;
            listener = l.transform;
        }
        Vector2 c = Random.insideUnitCircle.normalized * Random.Range(radius.x, radius.y);
        ProceduralSfx.Creak(listener.position + new Vector3(c.x, 0f, c.y));
    }

    void OnTriggerEnter(Collider other)
    {
        if (!triggerMode || Time.time - lastTrigger < cooldown) return;
        if (!other.CompareTag("Player")) return;
        lastTrigger = Time.time;
        switch (kind)
        {
            case Kind.Creak: ProceduralSfx.Creak(transform.position); break;
            case Kind.Pickup: ProceduralSfx.Pickup(transform.position); break;
            case Kind.Alert: ProceduralSfx.Alert(); break;
        }
    }
}
