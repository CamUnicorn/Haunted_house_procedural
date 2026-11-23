using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu "Audio Procedural" en la barra superior de Unity.
///  - "Crear todo": configura DemoScene y MainMenu y las guarda (un solo clic).
///  - "Avanzado/...": cada paso por separado.
/// Solo agrega lo que falta: se puede ejecutar varias veces sin duplicar nada.
/// </summary>
public static class ProceduralAudioSetup
{
    // ===================== CREAR TODO (un solo clic) =====================
    // Abre DemoScene y MainMenu, agrega todos los sonidos procedurales, activa los pasos de madera,
    // silencia el loop de pasos original (sin desactivarlo, para no generar avisos) y guarda ambas escenas.
    [MenuItem("Audio Procedural/Crear todo", false, 0)]
    static void CreateAll()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Audio Procedural", "Sal del modo Play antes de usar 'Crear todo'.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        string game = FindScene("DemoScene");
        string menu = FindScene("MainMenu");
        if (game == null)
        {
            EditorUtility.DisplayDialog("Audio Procedural", "No encontre la escena 'DemoScene' en el proyecto.", "OK");
            return;
        }

        string report = "";
        if (menu != null)
        {
            var m = EditorSceneManager.OpenScene(menu, OpenSceneMode.Single);
            SetupMenu();
            EditorSceneManager.SaveScene(m);
            report += "- MainMenu: sonidos de botones y fondo sonoro agregados.\n";
        }

        var g = EditorSceneManager.OpenScene(game, OpenSceneMode.Single);
        int added = SetupGame();
        UseSampledSteps();
        MuteOriginalFootsteps();
        EditorSceneManager.SaveScene(g);
        report += $"- DemoScene: {added} componentes nuevos, pasos de madera activos, pasos originales silenciados.\n";

