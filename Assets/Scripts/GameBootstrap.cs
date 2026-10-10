using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Punto de entrada. Se pone en UN solo GameObject de una escena vacía y construye todo el juego:
/// iluminación y ambientación, nivel (suelo, muros, obstáculos con texturas), reliquias,
/// puerta de salida, jugador, guardia con IA, polvo ambiental y la interfaz.
/// Cada sistema vive en su propio script (GameManager, PlayerController, GuardAI, ...).
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    static readonly Color FogColor = new Color(0.03f, 0.04f, 0.08f);

    Transform levelRoot;     // geometría estática -> se combina con Static Batching (optimización)
    Transform dynamicRoot;   // objetos que se mueven, giran o desaparecen
    readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    Texture2D brickTex, tileTex;

    void Awake()
    {
        Application.targetFrameRate = 60;
        ClearDefaultObjects();
        SetupAtmosphere();

        // Sistemas
        var systems = new GameObject("Systems");
        var gm = systems.AddComponent<GameManager>();
        systems.AddComponent<AudioManager>();
        systems.AddComponent<EffectsPool>();

        // Mundo
        BuildLevel(gm);
        PlayerController player = CreatePlayer();
        CreateGuard(player);
        EffectsPool.Instance.CreateDust(new Vector3(0f, 3f, 0f), new Vector3(38f, 5f, 38f));

        // Interfaz (al final, porque lee datos de los demás sistemas)
        var ui = new GameObject("UI").AddComponent<UIManager>();
        ui.player = player;

        // OPTIMIZACIÓN: Static Batching de toda la geometría fija (menos draw calls)
        StaticBatchingUtility.Combine(levelRoot.gameObject);
        Debug.Log("[Bootstrap] Static Batching aplicado a " + levelRoot.childCount + " objetos del nivel.");
    }

    // ------------------------------------------------------------------ ESCENA / AMBIENTE
    void ClearDefaultObjects()
    {
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.gameObject.SetActive(false);
            Destroy(cam.gameObject);
        }
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            l.gameObject.SetActive(false);
            Destroy(l.gameObject);
        }
    }

    void SetupAtmosphere()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.13f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = 0.04f;
        QualitySettings.pixelLightCount = 6;

        var moon = new GameObject("Moonlight");
        moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        AddLight(moon, LightType.Directional, new Color(0.55f, 0.65f, 1f), 0.45f, 10f);
    }

    // OPTIMIZACIÓN de luces: ninguna proyecta sombras y todas tienen rango limitado.
    Light AddLight(GameObject go, LightType type, Color color, float intensity, float range)
    {
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }

    // ------------------------------------------------------------------ NIVEL
    void BuildLevel(GameManager gm)
    {
        levelRoot = new GameObject("Level (static)").transform;
        dynamicRoot = new GameObject("Dynamic objects").transform;

        brickTex = GameUtils.MakePatternTexture(new Color(0.55f, 0.55f, 0.62f), new Color(0.2f, 0.2f, 0.26f), 32, 16, true);
        tileTex = GameUtils.MakePatternTexture(new Color(0.42f, 0.44f, 0.5f), new Color(0.14f, 0.14f, 0.18f), 32, 32, false);

        // Suelo (40 x 40)
        Block("Floor", new Vector3(0f, -0.25f, 0f), new Vector3(40f, 0.5f, 40f), tileTex, Color.white, new Vector2(20f, 20f), levelRoot);

        // Muros exteriores (la pared norte tiene un hueco de 4 m para la puerta)
        Wall("Wall S", new Vector3(0f, 2f, -20.5f), new Vector3(42f, 4f, 1f));
        Wall("Wall W", new Vector3(-20.5f, 2f, 0f), new Vector3(1f, 4f, 42f));
        Wall("Wall E", new Vector3(20.5f, 2f, 0f), new Vector3(1f, 4f, 42f));
        Wall("Wall N left", new Vector3(-11.5f, 2f, 20.5f), new Vector3(19f, 4f, 1f));
        Wall("Wall N right", new Vector3(11.5f, 2f, 20.5f), new Vector3(19f, 4f, 1f));

        CreateExit();

        // Obstáculos (x, z, ancho, largo)
        Vector4[] obstacles =
        {
            new Vector4(-8f, -6f, 4f, 4f),
            new Vector4(8f, -6f, 4f, 4f),
            new Vector4(0f, 2f, 6f, 2f),
            new Vector4(-12f, 8f, 2f, 8f),
            new Vector4(12f, 8f, 2f, 8f),
            new Vector4(-6f, 14f, 4f, 4f),
            new Vector4(6f, 14f, 4f, 4f)
        };
        foreach (var o in obstacles)
            Wall("Pillar", new Vector3(o.x, 1.5f, o.y), new Vector3(o.z, 3f, o.w));

        // Cajas decorativas
        Vector3[] crates =
        {
            new Vector3(-3f, 0.6f, -9f), new Vector3(4f, 0.6f, -14.5f), new Vector3(13f, 0.6f, 0f),
            new Vector3(-10f, 0.6f, 0f), new Vector3(3f, 0.6f, 9f), new Vector3(-2f, 0.6f, -3f)
        };
        foreach (var c in crates)
            Block("Crate", c, new Vector3(1.2f, 1.2f, 1.2f), null, new Color(0.45f, 0.3f, 0.15f), Vector2.one, levelRoot);

        // Reliquias
        Vector3[] relics =
        {
            new Vector3(-15f, 1.1f, -5f), new Vector3(15f, 1.1f, -5f), new Vector3(0f, 1.1f, 8f),
            new Vector3(-16f, 1.1f, 15f), new Vector3(16f, 1.1f, 15f)
        };
        foreach (var p in relics) CreateRelic(p);
        gm.RelicsTotal = relics.Length;
    }

    void CreateExit()
    {
        // Pasillo detrás de la puerta (estático)
        Block("Exit floor", new Vector3(0f, -0.25f, 23.25f), new Vector3(4f, 0.5f, 6.5f), tileTex, Color.white, new Vector2(2f, 3f), levelRoot);
        Wall("Exit wall L", new Vector3(-2.5f, 2f, 24f), new Vector3(1f, 4f, 6f));
        Wall("Exit wall R", new Vector3(2.5f, 2f, 24f), new Vector3(1f, 4f, 6f));
        Wall("Exit wall back", new Vector3(0f, 2f, 26.5f), new Vector3(6f, 4f, 1f));

        // Puerta (dinámica: baja al suelo al abrirse). Empieza roja.
        var door = Block("Door", new Vector3(0f, 2f, 20.5f), new Vector3(4f, 4f, 1f), null, new Color(0.8f, 0.1f, 0.1f), Vector2.one, dynamicRoot);
        var doorRenderer = door.GetComponent<Renderer>();
        GameUtils.SetEmission(doorRenderer.material, new Color(0.6f, 0.05f, 0.05f));

        // Zona de salida (trigger) + luz roja que sirve de faro desde lejos
        var zone = new GameObject("ExitZone");
        zone.transform.SetParent(dynamicRoot, false);
        zone.transform.position = new Vector3(0f, 1f, 23.5f);
        var col = zone.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(3.5f, 2f, 3.5f);

        var lightGO = new GameObject("ExitLight");
        lightGO.transform.SetParent(zone.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.5f, -1f);
        var exitLight = AddLight(lightGO, LightType.Point, new Color(1f, 0.15f, 0.1f), 2.5f, 10f);

        var exit = zone.AddComponent<ExitDoor>();
        exit.door = door.transform;
        exit.doorRenderer = doorRenderer;
        exit.exitLight = exitLight;
    }

    void CreateRelic(Vector3 pos)
    {
        var root = new GameObject("Relic");
        root.transform.SetParent(dynamicRoot, false);
        root.transform.position = pos;

        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1f;

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Gem";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
        visual.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        Destroy(visual.GetComponent<Collider>());
        var mat = GameUtils.NewMaterial(new Color(1f, 0.8f, 0.2f), null, Vector2.one, 0.8f);
        GameUtils.SetEmission(mat, new Color(1f, 0.7f, 0.1f) * 1.5f);
        visual.GetComponent<Renderer>().sharedMaterial = mat;

        var lightGO = new GameObject("Glow");
        lightGO.transform.SetParent(root.transform, false);
        AddLight(lightGO, LightType.Point, new Color(1f, 0.8f, 0.3f), 1.6f, 7f);

        root.AddComponent<Relic>();
    }

    // ------------------------------------------------------------------ JUGADOR
    PlayerController CreatePlayer()
    {
        var go = new GameObject("Player");
        go.transform.position = new Vector3(0f, 0.1f, -17f); // mira al norte, hacia la puerta roja
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        var pc = go.AddComponent<PlayerController>();

        var pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(go.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var camGO = new GameObject("Player Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(pivot.transform, false);
        var cam = camGO.AddComponent<Camera>();
        cam.fieldOfView = 70f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f; // OPTIMIZACIÓN: no se dibuja lo que está muy lejos
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = FogColor;
        camGO.AddComponent<AudioListener>();

        var fl = new GameObject("Flashlight");
        fl.transform.SetParent(camGO.transform, false);
        var spot = AddLight(fl, LightType.Spot, new Color(1f, 0.95f, 0.8f), 2.4f, 22f);
        spot.spotAngle = 55f;
        spot.renderMode = LightRenderMode.ForcePixel;

        pc.cameraPivot = pivot.transform;
        pc.flashlight = spot;
        return pc;
    }

    // ------------------------------------------------------------------ GUARDIA
    void CreateGuard(PlayerController player)
    {
        Vector3[] route =
        {
            new Vector3(-14f, 0f, -12f), new Vector3(14f, 0f, -12f), new Vector3(16f, 0f, 2f),
            new Vector3(14f, 0f, 17f), new Vector3(0f, 0f, 17f), new Vector3(-14f, 0f, 17f),
            new Vector3(-16f, 0f, 2f)
        };

        var wpRoot = new GameObject("Waypoints").transform;
        wpRoot.SetParent(dynamicRoot, false);
        var wps = new Transform[route.Length];
        for (int i = 0; i < route.Length; i++)
        {
            var w = new GameObject("WP" + i).transform;
            w.SetParent(wpRoot, false);
            w.position = route[i];
            wps[i] = w;
        }

        const int start = 3;
        var go = new GameObject("Guard");
        go.transform.position = route[start] + Vector3.up * 0.05f;
        var cc = go.AddComponent<CharacterController>();
        cc.height = 1.9f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0f, 0.95f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
        body.transform.localScale = new Vector3(0.8f, 0.95f, 0.8f);
        Destroy(body.GetComponent<Collider>());

        var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        visor.transform.SetParent(go.transform, false);
        visor.transform.localPosition = new Vector3(0f, 1.5f, 0.33f);
        visor.transform.localScale = new Vector3(0.5f, 0.12f, 0.2f);
        Destroy(visor.GetComponent<Collider>());

        var lightGO = new GameObject("GuardFlashlight");
        lightGO.transform.SetParent(go.transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 1.5f, 0.4f);
        lightGO.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
        var spot = AddLight(lightGO, LightType.Spot, new Color(1f, 0.95f, 0.8f), 2.5f, 14f);
        spot.spotAngle = 60f;
        spot.renderMode = LightRenderMode.ForcePixel;

        var ai = go.AddComponent<GuardAI>();
        ai.waypoints = wps;
        ai.startWaypoint = start;
        ai.player = player.transform;
        ai.bodyRenderer = body.GetComponent<Renderer>();
        ai.visorRenderer = visor.GetComponent<Renderer>();
        ai.flashlight = spot;
    }

    // ------------------------------------------------------------------ HELPERS
    void Wall(string name, Vector3 pos, Vector3 size)
    {
        var tiling = new Vector2(
            Mathf.Max(1f, Mathf.Round(Mathf.Max(size.x, size.z) / 4f)),
            Mathf.Max(1f, Mathf.Round(size.y / 4f)));
        Block(name, pos, size, brickTex, new Color(0.85f, 0.9f, 1f), tiling, levelRoot);
    }

    GameObject Block(string name, Vector3 pos, Vector3 size, Texture2D tex, Color tint, Vector2 tiling, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = GetMaterial(tex, tint, tiling);
        return go;
    }

    // Materiales compartidos: los objetos iguales reutilizan el mismo material (ayuda al batching)
    Material GetMaterial(Texture2D tex, Color tint, Vector2 tiling)
    {
        string key = (tex != null ? tex.GetInstanceID() : 0) + "_" + tint + "_" + tiling;
        Material m;
        if (!materials.TryGetValue(key, out m))
        {
            m = GameUtils.NewMaterial(tint, tex, tiling);
            materials[key] = m;
        }
        return m;
    }
}
