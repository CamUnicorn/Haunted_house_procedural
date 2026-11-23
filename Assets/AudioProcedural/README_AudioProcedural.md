# Sonorización procedural – Haunted House (Unity 6)

Todo el audio de esta carpeta se **genera por código en tiempo de ejecución** (síntesis muestra a muestra en el hilo de audio o clips sintetizados al vuelo). Los `.wav` originales del proyecto se conservan (ambiente de la casa, loop del fantasma), pero cada categoría del enunciado tiene una capa procedural que responde a variables del juego.

## Arquitectura

```
                   distancia a enemigos ─┐
 ProceduralEnemySense (avistamiento) ────┼──► TensionManager ──► tension 0..1 + estado (Calma/Sospecha/Peligro/Final)
                                         │          │
                                         │          ├──► AdaptiveMusic   (música generativa)
                                         │          ├──► ProceduralWind  (viento)
                                         │          ├──► ProceduralWhispers (susurros)
                                         │          ├──► ProceduralGhostVoice (agitación global)
                                         │          └──► ProceduralCreaksAndTriggers (densidad de crujidos)
 GameEnding ──► OnWin()/OnCaught() ──────┘
 Key / Door ──► ProceduralSfx.KeyPickup / DoorOpen / DoorLocked
 Animator "IsWalking" ──► ProceduralFootstepDriver ──► ProceduralFootsteps
 Light.intensity (LightFlicker) ──► ProceduralLightBuzz
 UI Toolkit (menú) ──► ProceduralMenuAudio ──► Hover / Click
```

`ProceduralVoice` es la clase base: crea un `AudioClip` en *streaming* (`AudioClip.Create(..., stream: true, OnRead)`) y cada subclase rellena el buffer en `Generate()`. Así se conserva la espacialización 3D, el rolloff y el mixer de Unity, pero el contenido no viene de ningún archivo.

## Correspondencia con los requisitos

