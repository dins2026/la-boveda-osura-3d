using UnityEngine;

/// <summary>
/// Reliquia coleccionable (mecánica principal). Gira y flota; al tocarla el jugador
/// suma al contador, suena un efecto y sale una explosión de partículas (del pool).
/// </summary>
public class Relic : MonoBehaviour
{
    public float spinSpeed = 90f;
    public float bobAmount = 0.15f;
    public float bobSpeed = 2f;

    Vector3 startPos;
    float phase;
    bool collected;

    void Start()
    {
        startPos = transform.position;
        phase = Random.value * 6f;
    }

    void Update()
    {
        if (collected) return;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount;
    }

    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<PlayerController>() == null) return;

        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing) return;

        collected = true;
        EffectsPool.Instance.Play(transform.position, new Color(1f, 0.85f, 0.2f));
        AudioManager.Instance.PlayPickup();
        gm.CollectRelic();
        gameObject.SetActive(false);
    }
}
