using UnityEngine;

/// <summary>
/// Clase base: crea un AudioClip en streaming (sin archivo) cuyo contenido
/// se calcula muestra a muestra en Generate(). Al reproducirse a traves de un
/// AudioSource conserva espacializacion 3D, mixer, rolloff, etc.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public abstract class ProceduralVoice : MonoBehaviour
{
    protected int sampleRate;
    protected AudioSource src;

    protected virtual void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        src = GetComponent<AudioSource>();
        if (!src.enabled) src.enabled = true; // un AudioSource desactivado no puede reproducir
        // Clip de 1 segundo, mono, streaming: Unity llama a OnRead continuamente (hilo de audio).
        AudioClip clip = AudioClip.Create(name + "_procedural", sampleRate, 1, sampleRate, true, OnRead);
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.Play();
    }

    void OnRead(float[] data) { Generate(data); }

    /// <summary>Configura el AudioSource como fuente 3D (lo usan las voces espaciales desde Reset()).</summary>
    protected void Configure3D(float minDistance, float maxDistance, float volume = 1f)
    {
        var s = GetComponent<AudioSource>();
        if (s == null) return;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.volume = volume;
        s.playOnAwake = false;
    }

    /// <summary>Rellena el buffer. OJO: corre en el hilo de audio (no usar API de Unity ni UnityEngine.Random).</summary>
    protected abstract void Generate(float[] data);
}
