using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ObjectPool
{
    readonly Queue<GameObject> available = new Queue<GameObject>();
    readonly Func<GameObject> factory;
    readonly Transform parent;

    public int TotalCreated { get; private set; }
    public int Available { get { return available.Count; } }

    public ObjectPool(Func<GameObject> factory, int prewarm, Transform parent)
    {
        this.factory = factory;
        this.parent = parent;
        for (int i = 0; i < prewarm; i++) Release(Create());
    }

    GameObject Create()
    {
        var go = factory();
        go.transform.SetParent(parent, false);
        TotalCreated++;
        return go;
    }

    public GameObject Get()
    {
        var go = available.Count > 0 ? available.Dequeue() : Create();
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        go.SetActive(false);
        available.Enqueue(go);
    }
}

/// <summary>
/// Efectos visuales: explosión de partículas (pooled) y polvo ambiental.
/// </summary>
public class EffectsPool : MonoBehaviour
{
    public static EffectsPool Instance { get; private set; }

    ObjectPool pool;
    Material particleMat;

    public int Created { get { return pool.TotalCreated; } }
    public int Available { get { return pool.Available; } }

    void Awake()
    {
        Instance = this;
        particleMat = GameUtils.NewParticleMaterial(GameUtils.MakeDotTexture());
        pool = new ObjectPool(CreateBurst, 4, transform);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- Explosión de partículas (con pool) ----------
    public void Play(Vector3 position, Color color)
    {
        var go = pool.Get();
        go.transform.position = position;
        var ps = go.GetComponent<ParticleSystem>();
        var main = ps.main;
        main.startColor = color;
        ps.Play();
        StartCoroutine(ReleaseAfter(go, 1.6f));
    }

    IEnumerator ReleaseAfter(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        go.GetComponent<ParticleSystem>().Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        pool.Release(go);
    }

    GameObject CreateBurst()
    {
        var go = new GameObject("BurstFX");
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 1.0f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = 0.22f;
        main.gravityModifier = 0.4f;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)40) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        go.GetComponent<ParticleSystemRenderer>().material = particleMat;
        go.SetActive(false);
        return go;
    }

    // ---------- Polvo ambiental ----------
    public void CreateDust(Vector3 center, Vector3 area)
    {
        var go = new GameObject("AmbientDust");
        go.transform.position = center;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.prewarm = true;
        main.duration = 10f;
        main.startLifetime = 14f;
        main.startSpeed = 0.1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        main.startColor = new Color(0.6f, 0.7f, 1f, 0.35f);
        main.maxParticles = 200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 14f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = area;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.25f;

        go.GetComponent<ParticleSystemRenderer>().material = particleMat;
        ps.Play();
    }
}
