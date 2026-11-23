using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Audio del menu principal (UI Toolkit). Ponlo en el objeto que tiene el UIDocument del menu.
///  - Hover y click procedurales en TODOS los botones del documento.
///  - Opcional: si se asigna un AdaptiveMusic/ProceduralWind a mano, les da una tension baja que "respira".
///    Por defecto NO busca ni cambia la musica de fondo del menu.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ProceduralMenuAudio : MonoBehaviour
{
    public AdaptiveMusic music;
    public ProceduralWind wind;
    [Range(0f, 1f)] public float menuTension = 0.2f;
    public float breatheAmount = 0.12f;
    public float breathePeriod = 20f;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        if (root == null) return;
        root.Query<Button>().ForEach(b =>
        {
            b.RegisterCallback<PointerEnterEvent>(_ => ProceduralSfx.Hover());
            b.RegisterCallback<FocusInEvent>(_ => ProceduralSfx.Hover());   // navegacion con teclado/mando
            b.clicked += ProceduralSfx.Click;
        });
    }

    void Update()
    {
        float b = Mathf.Sin(Time.time * 2f * Mathf.PI / Mathf.Max(1f, breathePeriod)) * breatheAmount;
        if (music != null) music.tension = Mathf.Clamp01(menuTension + b);
        if (wind != null) wind.intensity = Mathf.Clamp01(0.35f + b);
    }
}
