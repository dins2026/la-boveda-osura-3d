using UnityEngine;

/// <summary>
/// Utilidades para crear materiales y texturas por código (así el proyecto no necesita assets externos).
/// </summary>
public static class GameUtils
{
    static Material baseMaterial;

    // Tomamos el material por defecto de una primitiva: funciona en Built-in y en URP
    // y evita el problema de "shaders rosados" en el Build.
    static Material GetBase()
    {
        if (baseMaterial == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMaterial = tmp.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(tmp);
        }
        return baseMaterial;
    }

    public static Material NewMaterial(Color color, Texture2D tex = null, Vector2 tiling = default(Vector2), float smoothness = 0.1f)
    {
        var m = new Material(GetBase());
        m.color = color;
        if (tex != null)
        {
            m.mainTexture = tex;
            m.mainTextureScale = tiling == Vector2.zero ? Vector2.one : tiling;
        }
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    public static void SetEmission(Material m, Color c)
    {
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c);
    }

    public static Material NewParticleMaterial(Texture2D tex)
    {
        var shader = Shader.Find("Sprites/Default");
        var m = shader != null ? new Material(shader) : new Material(GetBase());
        m.mainTexture = tex;
        return m;
    }

    /// <summary>Textura procedural de ladrillos o baldosas (64x64) con ruido.</summary>
    public static Texture2D MakePatternTexture(Color baseCol, Color lineCol, int cellW, int cellH, bool offsetRows, float noise = 0.05f)
    {
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
        for (int y = 0; y < S; y++)
        {
            int row = y / cellH;
            int shift = (offsetRows && row % 2 == 1) ? cellW / 2 : 0;
            for (int x = 0; x < S; x++)
            {
                int xx = (x + shift) % S;
                bool line = (y % cellH) < 2 || (xx % cellW) < 2;
                Color c = line ? lineCol : baseCol;
                float n = Random.Range(-noise, noise);
                tex.SetPixel(x, y, new Color(c.r + n, c.g + n, c.b + n, 1f));
            }
        }
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply(true);
        return tex;
    }

    /// <summary>Punto suave (blanco con alfa radial) para las partículas.</summary>
    public static Texture2D MakeDotTexture()
    {
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f + 0.5f) / (S / 2f);
                float dy = (y - S / 2f + 0.5f) / (S / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.Apply();
        return tex;
    }
}
