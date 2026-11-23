using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cerebro del audio adaptativo. Calcula 'tension' (0..1) a partir de dos variables del juego:
///   - distancia del jugador al enemigo mas cercano (objetos con el script Observer),
///   - avistamientos: cuando el jugador entra en el campo de vision de un enemigo (ProceduralEnemySense).
/// y la reparte a todas las capas procedurales: musica, viento, susurros y voces de fantasmas.
///
/// Estados musicales (derivados de la tension, con histeresis para no "parpadear"):
///   Calma (< 0.25)  -> solo notas sueltas y espaciadas.
///   Sospecha        -> entra el drone filtrado, el tempo sube.
///   Peligro (> 0.6) -> latido, tritono disonante, susurros frecuentes, alerta.
///   Final           -> victoria (tension 0, arpegio mayor) o derrota (tension 1, barrido descendente).
/// </summary>
public class TensionManager : MonoBehaviour
{
    public enum MusicState { Calma, Sospecha, Peligro, Final }

    public static TensionManager Instance { get; private set; }

    [Header("Capas procedurales (se buscan solas si estan vacias)")]
    public AdaptiveMusic music;
    public ProceduralWind wind;
    public ProceduralWhispers whispers;

    [Header("Escena")]
    public Transform player;
    public List<Transform> enemies = new List<Transform>();

    [Header("Parametros")]
    public float dangerRange = 10f;
    public float alertThreshold = 0.8f;
    public float alertCooldown = 6f;
    public float sightingDecay = 0.25f;   // por segundo
    [Tooltip("Si esta apagado, la musica de fondo responde exactamente igual que antes (solo distancia). "
           + "Los avistamientos siguen afectando susurros, alerta y demas capas.")]
    public bool sightingsAffectMusic = false;

    [Header("Solo lectura (depuracion)")]
    [SerializeField, Range(0f, 1f)] float tension;
    [SerializeField] MusicState state = MusicState.Calma;

    public float Tension => tension;
    public MusicState State => state;

    float lastAlert = -99f, sighting, musicTension;
    bool ended;

    void Awake() { Instance = this; }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (music == null) music = FindAnyObjectByType<AdaptiveMusic>();
        if (wind == null) wind = FindAnyObjectByType<ProceduralWind>();
        if (whispers == null) whispers = FindAnyObjectByType<ProceduralWhispers>();

        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
            else
            {
                var pm = FindAnyObjectByType<StealthGame.PlayerMovement>();
                if (pm != null) player = pm.transform;
            }
        }
        if (enemies.Count == 0)
            foreach (var o in FindObjectsByType<StealthGame.Observer>())
                enemies.Add(o.transform);

        // Diagnostico: confirma en la consola que el sistema arranco y que encontro cada capa
        Debug.Log($"[Audio Procedural] Activo. Musica: {(music != null ? "si" : "NO")}, viento: {(wind != null ? "si" : "NO")}, "
                + $"susurros: {(whispers != null ? "si" : "NO")}, jugador: {(player != null ? "si" : "NO")}, enemigos: {enemies.Count}, "
                + $"voces procedurales: {FindObjectsByType<ProceduralVoice>().Length}");

        // Sonido de interfaz: click procedural en botones uGUI de la escena (si los hay)
        foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include))
            b.onClick.AddListener(ProceduralSfx.Click);
    }

    /// <summary>Un enemigo ve (o casi ve) al jugador. strength 0..1.</summary>
    public void ReportSighting(float strength)
    {
        sighting = Mathf.Max(sighting, Mathf.Clamp01(strength));
    }

    void Update()
    {
        if (player == null || ended) return;

        float target = 0f;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            float d = Vector3.Distance(player.position, e.position);
            target = Mathf.Max(target, 1f - Mathf.Clamp01(d / dangerRange));
        }
        sighting = Mathf.MoveTowards(sighting, 0f, Time.deltaTime * sightingDecay);

        // Tension de la musica: igual que el sistema original (solo distancia)
        musicTension = Mathf.MoveTowards(musicTension, target, Time.deltaTime * (target > musicTension ? 0.8f : 0.3f));

        target = Mathf.Max(target, sighting);
        tension = Mathf.MoveTowards(tension, target, Time.deltaTime * (target > tension ? 0.8f : 0.3f));
        UpdateState();

        if (music != null) music.tension = sightingsAffectMusic ? tension : musicTension;
        if (wind != null) wind.intensity = 0.4f + 0.6f * tension;
        if (whispers != null) whispers.presence = Mathf.Clamp01(0.1f + tension * 0.9f);

        if (tension > alertThreshold && Time.time - lastAlert > alertCooldown)
        {
            lastAlert = Time.time;
            ProceduralSfx.Alert();
        }
    }

    void UpdateState()
    {
        // Histeresis: umbrales de subida y bajada distintos
        switch (state)
        {
            case MusicState.Calma: if (tension > 0.3f) state = MusicState.Sospecha; break;
            case MusicState.Sospecha:
                if (tension > 0.6f) state = MusicState.Peligro;
                else if (tension < 0.2f) state = MusicState.Calma;
                break;
            case MusicState.Peligro: if (tension < 0.5f) state = MusicState.Sospecha; break;
        }
    }

    public void OnCaught()
    {
        if (ended) return; ended = true; state = MusicState.Final;
        if (music != null) music.tension = 1f;
        if (whispers != null) whispers.presence = 0f;
        ProceduralSfx.Lose();
    }

    public void OnWin()
    {
        if (ended) return; ended = true; state = MusicState.Final;
        if (music != null) music.tension = 0f;
        if (wind != null) wind.intensity = 0.2f;
        if (whispers != null) whispers.presence = 0f;
        ProceduralSfx.Win();
    }
}
