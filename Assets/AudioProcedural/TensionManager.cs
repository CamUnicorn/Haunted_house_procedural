using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cerebro del audio adaptativo. Calcula 'tension' (0..1) segun la distancia a los enemigos
/// (objetos con el script "Observer" del proyecto original) y la manda a la musica y al viento.
/// Tambien dispara la alerta de deteccion, y expone OnCaught()/OnWin() para GameEnding.
/// </summary>
public class TensionManager : MonoBehaviour
{
    public static TensionManager Instance { get; private set; }

    public AdaptiveMusic music;
    public ProceduralWind wind;
    public Transform player;
    public List<Transform> enemies = new List<Transform>();
    public float dangerRange = 10f;
    public float alertThreshold = 0.8f;
    public float alertCooldown = 6f;

    float tension, lastAlert = -99f;
    bool ended;

    void Awake() { Instance = this; }

    void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) player = go.transform;
            else foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                    if (mb.GetType().Name == "PlayerMovement") { player = mb.transform; break; }
        }
        if (enemies.Count == 0)
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
                if (mb.GetType().Name == "Observer") enemies.Add(mb.transform);

        // Sonido de interfaz: click procedural en todos los botones de la escena
        foreach (var b in FindObjectsOfType<Button>(true)) b.onClick.AddListener(ProceduralSfx.Click);
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
        tension = Mathf.MoveTowards(tension, target, Time.deltaTime * (target > tension ? 0.8f : 0.3f));

        if (music != null) music.tension = tension;
        if (wind != null) wind.intensity = 0.4f + 0.6f * tension;

        if (tension > alertThreshold && Time.time - lastAlert > alertCooldown)
        {
            lastAlert = Time.time;
            ProceduralSfx.Alert();
        }
    }

    public void OnCaught()
    {
        if (ended) return; ended = true;
        if (music != null) music.tension = 1f;
        ProceduralSfx.Lose();
    }

    public void OnWin()
    {
        if (ended) return; ended = true;
        if (music != null) music.tension = 0f;
        if (wind != null) wind.intensity = 0.2f;
        ProceduralSfx.Win();
    }
}
