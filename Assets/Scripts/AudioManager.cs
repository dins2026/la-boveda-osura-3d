using UnityEngine;

/// <summary>
/// Audio 100% procedural: todos los sonidos se generan por código (no hay archivos de audio).
///  - Ambiente: drone grave en loop.
///  - Tensión: capa que sube de volumen cuando el guardia te persigue.
///  - Efectos: recoger reliquia, alerta, pasos, puerta, victoria y derrota.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    const int SampleRate = 44100;
    const float TwoPi = Mathf.PI * 2f;
    static readonly float[] WinNotes = { 523.25f, 659.25f, 783.99f, 1046.5f };

    AudioSource ambientSrc, tensionSrc, sfxSrc, stepSrc;
    AudioClip pickupClip, alertClip, winClip, loseClip, stepClip, unlockClip;
    float tensionTarget;

    void Awake()
    {
        Instance = this;

        ambientSrc = CreateSource(true, 0.45f);
        ambientSrc.clip = Make("ambient", 4f, t =>
            (0.30f * Mathf.Sin(TwoPi * 55f * t)
            + 0.22f * Mathf.Sin(TwoPi * 82.5f * t) * (0.65f + 0.35f * Mathf.Sin(TwoPi * 0.25f * t))
            + 0.08f * Mathf.Sin(TwoPi * 164f * t) * (0.5f + 0.5f * Mathf.Sin(TwoPi * 0.5f * t))
            + (Random.value * 2f - 1f) * 0.02f) * 0.8f);
        ambientSrc.Play();

        tensionSrc = CreateSource(true, 0f);
        tensionSrc.clip = Make("tension", 2f, t =>
            (Mathf.Sin(TwoPi * 110f * t) * 0.5f + Mathf.Sin(TwoPi * 116f * t) * 0.35f)
            * (0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 2f * t))) * 0.8f);
        tensionSrc.Play();

        sfxSrc = CreateSource(false, 1f);
        stepSrc = CreateSource(false, 1f);

        pickupClip = Make("pickup", 0.5f, t =>
        {
            float f = t < 0.12f ? 880f : 1320f;
            return Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-5f * t) * 0.6f;
        });

        alertClip = Make("alert", 0.6f, t =>
        {
            float phase = TwoPi * (700f * t - 300f * t * t);
            return Mathf.Sign(Mathf.Sin(phase)) * 0.3f * (1f - t / 0.6f);
        });

        winClip = Make("win", 1.8f, t =>
        {
            int idx = Mathf.Min((int)(t / 0.22f), 3);
            float lt = t - idx * 0.22f;
            return Mathf.Sin(TwoPi * WinNotes[idx] * t) * Mathf.Exp(-3f * lt) * 0.5f;
        });

        loseClip = Make("lose", 1.4f, t =>
            Mathf.Sin(TwoPi * (180f * t - 60f * t * t)) * Mathf.Exp(-2.2f * t) * 0.6f
            + Mathf.Sin(TwoPi * 90f * t) * Mathf.Exp(-2f * t) * 0.4f);

        unlockClip = Make("unlock", 1.2f, t =>
            Mathf.Sin(TwoPi * (70f * t + 80f * t * t)) * Mathf.Min(1f, t * 8f) * Mathf.Exp(-1.5f * t) * 0.7f
            + Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-4f * t) * 0.3f);

        stepClip = Make("step", 0.14f, t =>
            (Random.value * 2f - 1f) * Mathf.Exp(-35f * t) * 0.35f
            + Mathf.Sin(TwoPi * 80f * t) * Mathf.Exp(-30f * t) * 0.5f);

        GameManager.Instance.OnStateChanged += HandleState;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnStateChanged -= HandleState;
    }

    void Update()
    {
        tensionSrc.volume = Mathf.MoveTowards(tensionSrc.volume, tensionTarget * 0.6f, Time.deltaTime * 0.9f);
    }

    void HandleState(GameState s)
    {
        if (s == GameState.Won) { PlayWin(); SetTension(0f); }
        else if (s == GameState.Lost) { PlayLose(); SetTension(0f); }
    }

    // ---------- API pública ----------
    public void SetTension(float value) { tensionTarget = Mathf.Clamp01(value); }
    public void PlayPickup() { sfxSrc.PlayOneShot(pickupClip, 0.8f); }
    public void PlayAlert() { sfxSrc.PlayOneShot(alertClip, 0.7f); }
    public void PlayWin() { sfxSrc.PlayOneShot(winClip, 0.9f); }
    public void PlayLose() { sfxSrc.PlayOneShot(loseClip, 0.9f); }
    public void PlayUnlock() { sfxSrc.PlayOneShot(unlockClip, 0.9f); }

    public void PlayFootstep()
    {
        stepSrc.pitch = Random.Range(0.85f, 1.15f);
        stepSrc.PlayOneShot(stepClip, 0.5f);
    }

    // ---------- Generación de sonidos ----------
    AudioSource CreateSource(bool loop, float volume)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop = loop;
        src.volume = volume;
        src.spatialBlend = 0f;
        src.playOnAwake = false;
        return src;
    }

    static AudioClip Make(string name, float seconds, System.Func<float, float> f)
    {
        int n = Mathf.CeilToInt(seconds * SampleRate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(f(i / (float)SampleRate), -1f, 1f);

        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
