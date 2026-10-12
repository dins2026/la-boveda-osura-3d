using UnityEngine;

public class ExitDoor : MonoBehaviour
{
    public Transform door;
    public Renderer doorRenderer;
    public Light exitLight;

    public bool Unlocked { get; private set; }

    Vector3 closedPos, openPos;
    float t;
    bool opening;

    void Start()
    {
        closedPos = door.position;
        openPos = closedPos + Vector3.down * 4.3f;
        GameManager.Instance.OnAllRelicsCollected += Unlock;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnAllRelicsCollected -= Unlock;
    }

    void Unlock()
    {
        if (Unlocked) return;
        Unlocked = true;
        opening = true;

        AudioManager.Instance.PlayUnlock();
        EffectsPool.Instance.Play(door.position + Vector3.up * 1.5f, Color.green);

        Color green = new Color(0.1f, 1f, 0.2f);
        doorRenderer.material.color = green;
        GameUtils.SetEmission(doorRenderer.material, new Color(0.1f, 0.8f, 0.2f));
        if (exitLight != null) exitLight.color = green;
    }

    void Update()
    {
        if (!opening) return;
        t += Time.deltaTime / 2f;
        door.position = Vector3.Lerp(closedPos, openPos, Mathf.SmoothStep(0f, 1f, t));
        if (t >= 1f) opening = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Unlocked) return;
        if (other.GetComponent<PlayerController>() == null) return;
        GameManager.Instance.Win();
    }
}
