using UnityEngine;

/// <summary>
/// Pone esto en el jugador. Lee el parametro bool "IsWalking" del Animator (el proyecto original lo usa)
/// y dispara un paso procedural a intervalos regulares, con superficie detectada por raycast (por nombre
/// del material fisico o tag; por defecto madera).
/// </summary>
public class ProceduralFootstepDriver : MonoBehaviour
{
    public Animator animator;
    public ProceduralFootsteps footsteps;
    public string walkingParam = "IsWalking";
    public float strideSeconds = 0.5f;
    public ProceduralFootsteps.Surface defaultSurface = ProceduralFootsteps.Surface.Wood;

    float timer;

    void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (animator == null || footsteps == null) return;
        if (!animator.GetBool(walkingParam)) { timer = 0f; return; }

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            timer = strideSeconds * Random.Range(0.92f, 1.08f);
            footsteps.Step(Random.Range(0.7f, 1f), DetectSurface());
        }
    }

    ProceduralFootsteps.Surface DetectSurface()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.5f))
        {
            string n = hit.collider.name.ToLowerInvariant();
            if (n.Contains("stone") || n.Contains("floor_tile") || n.Contains("marble")) return ProceduralFootsteps.Surface.Stone;
            if (n.Contains("carpet") || n.Contains("rug")) return ProceduralFootsteps.Surface.Carpet;
        }
        return defaultSurface;
    }
}
