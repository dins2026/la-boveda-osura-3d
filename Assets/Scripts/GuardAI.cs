using UnityEngine;

/// <summary>
/// IA del guardia con máquina de estados (3 comportamientos):
///   Patrol -> recorre waypoints.
///   Chase  -> persigue al jugador si lo VE (ángulo + distancia + Raycast sin obstáculos) o lo OYE (ruido).
///   Search -> va a la última posición conocida, mira alrededor y vuelve a patrullar.
/// Optimización: la percepción (Raycast) se calcula cada 'thinkInterval' segundos, no en cada frame.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class GuardAI : MonoBehaviour
{
    public enum State { Patrol, Chase, Search }
    public static event System.Action<State> OnGuardStateChanged;

    [Header("Referencias (las asigna GameBootstrap)")]
    public Transform[] waypoints;
    public int startWaypoint = 0;
    public Transform player;
    public Renderer bodyRenderer;
    public Renderer visorRenderer;
    public Light flashlight;

    [Header("Velocidades")]
    public float patrolSpeed = 2.2f;
    public float searchSpeed = 3.2f;
    public float chaseSpeed = 5.4f;   // mayor que caminar (5) pero menor que correr (8)

    [Header("Percepción")]
    public float viewDistance = 15f;
    public float viewHalfAngle = 60f;
    public float catchDistance = 1.4f;
    public float thinkInterval = 0.1f;

    [Header("Patrulla y búsqueda")]
    public float waypointTolerance = 1.2f;
    public float waitAtWaypoint = 1.2f;
    public float searchTime = 7f;

    public State Current { get; private set; }

    CharacterController controller;
    PlayerController playerController;
    int wpIndex;
    float thinkTimer, waitTimer, searchTimer;
    Vector3 lastKnownPosition;
    Vector3 steerDir;

    static readonly Color PatrolColor = new Color(0.25f, 0.45f, 1f);
    static readonly Color SearchColor = new Color(1f, 0.75f, 0.1f);
    static readonly Color ChaseColor = new Color(1f, 0.1f, 0.1f);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Current = State.Patrol;
    }

    void Start()
    {
        playerController = player.GetComponent<PlayerController>();
        wpIndex = waypoints.Length > 0 ? (startWaypoint + 1) % waypoints.Length : 0;
        ApplyVisuals();
        if (OnGuardStateChanged != null) OnGuardStateChanged(Current);
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.State != GameState.Playing || player == null) return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = thinkInterval;
            Perceive();
        }

        switch (Current)
        {
            case State.Patrol: UpdatePatrol(); break;
            case State.Chase: UpdateChase(gm); break;
            case State.Search: UpdateSearch(); break;
        }
    }

    // ---------- PERCEPCIÓN ----------
    void Perceive()
    {
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        Vector3 toPlayer = target - eye;
        float dist = toPlayer.magnitude;
        Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);

        // VISTA: dentro del cono y sin obstáculos en el medio (Raycast)
        bool sees = false;
        if (dist > 0.01f && dist <= viewDistance && Vector3.Angle(transform.forward, flat) <= viewHalfAngle)
        {
            RaycastHit hit;
            if (Physics.Raycast(eye, toPlayer / dist, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                sees = hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        // OÍDO: correr hace más ruido que caminar
        bool hears = flat.magnitude <= playerController.NoiseRadius;

        if (sees || hears)
        {
            lastKnownPosition = player.position;
            if (Current != State.Chase) ChangeState(State.Chase);
        }
        else if (Current == State.Chase)
        {
            ChangeState(State.Search);
        }

        // Dirección de movimiento (con esquiva simple de obstáculos)
        Vector3 goal;
        if (Current == State.Chase) goal = player.position;
        else if (Current == State.Search) goal = lastKnownPosition;
        else goal = waypoints.Length > 0 ? waypoints[wpIndex].position : transform.position;

        Vector3 desired = goal - transform.position;
        desired.y = 0f;
        steerDir = desired.sqrMagnitude > 0.01f ? Steer(desired.normalized) : Vector3.zero;
    }

    // ---------- COMPORTAMIENTOS ----------
    void UpdatePatrol()
    {
        if (waypoints.Length == 0) return;

        Vector3 to = waypoints[wpIndex].position - transform.position;
        to.y = 0f;
        if (to.magnitude <= waypointTolerance)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                wpIndex = (wpIndex + 1) % waypoints.Length;
                waitTimer = waitAtWaypoint;
            }
            return;
        }
        waitTimer = waitAtWaypoint;
        MoveAlong(steerDir, patrolSpeed);
    }

    void UpdateChase(GameManager gm)
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.magnitude <= catchDistance)
        {
            gm.Lose("¡El guardia te atrapó!");
            return;
        }
        MoveAlong(steerDir, chaseSpeed);
    }

    void UpdateSearch()
    {
        searchTimer -= Time.deltaTime;
        Vector3 to = lastKnownPosition - transform.position;
        to.y = 0f;

        if (to.magnitude > 1.2f) MoveAlong(steerDir, searchSpeed);
        else transform.Rotate(0f, 80f * Time.deltaTime, 0f); // mira alrededor

        if (searchTimer <= 0f) ChangeState(State.Patrol);
    }

    void MoveAlong(Vector3 dir, float speed)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
        controller.SimpleMove(dir * speed);
    }

    // ---------- ESQUIVA DE OBSTÁCULOS ----------
    Vector3 Steer(Vector3 desired)
    {
        Vector3 origin = transform.position + Vector3.up * 0.9f;
        if (!IsBlocked(origin, desired)) return desired;

        for (int angle = 30; angle <= 150; angle += 30)
        {
            Vector3 left = Quaternion.Euler(0f, -angle, 0f) * desired;
            if (!IsBlocked(origin, left)) return left;
            Vector3 right = Quaternion.Euler(0f, angle, 0f) * desired;
            if (!IsBlocked(origin, right)) return right;
        }
        return desired;
    }

    bool IsBlocked(Vector3 origin, Vector3 dir)
    {
        RaycastHit hit;
        if (Physics.SphereCast(origin, 0.35f, dir, out hit, 1.8f, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<PlayerController>() == null;
        return false;
    }

    // ---------- CAMBIOS DE ESTADO ----------
    void ChangeState(State next)
    {
        Current = next;
        var audio = AudioManager.Instance;

        switch (next)
        {
            case State.Chase:
                if (audio != null) { audio.PlayAlert(); audio.SetTension(1f); }
                break;
            case State.Search:
                searchTimer = searchTime;
                if (audio != null) audio.SetTension(0.4f);
                break;
            case State.Patrol:
                wpIndex = NearestWaypoint();
                waitTimer = 0f;
                if (audio != null) audio.SetTension(0f);
                break;
        }

        ApplyVisuals();
        if (OnGuardStateChanged != null) OnGuardStateChanged(next);
    }

    int NearestWaypoint()
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float d = (waypoints[i].position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    void ApplyVisuals()
    {
        Color c = Current == State.Chase ? ChaseColor : (Current == State.Search ? SearchColor : PatrolColor);
        if (bodyRenderer != null) bodyRenderer.material.color = new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f, 1f);
        if (visorRenderer != null) GameUtils.SetEmission(visorRenderer.material, new Color(c.r * 2f, c.g * 2f, c.b * 2f, 1f));
        if (flashlight != null) flashlight.color = Current == State.Patrol ? new Color(1f, 0.95f, 0.8f) : c;
    }
}