| Requisito | Script(s) | Técnica procedural | Parámetro del juego |
|---|---|---|---|
| 1. Música de fondo | `AdaptiveMusic`, `TensionManager`, `ProceduralMenuAudio` | Secuenciador generativo (random walk sobre pentatónica menor), drone de sierras desafinadas con filtro pasa-bajos, tritono, latido sintetizado, delay con realimentación | `tension` → tempo, densidad de notas, apertura del filtro, capas. Estados con histéresis. Menú: `ProceduralMenuTheme` = drone de sierras con filtro que respira + melodía generativa de caja de música con eco + viento + trueno lejano |
| 2. Personajes | `ProceduralFootsteps` + `ProceduralFootstepDriver`, `ProceduralGhostVoice`, `ProceduralEnemySense` | Pasos: ráfaga de ruido filtrado + golpe con caída de tono, aleatorizado por paso y por superficie. Fantasma: síntesis aditiva + vibrato/trémolo + aliento. Gárgolas/fantasmas: ruido marrón modulado + subarmónico con batido | `IsWalking` del Animator, superficie por raycast, distancia al jugador, tensión global, jugador dentro del cono de visión |
| 2b. Pasos suaves en alfombra (alternativa) | `ProceduralFootstepsSoft` | Solo ruido, sin tonos: pisotón = ruido blanco por pasa-bajos de 3 polos (260–380 Hz) + pasa-altos suave; roce de fibras = pasa-banda ancho ~1.8 kHz muy bajo. Dos fases talón→punta (45–80 ms) con ataque suave | Intensidad abre el filtro; con tensión alta pisa con más cuidado; `alwaysCarpet` ignora la superficie detectada |
| 2c. Pasos en madera naturales (recomendado) | `ProceduralFootstepsSampled` | Grabación real (Pixabay o `SFXFootstepsLooping.wav`) troceada automáticamente por detección de onsets (RMS en ventanas de 5 ms); cada paso elige un grano sin repetir, con pitch, ganancia y pasa-bajos aleatorios, voces solapadas y reflexiones tempranas | Intensidad y tensión (sigilo) bajan volumen y oscurecen el filtro |
| 3. Ambiente | `ProceduralWind`, `ProceduralWhispers`, `ProceduralCreaksAndTriggers`, `ProceduralLightBuzz` | Viento: ruido → SVF pasa-banda modulado por 3 LFO. Susurros: **síntesis de formantes** (ruido → 2 pasa-banda que se deslizan entre vocales) en frases aleatorias que se ubican alrededor del jugador. Crujidos: modelo stick-slip. Lámparas: hum aditivo 60 Hz + chispazos (proceso de Poisson) | Tensión → intensidad del viento, frecuencia de susurros y crujidos. `Light.intensity` y su derivada → volumen/brillo del hum y densidad de chispazos |
| 4. Interacción | `ProceduralSfx` (llamado desde `Key.cs` y `Door.cs`) | Llave vieja: síntesis modal grave + brillo disonante (segunda menor). Puerta embrujada: manija y pestillo modales + chirrido lento de bisagra (pulsos stick-slip irregulares → banco de resonadores SVF del panel) + aire + tope. Puerta cerrada: "toc" grave de nudillo (6 modos de madera). Todo con reverberación Schroeder | Recoger llave, chocar con puerta con/sin llave |
| 5. Interfaz y eventos | `ProceduralSfx`, `ProceduralMenuAudio`, `TensionManager`, `GameEnding` | Estética de terror: hover = nota corta tipo caja de música, click = golpe suave + campana oscura en tercera menor, alerta = latido + riser + trémolo de cuerdas disonantes, detectado = chirrido de cuerdas en cluster tipo "Psicosis", victoria = campana lejana + acorde de alivio, derrota = impacto + acorde disonante + chillido. Todos con reverberación | Hover/click, tensión > umbral, entrar al campo de visión, `GameEnding` |
| 6. Audio procedural | Todos | Síntesis aditiva, sustractiva, modal, de formantes, modelado físico simple, generación algorítmica, modulación (LFO, vibrato, trémolo), adaptación dinámica | Ver columnas anteriores |

## Instalación en Unity

1. Cerrar Unity y copiar la carpeta `Assets` del zip sobre la del proyecto con el explorador de archivos (reemplazar). No arrastrar al panel Project: Unity crea copias ("AudioProcedural 1") que duplican las clases.
2. Abrir Unity, verificar que la consola no tenga errores en rojo.
3. Menú superior **Audio Procedural → Crear todo**. Configura y guarda `MainMenu` y `DemoScene`: agrega todas las capas procedurales, activa los pasos de madera (grabación procesada), deja un solo sintetizador de pasos y silencia (mute) el loop de pasos original.
4. Play en `DemoScene`. La consola muestra `[Audio Procedural] Activo...` con lo que encontró.
5. (Opcional) En el objeto de pasos, arrastrar grabaciones de Pixabay al campo `Recordings`.

## Mezcla (niveles sugeridos)

- Música: `masterGain` 0.3–0.4 · Viento: `gain` 0.5–0.7 · Susurros: `gain` 0.4–0.6, AudioSource 0.8
- Zumbido de lámparas: `gain` 0.2–0.3, distancia máx. 6 m (solo se oye cerca)
- Sentido de enemigo: `gain` 0.5–0.7, distancia máx. 12 m
- Los SFX de evento se normalizan por código (pico 0.75–0.85).

## Notas técnicas

- `Generate()` corre en el hilo de audio: no usa API de Unity ni `UnityEngine.Random`; usa generadores xorshift propios. La comunicación con el hilo principal es por campos `volatile`.
- Los cambios de parámetro se suavizan dentro del bucle de muestras para evitar clics.
- Los sonidos 2D (UI, victoria/derrota) usan `DontDestroyOnLoad` para que no se corten al cambiar o reiniciar la escena.
