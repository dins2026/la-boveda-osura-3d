using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interfaz completa creada por código: pantalla de inicio, HUD (reliquias, tiempo, resistencia,
/// avisos del guardia), pantalla de victoria/derrota y panel técnico (F1) para la sustentación.
/// </summary>
public class UIManager : MonoBehaviour
{
    public PlayerController player;

    Font font;
    Text relicsText, timerText, hintText, alertText, titleText, subText, debugText;
    RectTransform staminaFill;
    GameObject overlay, debugPanel;

    int lastSecond = -1;
    float lastStamina = -1f;
    float fpsAccum;
    int fpsFrames;
    string guardState = "Patrulla";

    void Awake()
    {
        font = LoadFont();
        BuildUI();

        var gm = GameManager.Instance;
        gm.OnStateChanged += HandleState;
        gm.OnRelicsChanged += HandleRelics;
        gm.OnAllRelicsCollected += HandleAllRelics;
        GuardAI.OnGuardStateChanged += HandleGuard;

        HandleRelics(gm.RelicsCollected, gm.RelicsTotal);
        HandleState(gm.State);
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnStateChanged -= HandleState;
            gm.OnRelicsChanged -= HandleRelics;
            gm.OnAllRelicsCollected -= HandleAllRelics;
        }
        GuardAI.OnGuardStateChanged -= HandleGuard;
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // El texto del tiempo solo se actualiza cuando cambia el segundo
        int sec = Mathf.CeilToInt(gm.TimeLeft);
        if (sec != lastSecond)
        {
            lastSecond = sec;
            timerText.text = string.Format("{0:00}:{1:00}", sec / 60, sec % 60);
            timerText.color = sec <= 30 ? new Color(1f, 0.25f, 0.25f) : Color.white;
        }

        if (player != null)
        {
            float s = Mathf.Clamp01(player.StaminaNormalized);
            if (!Mathf.Approximately(s, lastStamina))
            {
                lastStamina = s;
                staminaFill.anchorMax = new Vector2(s, 1f);
            }
        }

        if (Input.GetKeyDown(KeyCode.F1)) debugPanel.SetActive(!debugPanel.activeSelf);
        if (debugPanel.activeSelf) UpdateDebug();
    }

    // ---------- Eventos ----------
    void HandleState(GameState s)
    {
        var gm = GameManager.Instance;
        overlay.SetActive(s != GameState.Playing);

        switch (s)
        {
            case GameState.Menu:
                titleText.text = "LA BÓVEDA OSCURA";
                titleText.color = new Color(1f, 0.85f, 0.3f);
                subText.text = "Reúne las reliquias doradas y escapa por la puerta del norte\nantes de que se acabe el tiempo.\n\n" +
                               "El guardia te ve, te oye cuando corres y te persigue.\n\n" +
                               "WASD: mover   ·   Mouse: mirar   ·   Shift: correr   ·   F: linterna   ·   F1: panel técnico\n\n" +
                               "Presiona ESPACIO para comenzar";
                break;
            case GameState.Won:
                titleText.text = "¡ESCAPASTE!";
                titleText.color = new Color(0.3f, 1f, 0.4f);
                subText.text = gm.EndReason + "\nTe sobraron " + Mathf.CeilToInt(gm.TimeLeft) + " segundos.\n\nR: jugar de nuevo   ·   ESC: salir";
                break;
            case GameState.Lost:
                titleText.text = "PERDISTE";
                titleText.color = new Color(1f, 0.3f, 0.3f);
                subText.text = gm.EndReason + "\n\nR: reintentar   ·   ESC: salir";
                break;
        }

        if (s != GameState.Playing) alertText.text = "";
    }

    void HandleRelics(int collected, int total)
    {
        relicsText.text = "Reliquias: " + collected + " / " + total;
    }

    void HandleAllRelics()
    {
        hintText.text = "¡La puerta se abrió! Corre a la salida (norte)";
        hintText.color = new Color(0.4f, 1f, 0.5f);
    }

    void HandleGuard(GuardAI.State s)
    {
        switch (s)
        {
            case GuardAI.State.Chase:
                alertText.text = "¡TE VIERON!";
                alertText.color = new Color(1f, 0.2f, 0.2f);
                guardState = "Persecución";
                break;
            case GuardAI.State.Search:
                alertText.text = "El guardia te está buscando...";
                alertText.color = new Color(1f, 0.8f, 0.2f);
                guardState = "Búsqueda";
                break;
            default:
                alertText.text = "";
                guardState = "Patrulla";
                break;
        }
    }

    void UpdateDebug()
    {
        fpsAccum += Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsAccum < 0.5f) return;

        float fps = fpsFrames / fpsAccum;
        fpsAccum = 0f;
        fpsFrames = 0;
        var fx = EffectsPool.Instance;
        debugText.text = "FPS: " + fps.ToString("0") +
                         "\nPool de efectos: " + fx.Created + " creados / " + fx.Available + " libres" +
                         "\nGuardia: " + guardState;
    }

    // ---------- Construcción de la UI ----------
    void BuildUI()
    {
        var canvasGO = new GameObject("UI Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGO.transform;

        // HUD
        relicsText = AddText(root, "Relics", 42, TextAnchor.UpperLeft, Color.white,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -25f), new Vector2(700f, 60f));
        hintText = AddText(root, "Hint", 28, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -80f), new Vector2(1100f, 50f));
        hintText.text = "Encuentra las reliquias doradas (brillan en la oscuridad)";
        timerText = AddText(root, "Timer", 46, TextAnchor.UpperRight, Color.white,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -25f), new Vector2(400f, 60f));
        alertText = AddText(root, "Alert", 52, TextAnchor.UpperCenter, Color.red,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1000f, 80f));

        // Barra de resistencia
        var staminaBg = AddImage(root, "StaminaBg", new Color(0f, 0f, 0f, 0.5f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(420f, 16f));
        var fill = AddImage(staminaBg.transform, "StaminaFill", new Color(0.3f, 0.8f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        staminaFill = fill.rectTransform;
        staminaFill.anchorMin = Vector2.zero;
        staminaFill.anchorMax = Vector2.one;
        staminaFill.offsetMin = Vector2.zero;
        staminaFill.offsetMax = Vector2.zero;

        // Mira
        AddImage(root, "Crosshair", new Color(1f, 1f, 1f, 0.6f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));

        // Panel técnico (F1)
        var debugBg = AddImage(root, "DebugPanel", new Color(0f, 0f, 0f, 0.55f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -95f), new Vector2(620f, 130f));
        debugBg.rectTransform.pivot = new Vector2(1f, 1f);
        debugText = AddText(debugBg.transform, "DebugText", 24, TextAnchor.UpperLeft, new Color(1f, 1f, 0.6f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600f, 120f));
        debugPanel = debugBg.gameObject;
        debugPanel.SetActive(false);

        // Pantalla de menú / victoria / derrota
        var ov = AddImage(root, "Overlay", new Color(0f, 0f, 0f, 0.82f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ov.rectTransform.anchorMin = Vector2.zero;
        ov.rectTransform.anchorMax = Vector2.one;
        ov.rectTransform.offsetMin = Vector2.zero;
        ov.rectTransform.offsetMax = Vector2.zero;
        overlay = ov.gameObject;

        titleText = AddText(overlay.transform, "Title", 96, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 190f), new Vector2(1600f, 150f));
        subText = AddText(overlay.transform, "Sub", 34, TextAnchor.UpperCenter, new Color(0.9f, 0.92f, 1f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1500f, 500f));
        subText.rectTransform.pivot = new Vector2(0.5f, 1f);
    }

    Text AddText(Transform parent, string name, int size, TextAnchor align, Color color,
                 Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.fontStyle = FontStyle.Bold;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    Image AddImage(Transform parent, string name, Color color,
                   Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Font LoadFont()
    {
        try { return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        try { return Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        return Font.CreateDynamicFontFromOSFont("Arial", 16);
    }
}
