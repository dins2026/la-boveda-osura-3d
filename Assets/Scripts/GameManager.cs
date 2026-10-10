using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Won, Lost }

/// <summary>
/// Controla el flujo del juego: Menú -> Jugando -> Victoria / Derrota.
/// Guarda las reliquias recolectadas y el tiempo restante.
/// Los demás sistemas (UI, audio, puerta) se suscriben a sus eventos.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    static bool skipMenuOnReload;

    [Header("Reglas")]
    public float timeLimit = 240f; // 4 minutos

    public GameState State { get; private set; } = GameState.Menu;
    public int RelicsCollected { get; private set; }
    public int RelicsTotal { get; set; }
    public float TimeLeft { get; private set; }
    public string EndReason { get; private set; } = "";

    public event Action<GameState> OnStateChanged;
    public event Action<int, int> OnRelicsChanged;
    public event Action OnAllRelicsCollected;

    void Awake()
    {
        Instance = this;
        TimeLeft = timeLimit;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (skipMenuOnReload)
        {
            skipMenuOnReload = false;
            StartGame();
        }
        else
        {
            SetState(GameState.Menu);
        }
    }

    void Update()
    {
        switch (State)
        {
            case GameState.Menu:
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) StartGame();
                if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
                break;

            case GameState.Playing:
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    Lose("Se acabó el tiempo.");
                }
                break;

            default: // Won o Lost
                if (Input.GetKeyDown(KeyCode.R)) Restart();
                if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
                break;
        }
    }

    public void StartGame()
    {
        SetState(GameState.Playing);
    }

    public void CollectRelic()
    {
        if (State != GameState.Playing) return;
        RelicsCollected++;
        OnRelicsChanged?.Invoke(RelicsCollected, RelicsTotal);
        if (RelicsCollected >= RelicsTotal) OnAllRelicsCollected?.Invoke();
    }

    public void Win()
    {
        if (State != GameState.Playing) return;
        EndReason = "Escapaste con todas las reliquias.";
        SetState(GameState.Won);
    }

    public void Lose(string reason)
    {
        if (State != GameState.Playing) return;
        EndReason = reason;
        SetState(GameState.Lost);
    }

    public void Restart()
    {
        skipMenuOnReload = true; // al reiniciar se salta el menú
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void SetState(GameState next)
    {
        State = next;
        bool playing = next == GameState.Playing;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
        OnStateChanged?.Invoke(next);
    }
}
