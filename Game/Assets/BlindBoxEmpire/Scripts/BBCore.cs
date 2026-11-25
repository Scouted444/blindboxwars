// Blind Box Wars - core helpers: art-bible palette, input wrapper, low-poly mesh builder, materials, audio, bootstrap.
//
// ART BIBLE
//  Style:   Low-poly stylised suburb (Schedule-I-like): faceted flat-shaded meshes, chunky people with big expressive faces,
//           soft-banded sun, long shadows, light bloom.
//  Palette: BBPal below - every colour in the game reads from it.
//  Mood:    Warm late-afternoon sun with cool lilac sky bounce; at night windows, lamps and headlights carry the frame.
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class BBPal
{
    public static Color Hex(string h) { Color c; ColorUtility.TryParseHtmlString(h, out c); return c; }
    public static Color Dim(Color c, float k) { return BB.Dim(c, k); }
    public static Color Lamp(Color c) { return BB.Lamp(c); }

    public static readonly Color Asphalt = Hex("#45414F");
    public static readonly Color AsphaltDark = Hex("#3A3644");
    public static readonly Color Lane = Hex("#F3D37A");
    public static readonly Color Paint = Hex("#ECE8F0");
    public static readonly Color Sidewalk = Hex("#C9C2BC");
    public static readonly Color Curb = Hex("#A8A19C");
    public static readonly Color Grass = Hex("#7DB86A");
    public static readonly Color GrassDark = Hex("#6AA65A");
    public static readonly Color Bubblegum = Hex("#F2A7B5");
    public static readonly Color Mint = Hex("#9FD8CB");
    public static readonly Color Butter = Hex("#F6D78B");
    public static readonly Color Lilac = Hex("#B7A6E8");
    public static readonly Color Peach = Hex("#F7B98B");
    public static readonly Color Cream = Hex("#EFE6D2");
    public static readonly Color SkyBlueWall = Hex("#A9C8E8");
    public static readonly Color Brick = Hex("#B5654F");
    public static readonly Color Roof = Hex("#6D5A7E");
    public static readonly Color[] Roofs = { Hex("#8E4B45"), Hex("#4F5A6E"), Hex("#6B5344"), Hex("#3F6B66"), Hex("#6D5A7E") };
    public static readonly Color Trim = Hex("#F7F3EC");
    public static readonly Color Glass = Hex("#41506A");
    public static readonly Color WindowLit = Hex("#FFD98A");
    public static readonly Color Leaf = Hex("#5FA86A");
    public static readonly Color LeafDark = Hex("#4C8F58");
    public static readonly Color Pine = Hex("#3F7A55");
    public static readonly Color Trunk = Hex("#7A5236");
    public static readonly Color Accent = Hex("#FF4FA0");
    public static readonly Color Cop = Hex("#2C3E8F");
    public static readonly Color SkyDay = Hex("#8EC3EE");
    public static readonly Color SkyHorizon = Hex("#E6EEF2");
    public static readonly Color SkyNight = Hex("#141228");
    public static readonly Color SkyNightHorizon = Hex("#2B2A4A");
    public static readonly Color Sunset = Hex("#FF9F6B");
    public static readonly Color Sun = Hex("#FFF0D6");
    public static readonly Color Moon = Hex("#8EA6E0");
    public static readonly Color Mountain = Hex("#6F8E86");
    public static readonly Color MountainFar = Hex("#8FA6B4");
    public static readonly Color Panel = Hex("#241A33");
    public static readonly Color Ink = Hex("#FFF3FA");
    public static readonly Color Muted = Hex("#B9A8CC");
    public static readonly Color Good = Hex("#7BE0B0");
    public static readonly Color Warn = Hex("#FFC857");
    public static readonly Color Bad = Hex("#FF5A6E");
    public static readonly Color Wood = Hex("#A87B54");
    public static readonly Color WoodDark = Hex("#7C583B");
    public static readonly Color Steel = Hex("#8C8A99");
    public static readonly Color SteelDark = Hex("#55535F");
    public static readonly Color Concrete = Hex("#BDB6B0");
    public static readonly Color Tire = Hex("#26242C");
    public static readonly Color Water = Hex("#7FC8F0");
    public static readonly Color[] Walls = { Bubblegum, Mint, Butter, Lilac, Peach, Cream, SkyBlueWall };
    public static readonly Color[] CarPaint = { Hex("#D9493F"), Hex("#3F6FD9"), Hex("#E8E4DA"), Hex("#2E2C35"), Hex("#58A86B"), Hex("#E8B83F"), Hex("#8E8A96"), Hex("#7A3F9E") };
    public static readonly Color[] Skin = { Hex("#F3C9A8"), Hex("#E0AC85"), Hex("#C08660"), Hex("#8D5A3B"), Hex("#5E3B27"), Hex("#FFE0C7") };
    public static readonly Color[] Hair = { Hex("#2B2024"), Hex("#5A3A28"), Hex("#A0673A"), Hex("#E2C27A"), Hex("#B8B3B0"), Hex("#C4473F"), Hex("#E989B5") };
    public static readonly Color[] Clothes = { Hex("#E8556D"), Hex("#4FB0C6"), Hex("#F2A541"), Hex("#7D5BA6"), Hex("#59A96A"), Hex("#EDE6F2"), Hex("#3D3D4E"), Hex("#D96F3F"), Hex("#3F7FBF") };
    public static readonly Color[] Pants = { Hex("#36435E"), Hex("#2E2C35"), Hex("#7A6A55"), Hex("#4F6B8E"), Hex("#5A4A6E") };
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
    public static bool Rotate { get { return K != null && K.rKey.wasPressedThisFrame; } }
    public static bool Brake { get { return K != null && K.spaceKey.isPressed; } }
    public static bool Drop { get { return K != null && K.gKey.wasPressedThisFrame; } }
    public static float Scroll() { var m = Mouse.current; return m == null ? 0f : Mathf.Sign(m.scroll.ReadValue().y) * (Mathf.Abs(m.scroll.ReadValue().y) > 0.01f ? 1 : 0); }
    public static int SlotKey()
    {
        var k = K; if (k == null) return -1;
        if (k.digit1Key.wasPressedThisFrame) return 0; if (k.digit2Key.wasPressedThisFrame) return 1;
        if (k.digit3Key.wasPressedThisFrame) return 2; if (k.digit4Key.wasPressedThisFrame) return 3;
        if (k.digit5Key.wasPressedThisFrame) return 4; if (k.digit6Key.wasPressedThisFrame) return 5;
        if (k.digit7Key.wasPressedThisFrame) return 6; if (k.digit8Key.wasPressedThisFrame) return 7;
        return -1;
    }
#else
    public static Vector2 Move() { return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")); }
    public static Vector2 Look() { return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f; }
    public static bool Sprint { get { return Input.GetKey(KeyCode.LeftShift); } }
    public static bool Jump { get { return Input.GetKeyDown(KeyCode.Space); } }
    public static bool Interact { get { return Input.GetKeyDown(KeyCode.E); } }
    public static bool Phone { get { return Input.GetKeyDown(KeyCode.Tab); } }
    public static bool Esc { get { return Input.GetKeyDown(KeyCode.Escape); } }
    public static bool Rotate { get { return Input.GetKeyDown(KeyCode.R); } }
    public static bool Brake { get { return Input.GetKey(KeyCode.Space); } }
    public static bool Drop { get { return Input.GetKeyDown(KeyCode.G); } }
    public static float Scroll() { float y = Input.mouseScrollDelta.y; return Mathf.Abs(y) > 0.01f ? Mathf.Sign(y) : 0f; }
    public static int SlotKey()
    {
        for (int i = 0; i < 8; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return i;
        return -1;
    }
#endif
}

// Flat-shaded low-poly mesh builder. Every face gets its own vertices so the model reads faceted.
// All shapes go through M (a TRS) so a whole house can be authored in local space and placed/rotated.
public class BBMesh
{
    readonly List<Vector3> v = new List<Vector3>();
    readonly List<Vector3> n = new List<Vector3>();
    readonly List<Color> c = new List<Color>();
    readonly List<int> t = new List<int>();
    public Matrix4x4 M = Matrix4x4.identity;
    public int VertexCount { get { return v.Count; } }

    public void SetFrame(Vector3 pos, float yaw) { M = Matrix4x4.TRS(pos, Quaternion.Euler(0, yaw, 0), Vector3.one); }
    public void SetFrame(Matrix4x4 m) { M = m; }

    Vector3 P(Vector3 p) { return M.MultiplyPoint3x4(p); }

    // Quad from four local points. Winding is fixed automatically so the face points along 'outward'.
    public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, Vector3 outward)
    {
        a = P(a); b = P(b); cc = P(cc); d = P(d);
        outward = M.MultiplyVector(outward);
        Vector3 nn = Vector3.Cross(b - a, cc - a);
        if (nn.sqrMagnitude < 1e-12f) nn = Vector3.Cross(cc - a, d - a);
        if (Vector3.Dot(nn, outward) < 0) { var tmp = b; b = d; d = tmp; nn = -nn; }
        nn.Normalize();
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(cc); v.Add(d);
        for (int k = 0; k < 4; k++) { n.Add(nn); c.Add(col); }
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
        t.Add(i); t.Add(i + 2); t.Add(i + 3);
    }

    public void Tri(Vector3 a, Vector3 b, Vector3 cc, Color col, Vector3 outward)
    {
        a = P(a); b = P(b); cc = P(cc);
        outward = M.MultiplyVector(outward);
        Vector3 nn = Vector3.Cross(b - a, cc - a);
        if (Vector3.Dot(nn, outward) < 0) { var tmp = b; b = cc; cc = tmp; nn = -nn; }
        nn.Normalize();
        int i = v.Count;
        v.Add(a); v.Add(b); v.Add(cc);
        for (int k = 0; k < 3; k++) { n.Add(nn); c.Add(col); }
        t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    // Axis-aligned (in local frame) box, optionally yawed about its own centre.
    public void Box(Vector3 center, Vector3 size, Color col, float yaw = 0f)
    {
        Box(center, size, col, col, yaw);
    }

    public void Box(Vector3 center, Vector3 size, Color col, Color top, float yaw)
    {
        Quaternion r = Quaternion.Euler(0, yaw, 0);
        Vector3 h = size * 0.5f;
        Vector3[] p = new Vector3[8];
        for (int i = 0; i < 8; i++)
            p[i] = center + r * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        // index bits: x=1, y=2, z=4
        Quad(p[0], p[2], p[3], p[1], col, r * Vector3.back);
        Quad(p[4], p[5], p[7], p[6], col, r * Vector3.forward);
        Quad(p[0], p[4], p[6], p[2], col, r * Vector3.left);
        Quad(p[1], p[3], p[7], p[5], col, r * Vector3.right);
        Quad(p[2], p[6], p[7], p[3], top, r * Vector3.up);
        Quad(p[0], p[1], p[5], p[4], BB.Dim(col, 0.8f), r * Vector3.down);
    }

    // Box with explicit rotation (used for slanted awnings, wheels, etc.)
    public void BoxRot(Vector3 center, Vector3 size, Color col, Quaternion r)
    {
        Vector3 h = size * 0.5f;
        Vector3[] p = new Vector3[8];
        for (int i = 0; i < 8; i++)
            p[i] = center + r * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
        Quad(p[0], p[2], p[3], p[1], col, r * Vector3.back);
        Quad(p[4], p[5], p[7], p[6], col, r * Vector3.forward);
        Quad(p[0], p[4], p[6], p[2], col, r * Vector3.left);
        Quad(p[1], p[3], p[7], p[5], col, r * Vector3.right);
        Quad(p[2], p[6], p[7], p[3], col, r * Vector3.up);
        Quad(p[0], p[1], p[5], p[4], col, r * Vector3.down);
    }

    // Gable roof: base rectangle w (x) by d (z) at 'baseCenter', ridge along x at height h.
    public void Gable(Vector3 baseCenter, float w, float d, float h, Color roof, Color gableEnd)
    {
        Vector3 o = baseCenter;
        Vector3 fl = o + new Vector3(-w / 2, 0, -d / 2), fr = o + new Vector3(w / 2, 0, -d / 2);
        Vector3 bl = o + new Vector3(-w / 2, 0, d / 2), br = o + new Vector3(w / 2, 0, d / 2);
        Vector3 rl = o + new Vector3(-w / 2, h, 0), rr = o + new Vector3(w / 2, h, 0);
        Quad(fl, rl, rr, fr, roof, new Vector3(0, 1, -1));
        Quad(br, rr, rl, bl, BB.Dim(roof, 0.93f), new Vector3(0, 1, 1));
        Tri(fl, bl, rl, gableEnd, Vector3.left);
        Tri(fr, rr, br, gableEnd, Vector3.right);
        Quad(fl, fr, br, bl, BB.Dim(roof, 0.6f), Vector3.down);
    }

    // Cylinder / cone / frustum standing on baseCenter.
    public void Cyl(Vector3 baseCenter, float r, float h, int seg, Color col, float rTop = -1f, bool caps = true)
    {
        if (rTop < 0) rTop = r;
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 mid = (d0 + d1).normalized;
            Quad(baseCenter + d0 * r, baseCenter + d0 * rTop + Vector3.up * h, baseCenter + d1 * rTop + Vector3.up * h, baseCenter + d1 * r, col,
                mid + Vector3.up * (r - rTop) / Mathf.Max(0.01f, h));
            if (caps)
            {
                if (rTop > 0.001f) Tri(baseCenter + Vector3.up * h, baseCenter + d0 * rTop + Vector3.up * h, baseCenter + d1 * rTop + Vector3.up * h, col, Vector3.up);
                Tri(baseCenter, baseCenter + d1 * r, baseCenter + d0 * r, BB.Dim(col, 0.8f), Vector3.down);
            }
        }
    }

    // Cylinder lying along the local X axis (wheels, pipes).
    public void CylX(Vector3 center, float r, float len, int seg, Color col, Color cap)
    {
        for (int i = 0; i < seg; i++)
        {
            float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
            Vector3 d0 = new Vector3(0, Mathf.Cos(a0), Mathf.Sin(a0)), d1 = new Vector3(0, Mathf.Cos(a1), Mathf.Sin(a1));
            Vector3 L = new Vector3(len / 2, 0, 0);
            Quad(center - L + d0 * r, center + L + d0 * r, center + L + d1 * r, center - L + d1 * r, col, d0 + d1);
            Tri(center + L, center + L + d0 * r, center + L + d1 * r, cap, Vector3.right);
            Tri(center - L, center - L + d1 * r, center - L + d0 * r, cap, Vector3.left);
        }
    }

    // Low-poly ellipsoid (tree crowns, bushes, clouds, heads of hair).
    public void Blob(Vector3 center, Vector3 radii, Color col, int lon = 7, int lat = 5)
    {
        for (int y = 0; y < lat; y++)
        {
            float p0 = Mathf.PI * y / lat - Mathf.PI / 2, p1 = Mathf.PI * (y + 1) / lat - Mathf.PI / 2;
            for (int x = 0; x < lon; x++)
            {
                float a0 = Mathf.PI * 2 * x / lon, a1 = Mathf.PI * 2 * (x + 1) / lon;
                Vector3 q00 = S(a0, p0, radii), q10 = S(a1, p0, radii), q01 = S(a0, p1, radii), q11 = S(a1, p1, radii);
                Vector3 mid = (q00 + q10 + q01 + q11) * 0.25f;
                Color shade = BB.Dim(col, 0.88f + 0.12f * Mathf.Sin(x * 2.3f + y));
                Quad(center + q00, center + q01, center + q11, center + q10, shade, mid);
            }
        }
    }

    static Vector3 S(float a, float p, Vector3 r)
    {
        return new Vector3(Mathf.Cos(p) * Mathf.Cos(a) * r.x, Mathf.Sin(p) * r.y, Mathf.Cos(p) * Mathf.Sin(a) * r.z);
    }

    public Mesh Build(string name = "BBMesh")
    {
        var m = new Mesh();
        m.name = name;
        if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v);
        m.SetNormals(n);
        m.SetColors(c);
        m.SetTriangles(t, 0);
        m.RecalculateBounds();
        return m;
    }

    public GameObject Emit(string name, Transform parent, Material mat = null, bool shadows = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = Build(name);
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat != null ? mat : BB.Toon;
        mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    public void Clear() { v.Clear(); n.Clear(); c.Clear(); t.Clear(); M = Matrix4x4.identity; }
}

public static class BB
{
    static Shader toonShader;
    static Material toon;
    static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

    public static Color Glow(Color c) { c.a = 0f; return c; }     // vertex alpha 0: window that glows warm at night
    public static Color Lamp(Color c) { c.a = 0.5f; return c; }   // vertex alpha 0.5: glows in its own colour at night
    public static Color Dim(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }

    static Shader ToonShader()
    {
        if (toonShader == null) toonShader = Shader.Find("BB/Toon");
        if (toonShader == null) toonShader = Shader.Find("Standard");
        return toonShader;
    }

    public static Material Toon
    {
        get
        {
            if (toon == null) { toon = new Material(ToonShader()); toon.name = "BB Toon"; }
            return toon;
        }
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
        var m = new Material(ToonShader());
        SetCol(m, c);
        return m;
    }

    public static void SetCol(Material m, Color c)
    {
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    public static void SetEmit(Material m, Color e)
    {
        m.EnableKeyword("_EMISSION");
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", e);
    }

    // Unit shapes in white (tinted by the material) - stand-ins for Unity's primitives but faceted and vertex-coloured.
    public static Mesh Unit(PrimitiveType t, Color col)
    {
        string key = t + ColorUtility.ToHtmlStringRGBA(col);
        Mesh m;
        if (meshCache.TryGetValue(key, out m)) return m;
        var b = new BBMesh();
        switch (t)
        {
            case PrimitiveType.Cube: b.Box(Vector3.zero, Vector3.one, col); break;
            case PrimitiveType.Sphere: b.Blob(Vector3.zero, Vector3.one * 0.5f, col, 8, 6); break;
            case PrimitiveType.Capsule:
                b.Cyl(new Vector3(0, -0.5f, 0), 0.5f, 1f, 8, col, -1f, false);
                b.Blob(new Vector3(0, 0.5f, 0), new Vector3(0.5f, 0.5f, 0.5f), col, 8, 6);
                b.Blob(new Vector3(0, -0.5f, 0), new Vector3(0.5f, 0.5f, 0.5f), col, 8, 6);
                break;
            default: b.Cyl(new Vector3(0, -1f, 0), 0.5f, 2f, 10, col); break;
        }
        m = b.Build("Unit" + t);
        meshCache[key] = m;
        return m;
    }

    public static GameObject Prim(PrimitiveType t, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider = true, string name = null)
    {
        var g = new GameObject(name ?? t.ToString());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = Unit(t, Color.white);
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider)
        {
            if (t == PrimitiveType.Cube) g.AddComponent<BoxCollider>();
            else if (t == PrimitiveType.Sphere) g.AddComponent<SphereCollider>();
            else g.AddComponent<CapsuleCollider>();
        }
        return g;
    }

    // A coloured box using the single shared toon material (batches well).
    public static GameObject CBox(Transform parent, Vector3 localPos, Vector3 size, Color col, string name = "Part")
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos;
        g.transform.localScale = size;
        g.AddComponent<MeshFilter>().sharedMesh = Unit(PrimitiveType.Cube, col);
        g.AddComponent<MeshRenderer>().sharedMaterial = Toon;
        return g;
    }

    public static GameObject Box(Vector3 pos, Vector3 size, Color c, Transform parent, bool collider = true, string name = null)
    {
        var g = CBox(parent, pos, size, c, name ?? "Box");
        if (collider) g.AddComponent<BoxCollider>();
        return g;
    }

    // Invisible collider (static geometry is drawn by merged meshes).
    public static GameObject Solid(Transform parent, Vector3 center, Vector3 size, float yaw = 0f, string name = "Collider")
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.position = center;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        g.AddComponent<BoxCollider>().size = size;
        return g;
    }

    static Font font;
    public static Font UIFont()
    {
        if (font == null) { try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { } }
        return font;
    }

    public static void Sign(string text, Vector3 pos, float yaw, float size, Color c, Transform parent)
    {
        var g = new GameObject("Sign " + text);
        g.transform.SetParent(parent, false);
        g.transform.position = pos;
        g.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var tm = g.AddComponent<TextMesh>();
        tm.text = text; tm.fontSize = 64; tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.color = c; tm.fontStyle = FontStyle.Bold;
        var f = UIFont();
        if (f != null) { tm.font = f; g.GetComponent<MeshRenderer>().sharedMaterial = f.material; }
    }

    public static Texture2D Tex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); t.hideFlags = HideFlags.DontSave; return t;
    }

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

    // Soft looping ambience: wind-ish filtered noise.
    public static AudioClip Ambience()
    {
        int rate = 22050, len = rate * 4;
        var data = new float[len];
        var r = new System.Random(7);
        float a = 0, b = 0;
        for (int i = 0; i < len; i++)
        {
            float w = (float)r.NextDouble() * 2 - 1;
            a += (w - a) * 0.02f; b += (a - b) * 0.05f;
            float fade = Mathf.Min(1f, Mathf.Min(i, len - i) / 2000f);
            data[i] = b * 0.9f * fade;
        }
        var clip = AudioClip.Create("ambience", len, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    public static string Money(int v) { return "$" + v.ToString("N0"); }

    public static int Hash(int a, int b)
    {
        unchecked
        {
            int h = a * 374761393 + b * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }
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