        Debug.Log("[Audio Procedural] Crear todo terminado.\n" + report);
        EditorUtility.DisplayDialog("Audio Procedural",
            "Listo. Escenas configuradas y guardadas:\n\n" + report + "\nDale Play en DemoScene.", "OK");
    }

    static string FindScene(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Scene"))
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return p;
        }
        return null;
    }

    [MenuItem("Audio Procedural/Avanzado/Configurar escena de juego (DemoScene)")]
    static void SetupGameMenu() { SetupGame(); }

    static int SetupGame()
    {
        int added = 0;

        // 1) TensionManager
        var tm = Object.FindAnyObjectByType<TensionManager>();
        if (tm == null)
        {
            var go = new GameObject("AudioManager");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            tm = Undo.AddComponent<TensionManager>(go);
            added++;
        }

        // 1b) Musica adaptativa y viento: SOLO se crean si no existen.
        //     Nunca se modifican los objetos de audio de fondo que ya estan en la escena
        //     (Ambient con SFXHouseAmbience, musica existente, etc.).
        var music = Object.FindAnyObjectByType<AdaptiveMusic>();
        if (music == null)
        {
            var go = new GameObject("Music (Procedural)");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            music = Undo.AddComponent<AdaptiveMusic>(go);
            Make2D(go.GetComponent<AudioSource>(), 1f);
            added++;
        }
        var wind = Object.FindAnyObjectByType<ProceduralWind>();
        if (wind == null)
        {
            var go = new GameObject("Wind (Procedural)");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            wind = Undo.AddComponent<ProceduralWind>(go);
            Make2D(go.GetComponent<AudioSource>(), 0.5f);
            added++;
        }

        // 1c) Pasos procedurales del jugador (sintetizador + driver que lee "IsWalking")
        var pm = Object.FindAnyObjectByType<StealthGame.PlayerMovement>();
        if (pm != null && Object.FindAnyObjectByType<ProceduralFootstepDriver>() == null)
        {
            var steps = Object.FindAnyObjectByType<ProceduralFootsteps>();
            if (steps == null)
            {
                var go = new GameObject("Footsteps (Procedural)");
                Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
                Undo.SetTransformParent(go.transform, pm.transform, "Audio Procedural");
                go.transform.localPosition = Vector3.zero;
                steps = Undo.AddComponent<ProceduralFootsteps>(go);
                Make3D(go.GetComponent<AudioSource>(), 1f, 15f, 1f);
                added++;
            }
            var drv = Undo.AddComponent<ProceduralFootstepDriver>(pm.gameObject);
            drv.animator = pm.GetComponentInChildren<Animator>();
            drv.footsteps = steps;
            added++;
        }

        // 2) Susurros sobrenaturales
        var wh = Object.FindAnyObjectByType<ProceduralWhispers>();
        if (wh == null)
        {
            var go = new GameObject("Procedural Whispers");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            wh = Undo.AddComponent<ProceduralWhispers>(go);
            Make3D(go.GetComponent<AudioSource>(), 1f, 10f, 0.8f);
            added++;
        }
        Undo.RecordObject(tm, "Audio Procedural");
        tm.whispers = wh;
        if (tm.music == null) tm.music = music;
        if (tm.wind == null) tm.wind = wind;

        // 3) Crujidos de la casa alrededor del jugador
        bool hasCreaks = false;
        foreach (var c in Object.FindObjectsByType<ProceduralCreaksAndTriggers>())
            if (c.randomAroundListener) hasCreaks = true;
        if (!hasCreaks)
        {
            var go = new GameObject("Procedural Creaks (ambiente)");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            Undo.AddComponent<ProceduralCreaksAndTriggers>(go);
            added++;
        }

        // 4) Zumbido electrico en cada lampara que parpadea
        foreach (var lf in Object.FindObjectsByType<StealthGame.LightFlicker>())
        {
            if (lf.GetComponentInChildren<ProceduralLightBuzz>(true) != null) continue;
            var go = new GameObject("Procedural Light Buzz");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            Undo.SetTransformParent(go.transform, lf.transform, "Audio Procedural");
            go.transform.localPosition = Vector3.zero;
            var buzz = Undo.AddComponent<ProceduralLightBuzz>(go);
            buzz.targetLight = lf.flickeringLight;
            buzz.minIntensity = lf.lightIntensityMin;
            buzz.maxIntensity = lf.lightIntensityMax;
            Make3D(go.GetComponent<AudioSource>(), 0.5f, 6f, 1f);
            added++;
        }

        // 5) Reaccion de cada enemigo (gargolas y fantasmas) al ver al jugador
        foreach (var obs in Object.FindObjectsByType<StealthGame.Observer>())
        {
            if (obs.GetComponent<ProceduralEnemySense>() != null) continue;
            Undo.AddComponent<ProceduralEnemySense>(obs.gameObject);
            Make3D(obs.GetComponent<AudioSource>(), 1f, 12f, 1f);
            added++;
        }

        // 6) Voz procedural en cada fantasma que patrulla (si aun no tiene)
        foreach (var wp in Object.FindObjectsByType<StealthGame.WaypointPatrol>())
        {
            if (wp.GetComponentInChildren<ProceduralGhostVoice>(true) != null) continue;
            var go = new GameObject("Procedural Ghost Voice");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            Undo.SetTransformParent(go.transform, wp.transform, "Audio Procedural");
            go.transform.localPosition = Vector3.up;
            Undo.AddComponent<ProceduralGhostVoice>(go);
            Make3D(go.GetComponent<AudioSource>(), 1f, 12f, 0.8f);
            added++;
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Audio Procedural] Escena de juego configurada. Componentes agregados: {added}. Guarda la escena (Ctrl+S).");
        return added;
    }

    [MenuItem("Audio Procedural/Avanzado/Configurar menu principal (MainMenu)")]
    static void SetupMenu()
    {
        var menu = Object.FindAnyObjectByType<StealthGame.MainMenu>();
        if (menu == null) { Debug.LogWarning("[Audio Procedural] Abre primero la escena MainMenu."); return; }

        // Sonidos de los botones
        var ma = menu.GetComponent<ProceduralMenuAudio>();
        if (ma == null) ma = Undo.AddComponent<ProceduralMenuAudio>(menu.gameObject);

        // Fondo sonoro del menu (tema procedural)
        if (Object.FindAnyObjectByType<ProceduralMenuTheme>() == null)
        {
            var go = new GameObject("Menu Theme (Procedural)");
            Undo.RegisterCreatedObjectUndo(go, "Audio Procedural");
            Undo.AddComponent<ProceduralMenuTheme>(go);
            Make2D(go.GetComponent<AudioSource>(), 1f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Audio Procedural] Menu principal configurado. Guarda la escena (Ctrl+S).");
    }

    [MenuItem("Audio Procedural/Avanzado/Silenciar pasos pregrabados del jugador")]
    static void MuteOriginalFootsteps()
    {
        var pm = Object.FindAnyObjectByType<StealthGame.PlayerMovement>();
        if (pm == null) return;
        var src = pm.GetComponent<AudioSource>();
        if (src == null) return;
        Undo.RecordObject(src, "Audio Procedural");
        src.enabled = true; // desactivado genera "Can not play a disabled audio source" (PlayerMovement lo reproduce)
        src.mute = true; // PlayerMovement sigue funcionando igual; solo deja de oirse el loop .wav
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Audio Procedural] Loop de pasos pregrabado silenciado; quedan los pasos procedurales.");
    }

    [MenuItem("Audio Procedural/Avanzado/Pasos: madera natural (grabacion procesada)")]
    static void UseSampledSteps()
    {
        var steps = SwapFootsteps<ProceduralFootstepsSampled>() as ProceduralFootstepsSampled;
        if (steps == null) return;
        if (steps.recordings == null || steps.recordings.Length == 0)
        {
            // Por defecto usa la grabacion de pasos que trae el proyecto; agrega tus archivos de Pixabay al arreglo.
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_3DStealthGame/Audio/SFXFootstepsLooping.wav");
            Undo.RecordObject(steps, "Audio Procedural");
            steps.recordings = clip != null ? new[] { clip } : new AudioClip[0];
        }
        Selection.activeObject = steps.gameObject;
        Debug.Log("[Audio Procedural] Pasos de madera activados. Arrastra tus grabaciones de Pixabay al campo 'Recordings' y guarda la escena.");
    }

    [MenuItem("Audio Procedural/Avanzado/Pasos: suaves sintetizados (alfombra)")]
    static void UseSoftSteps() { SwapFootsteps<ProceduralFootstepsSoft>(); }

    [MenuItem("Audio Procedural/Avanzado/Pasos: sintetizados originales")]
    static void UseOriginalSteps() { SwapFootsteps<ProceduralFootsteps>(); }

    // Deja UN solo sintetizador de pasos del tipo T: reemplaza los de otro tipo, quita duplicados
    // (asi no queda sonando ningun paso viejo) y deja un unico driver apuntando a el.
    static ProceduralFootsteps SwapFootsteps<T>() where T : ProceduralFootsteps
    {
        var all = Object.FindObjectsByType<ProceduralFootsteps>(FindObjectsInactive.Include);
        if (all.Length == 0)
        {
            Debug.LogWarning("[Audio Procedural] No hay pasos procedurales en la escena. Usa 'Crear todo'.");
            return null;
        }

        ProceduralFootsteps keep = null;
        foreach (var fs in all)
        {
            if (fs == null) continue;
            if (keep == null && fs.GetType() == typeof(T)) { keep = fs; continue; }
            var go = fs.gameObject;
            Undo.DestroyObjectImmediate(fs);
            if (keep == null) keep = Undo.AddComponent<T>(go);
        }

        var drivers = Object.FindObjectsByType<ProceduralFootstepDriver>(FindObjectsInactive.Include);
        for (int i = 0; i < drivers.Length; i++)
        {
            if (i == 0)
            {
                Undo.RecordObject(drivers[0], "Audio Procedural");
                drivers[0].footsteps = keep;
                if (drivers[0].animator == null) drivers[0].animator = drivers[0].GetComponentInChildren<Animator>();
            }
            else Undo.DestroyObjectImmediate(drivers[i]);
        }

        var src = keep.GetComponent<AudioSource>();
        if (src != null) { Undo.RecordObject(src, "Audio Procedural"); src.enabled = true; src.mute = false; }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[Audio Procedural] Pasos activos: " + typeof(T).Name + ".");
        return keep;
    }

    static void Make2D(AudioSource s, float vol)
    {
        if (s == null) return;
        Undo.RecordObject(s, "Audio Procedural");
        s.spatialBlend = 0f;
        s.volume = vol;
        s.playOnAwake = false;
    }

    static void Make3D(AudioSource s, float min, float max, float vol)
    {
        if (s == null) return;
        Undo.RecordObject(s, "Audio Procedural");
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = min;
        s.maxDistance = max;
        s.volume = vol;
        s.playOnAwake = false;
    }
}
