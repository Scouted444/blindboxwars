// Blind Box Wars - core helpers: art-bible palette, input wrapper, materials, primitives, audio, bootstrap.
//
// ART BIBLE
//  Style:   Toy-box suburb - flat-shaded candy pastels on plum asphalt, chunky oversized primitives.
//  Palette: see BBPal below; every colour in the game reads from it.
//  Mood:    Warm late-morning sun with cool lilac shadow bounce; at night the windows and lamps carry the frame.
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class BBPal
{
    public static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }

    public static readonly Color Asphalt = Hex("#3B3546");
    public static readonly Color Lane = Hex("#F6D78B");
    public static readonly Color Sidewalk = Hex("#D9CFC1");
    public static readonly Color Grass = Hex("#8FCB8A");
    public static readonly Color Bubblegum = Hex("#F2A7B5");
    public static readonly Color Mint = Hex("#9FD8CB");
    public static readonly Color Butter = Hex("#F6D78B");
    public static readonly Color Lilac = Hex("#B7A6E8");
    public static readonly Color Peach = Hex("#F7B98B");
    public static readonly Color Roof = Hex("#6D5A7E");
    public static readonly Color Window = Hex("#2E2840");
    public static readonly Color WindowLit = Hex("#FFD98A");
    public static readonly Color Leaf = Hex("#5FB37A");
    public static readonly Color Trunk = Hex("#7A5236");
    public static readonly Color Accent = Hex("#FF4FA0");
    public static readonly Color Cop = Hex("#2C3E8F");
    public static readonly Color SkyDay = Hex("#A9D4F5");
    public static readonly Color SkyNight = Hex("#1B1830");
    public static readonly Color Sun = Hex("#FFF2DC");
    public static readonly Color Moon = Hex("#8EA6E0");
    public static readonly Color Panel = Hex("#241A33");
    public static readonly Color Ink = Hex("#FFF3FA");
    public static readonly Color Muted = Hex("#B9A8CC");
    public static readonly Color Good = Hex("#7BE0B0");
    public static readonly Color Warn = Hex("#FFC857");
    public static readonly Color Bad = Hex("#FF5A6E");
    public static readonly Color Wood = Hex("#A87B54");
    public static readonly Color Steel = Hex("#8C8A99");
    public static readonly Color Concrete = Hex("#BDB3C7");
    public static readonly Color Water = Hex("#7FC8F0");
    public static readonly Color[] Walls = { Bubblegum, Mint, Butter, Lilac, Peach };
    public static readonly Color[] Skin = { Hex("#F3C9A8"), Hex("#D9A07A"), Hex("#A8714F"), Hex("#6E4630"), Hex("#FFE0C7") };
    public static readonly Color[] Clothes = { Hex("#E8556D"), Hex("#4FB0C6"), Hex("#F2A541"), Hex("#7D5BA6"), Hex("#59A96A"), Hex("#EDE6F2"), Hex("#3D3D4E") };
}

public static class BBInp
{
#if ENABLE_INPUT_SYSTEM
    static Keyboard K { get { return Keyboard.current; } }
    public static Vector2 Move()
    {
        var k = K; if (k == null) return Vector2.zero;
        return new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0),
                           (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
    }
    public static Vector2 Look() { var m = Mouse.current; return m == null ? Vector2.zero : m.delta.ReadValue() * 0.08f; }
    public static bool Sprint { get { return K != null && K.leftShiftKey.isPressed; } }
    public static bool Jump { get { return K != null && K.spaceKey.wasPressedThisFrame; } }
    public static bool Interact { get { return K != null && K.eKey.wasPressedThisFrame; } }
    public static bool Phone { get { return K != null && K.tabKey.wasPressedThisFrame; } }
    public static bool Esc { get { return K != null && K.escapeKey.wasPressedThisFrame; } }
#else
    public static Vector2 Move() { return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); }
    public static Vector2 Look() { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f; }
    public static bool Sprint { get { return Input.GetKey(KeyCode.LeftShift); } }
    public static bool Jump { get { return Input.GetKeyDown(KeyCode.Space); } }
    public static bool Interact { get { return Input.GetKeyDown(KeyCode.E); } }
    public static bool Phone { get { return Input.GetKeyDown(KeyCode.Tab); } }
    public static bool Esc { get { return Input.GetKeyDown(KeyCode.Escape); } }
#endif
}

public static class BB
{
    static Material baseMat;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

    static Material Base()
    {
        if (baseMat == null)
        {
            // Borrow the active render pipeline's default lit material so this works in URP and Built-in.
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseMat = g.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(g);
        }
        return baseMat;
    }

    public static Material Mat(Color c, float emit = 0f)
    {
        string key = ColorUtility.ToHtmlStringRGBA(c) + "_" + emit;
        Material m;
        if (cache.TryGetValue(key, out m)) return m;
        m = NewMat(c);
        if (emit > 0f) SetEmit(m, c * emit);
        cache[key] = m;
        return m;
    }

    public static Material NewMat(Color c)
    {
        var m = new Material(Base());
        SetCol(m, c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.18f);
        return m;
    }

    public static void SetCol(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    public static void SetEmit(Material m, Color e)
    {
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e);
    }

    public static GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider = true, string name = null)
    {
        var g = GameObject.CreatePrimitive(t);
        if (name != null) g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.Destroy(g.GetComponent<Collider>());
        return g;
    }

    public static GameObject Box(Vector3 pos, Vector3 size, Color c, Transform parent, bool collider = true, string name = null)
    {
        return Prim(PrimitiveType.Cube, pos, size, Mat(c), parent, collider, name);
    }

    public static void Sign(string text, Vector3 pos, float yaw, float size, Color c, Transform parent)
    {
        var g = new GameObject("Sign " + text);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var tm = g.AddComponent<TextMesh>();
        tm.text = text; tm.fontSize = 64; tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.color = c; tm.fontStyle = FontStyle.Bold;
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        if (f != null) { tm.font = f; g.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
    }

    public static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.DontSave; return t;
    }

    // Tiny procedural sound effects so the game isn't silent.
    public static AudioClip Tone(string name, float[] freqs, float noteLen, float vol, bool square = false)
    {
        int rate = 44100;
        int per = (int)(rate * noteLen);
        var data = new float[per * freqs.Length];
        for (int n = 0; n < freqs.Length; n++)
            for (int i = 0; i < per; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Min(1f, i / 300f) * Mathf.Clamp01(1f - (float)i / per);
                float s = Mathf.Sin(2 * Mathf.PI * freqs[n] * t);
                if (square) s = Mathf.Sign(s) * 0.6f;
                data[n * per + i] = s * env * vol;
            }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static string Money(int v) { return "$" + v.ToString("N0"); }
}

public static class BBBoot
{
    // Set to false if you drop these scripts into a project where you don't want the game to auto-start.
    public static bool AutoStart = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!AutoStart) return;
        if (Object.FindFirstObjectByType<BBGame>() != null) return;
        new GameObject("Blind Box Wars").AddComponent<BBGame>();
    }
}
