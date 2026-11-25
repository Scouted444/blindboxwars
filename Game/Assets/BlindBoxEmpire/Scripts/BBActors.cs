// Player controller (with first-person hands), interactables, customers, cops, pedestrians and traffic.
using System;
using System.Collections.Generic;
using UnityEngine;

public class BBInteract : MonoBehaviour
{
    public Func<string> prompt;
    public Action use;
}

public class BBPlayer : MonoBehaviour
{
    public Camera cam;
    public float speedMult = 1f;
    CharacterController cc;
    float pitch, yaw, vy, bob;
    Transform hands, handL, handR, phone, box;
    string heldShown = "<none>";
    Vector2 sway;

    void Awake()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.4f;
        var camGo = new GameObject("Player Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 72; cam.nearClipPlane = 0.03f; cam.farClipPlane = 1200f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.allowHDR = true;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<BBPost>();
        BuildHands();
    }

    void BuildHands()
    {
        hands = new GameObject("Hands").transform;
        hands.SetParent(cam.transform, false);
        Color sleeve = BBPal.Hex("#4F6B8E"), skin = BBPal.Skin[1];
        handR = new GameObject("R").transform; handR.SetParent(hands, false);
        handL = new GameObject("L").transform; handL.SetParent(hands, false);
        foreach (var h in new[] { handR, handL })
        {
            var a = BB.CBox(h, new Vector3(0, 0, -0.18f), new Vector3(0.09f, 0.09f, 0.36f), sleeve);
            BB.CBox(h, new Vector3(0, 0, 0.04f), new Vector3(0.085f, 0.075f, 0.11f), skin);
            BB.CBox(h, new Vector3(0, 0.035f, 0.12f), new Vector3(0.08f, 0.02f, 0.06f), skin);
            foreach (var r in h.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        phone = new GameObject("Phone").transform; phone.SetParent(handR, false);
        phone.localPosition = new Vector3(-0.02f, 0.07f, 0.13f);
        phone.localRotation = Quaternion.Euler(-60, 0, 0);
        BB.CBox(phone, Vector3.zero, new Vector3(0.075f, 0.15f, 0.012f), BBPal.Panel);
        BB.CBox(phone, new Vector3(0, 0, -0.007f), new Vector3(0.065f, 0.13f, 0.004f), BB.Lamp(BBPal.Accent));
        box = new GameObject("BlindBox").transform; box.SetParent(handR, false);
        box.localPosition = new Vector3(-0.03f, 0.08f, 0.12f);
        BB.CBox(box, Vector3.zero, new Vector3(0.11f, 0.14f, 0.11f), BBPal.Bubblegum);
        BB.CBox(box, new Vector3(0, 0.02f, -0.056f), new Vector3(0.07f, 0.05f, 0.004f), BBPal.Accent);
        BB.CBox(box, new Vector3(0, 0.072f, 0), new Vector3(0.115f, 0.01f, 0.115f), BBPal.Ink);
        foreach (var r in hands.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Rebuild the little model in the right hand to match the selected hotbar item.
    void RebuildHeld(string id)
    {
        for (int i = box.childCount - 1; i >= 0; i--) Destroy(box.GetChild(i).gameObject);
        if (id == null) return;
        Color c = BBGame.I.ItemColor(id);
        if (id.StartsWith("B:"))
        {
            BB.CBox(box, Vector3.zero, new Vector3(0.11f, 0.14f, 0.11f), BBPal.Bubblegum);
            BB.CBox(box, new Vector3(0, 0.02f, -0.056f), new Vector3(0.07f, 0.06f, 0.004f), c);
            BB.CBox(box, new Vector3(0, 0.072f, 0), new Vector3(0.115f, 0.01f, 0.115f), BBPal.Ink);
        }
        else if (id.StartsWith("L:"))
        {
            BB.CBox(box, Vector3.zero, new Vector3(0.09f, 0.1f, 0.07f), c);
            BB.CBox(box, new Vector3(-0.03f, 0.07f, 0), new Vector3(0.025f, 0.06f, 0.025f), c);
            BB.CBox(box, new Vector3(0.03f, 0.07f, 0), new Vector3(0.025f, 0.06f, 0.025f), c);
            BB.CBox(box, new Vector3(-0.02f, 0.015f, -0.036f), new Vector3(0.015f, 0.02f, 0.004f), BBPal.Panel);
            BB.CBox(box, new Vector3(0.02f, 0.015f, -0.036f), new Vector3(0.015f, 0.02f, 0.004f), BBPal.Panel);
            BB.CBox(box, new Vector3(0, -0.02f, -0.036f), new Vector3(0.04f, 0.008f, 0.004f), BBPal.Ink);
        }
        else if (id == "kit")
        {
            BB.CBox(box, Vector3.zero, new Vector3(0.16f, 0.1f, 0.12f), BBPal.Bubblegum);
            BB.CBox(box, new Vector3(0, 0, -0.061f), new Vector3(0.1f, 0.05f, 0.004f), BBPal.Ink);
        }
        else if (id == "boxes")
        {
            for (int k = 0; k < 3; k++) BB.CBox(box, new Vector3(0, k * 0.03f, 0), new Vector3(0.12f, 0.025f, 0.12f), BBPal.Ink);
        }
        else if (id.StartsWith("ing"))
        {
            BB.CBox(box, Vector3.zero, new Vector3(0.06f, 0.13f, 0.06f), c);
            BB.CBox(box, new Vector3(0, 0.08f, 0), new Vector3(0.035f, 0.04f, 0.035f), BBPal.SteelDark);
        }
        else
        {
            BB.CBox(box, new Vector3(0, 0.02f, 0.04f), new Vector3(0.22f, 0.18f, 0.2f), BBPal.Wood);
            BB.CBox(box, new Vector3(0, 0.02f, -0.061f), new Vector3(0.23f, 0.03f, 0.004f), BBPal.WoodDark);
        }
        foreach (var r in box.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void Teleport(Vector3 p, float facingYaw)
    {
        cc.enabled = false; transform.position = p; cc.enabled = true;
        yaw = facingYaw; pitch = 0; vy = 0;
    }

    public bool Sprinting { get; private set; }

    void Update()
    {
        var g = BBGame.I;
        bool ui = g.UIOpen;
        Cursor.lockState = ui ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = ui;

        Vector2 look = ui ? Vector2.zero : BBInp.Look();
        yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        Vector2 mv = ui ? Vector2.zero : BBInp.Move();
        Sprinting = !ui && BBInp.Sprint && mv.y > 0;
        float speed = (Sprinting ? 7.5f : 4.5f) * speedMult;
        Vector3 dir = (transform.forward * mv.y + transform.right * mv.x);
        if (dir.sqrMagnitude > 1) dir.Normalize();
        if (cc.isGrounded) { vy = -1f; if (!ui && BBInp.Jump) vy = 5.5f; }
        else vy -= 18f * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * vy) * Time.deltaTime);

        bob += dir.magnitude * speed * Time.deltaTime * 1.6f;
        float b = cc.isGrounded ? Mathf.Sin(bob) * 0.04f * dir.magnitude : 0;
        cam.transform.localPosition = new Vector3(0, 1.65f + b, 0);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Sprinting ? 80f : 72f, Time.deltaTime * 6f);

        // hands: phone when the phone is open, a blind box when carrying product, otherwise relaxed at the sides
        string held = g.HeldId;
        bool phoneUp = g.PhoneOpen, holding = !phoneUp && held != null;
        if (held != heldShown) { heldShown = held; RebuildHeld(held); }
        sway = Vector2.Lerp(sway, -look * 0.004f, Time.deltaTime * 10f);
        float hb = Mathf.Sin(bob) * 0.012f * dir.magnitude, hb2 = Mathf.Abs(Mathf.Cos(bob)) * 0.01f * dir.magnitude;
        Vector3 rTarget = phoneUp ? new Vector3(0.07f, -0.17f, 0.32f) : holding ? new Vector3(0.2f, -0.24f, 0.36f) : new Vector3(0.26f, -0.42f, 0.3f);
        Vector3 lTarget = new Vector3(-0.26f, -0.42f, 0.3f);
        handR.localPosition = Vector3.Lerp(handR.localPosition, rTarget + new Vector3(sway.x + hb, sway.y + hb2, 0), Time.deltaTime * 12f);
        handL.localPosition = Vector3.Lerp(handL.localPosition, lTarget + new Vector3(sway.x - hb, sway.y + hb2, 0), Time.deltaTime * 12f);
        handR.localRotation = Quaternion.Slerp(handR.localRotation, Quaternion.Euler(phoneUp ? -15 : holding ? -8 : 10, phoneUp ? -12 : -6, 0), Time.deltaTime * 10f);
        handL.localRotation = Quaternion.Euler(10, 6, 0);
        phone.gameObject.SetActive(phoneUp);
        box.gameObject.SetActive(holding);

        g.prompt = null;
        if (ui) return;
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 3.4f, ~0, QueryTriggerInteraction.Collide))
        {
            var it = hit.collider.GetComponentInParent<BBInteract>();
            if (it != null && it.prompt != null)
            {
                g.prompt = it.prompt();
                if (BBInp.Interact && it.use != null) it.use();
            }
        }
    }
}

// Camera post effect: bloom + grade + vignette, driven by BBGame's time of day.
public class BBPost : MonoBehaviour
{
    public static float Exposure = 1f, Saturation = 1.12f, Contrast = 1.06f, Bloom = 0.55f, Vignette = 0.28f;
    public static Color Tint = Color.white;
    Material mat;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (mat == null)
        {
            var sh = Shader.Find("Hidden/BB/Post");
            if (sh == null || !sh.isSupported) { Graphics.Blit(src, dst); return; }
            mat = new Material(sh); mat.hideFlags = HideFlags.DontSave;
        }
        int w = src.width / 4, h = src.height / 4;
        var a = RenderTexture.GetTemporary(w, h, 0, src.format);
        var b = RenderTexture.GetTemporary(w, h, 0, src.format);
        mat.SetFloat("_Threshold", 0.78f);
        Graphics.Blit(src, a, mat, 0);
        for (int i = 0; i < 2; i++)
        {
            mat.SetVector("_BlurDir", new Vector4(1, 0, 0, 0)); Graphics.Blit(a, b, mat, 1);
            mat.SetVector("_BlurDir", new Vector4(0, 1, 0, 0)); Graphics.Blit(b, a, mat, 1);
        }
        mat.SetTexture("_BloomTex", a);
        mat.SetFloat("_BloomIntensity", Bloom);
        mat.SetFloat("_Exposure", Exposure);
        mat.SetFloat("_Contrast", Contrast);
        mat.SetFloat("_Saturation", Saturation);
        mat.SetColor("_Tint", Tint);
        mat.SetFloat("_Vignette", Vignette);
        Graphics.Blit(src, dst, mat, 2);
        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
    }
}

public class BBCustomerNpc : MonoBehaviour
{
    public BBCustomer data;
    public List<Vector3> path = new List<Vector3>();
    public bool leaving, waiting;
    BBHuman body;
    int idx;
    const float Speed = 2.6f;

    public static BBCustomerNpc Spawn(BBCustomer c, List<Vector3> path, Transform parent)
    {
        var go = new GameObject("Customer " + c.name);
        go.transform.SetParent(parent, false);
        go.transform.position = path[0];
        var npc = go.AddComponent<BBCustomerNpc>();
        npc.body = BBHuman.Create(go.transform, c.style);
        npc.body.lookTarget = BBGame.I.player.cam.transform;
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 2.0f; col.radius = 0.4f; col.center = new Vector3(0, 1.0f, 0);
        npc.data = c; npc.path = path;
        var it = go.AddComponent<BBInteract>();
        it.prompt = () => BBGame.I.DealPrompt(c);
        it.use = () => BBGame.I.CompleteDeal(c);
        return npc;
    }

    public void Leave(List<Vector3> route)
    {
        leaving = true; waiting = false; path = route; idx = 0;
    }

    void Update()
    {
        float sp = 0;
        if (idx < path.Count)
        {
            Vector3 p = transform.position, t = path[idx];
            Vector3 flat = new Vector3(t.x - p.x, 0, t.z - p.z);
            float step = Speed * Time.deltaTime;
            if (flat.magnitude <= step) { p.x = t.x; p.z = t.z; idx++; }
            else
            {
                p += flat.normalized * step; sp = Speed;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 8f);
            }
            p.y = BBWorld.GroundY(p);
            transform.position = p;
        }
        else if (leaving) Destroy(gameObject);
        else
        {
            waiting = true;
            Vector3 to = BBGame.I.player.transform.position - transform.position; to.y = 0;
            if (to.magnitude < 12f && to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 4f);
        }
        body.speed = sp;
    }
}

public class BBPed : MonoBehaviour
{
    Vector3[] loop;
    int idx; float speed, pauseUntil;
    BBHuman body;

    public static void SpawnMany(int count, Transform parent, System.Random r)
    {
        for (int k = 0; k < count; k++)
        {
            int i = r.Next(BBWorld.N - 1), j = r.Next(BBWorld.N - 1);
            float x0 = i * BBWorld.S + BBWorld.RoadHalf + 1.25f, z0 = j * BBWorld.S + BBWorld.RoadHalf + 1.25f;
            float x1 = (i + 1) * BBWorld.S - BBWorld.RoadHalf - 1.25f, z1 = (j + 1) * BBWorld.S - BBWorld.RoadHalf - 1.25f;
            float y = BBWorld.Curb;
            var pts = new[] { new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1) };
            if (r.NextDouble() < 0.5) Array.Reverse(pts);
            var go = new GameObject("Pedestrian");
            go.transform.SetParent(parent, false);
            int start = r.Next(4);
            go.transform.position = Vector3.Lerp(pts[start], pts[(start + 1) % 4], (float)r.NextDouble());
            var ped = go.AddComponent<BBPed>();
            ped.loop = pts; ped.idx = (start + 1) % 4;
            ped.speed = 1.1f + (float)r.NextDouble() * 0.7f;
            ped.body = BBHuman.Create(go.transform, BBHumanStyle.Random(r));
            ped.body.lookTarget = BBGame.I.player.cam.transform;
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.9f; col.radius = 0.3f; col.center = new Vector3(0, 0.95f, 0);
        }
    }

    void Update()
    {
        if (Time.time < pauseUntil) { body.speed = 0; return; }
        Vector3 p = transform.position, t = loop[idx];
        Vector3 to = t - p; to.y = 0;
        // give the player room
        Vector3 pp = BBGame.I.player.transform.position - p; pp.y = 0;
        float sp = speed;
        if (pp.magnitude < 1.4f && Vector3.Dot(pp, to) > 0) sp = 0;
        float step = sp * Time.deltaTime;
        if (to.magnitude <= step)
        {
            transform.position = new Vector3(t.x, t.y, t.z);
            idx = (idx + 1) % loop.Length;
            if (UnityEngine.Random.value < 0.3f) pauseUntil = Time.time + UnityEngine.Random.Range(2f, 6f);
        }
        else
        {
            transform.position = p + to.normalized * step;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 6f);
        }
        body.speed = sp;
    }
}

public class BBCar : MonoBehaviour
{
    public static readonly List<BBCar> All = new List<BBCar>();
    int ia, ib, dx, dz;          // heading from intersection (ia,ib) toward (ia+dx, ib+dz)
    Vector3 p0, p1, p2;          // current curve
    float t, len, speed;
    bool straightSeg;

    public static void SpawnMany(int count, Transform parent, System.Random r)
    {
        for (int k = 0; k < count; k++)
        {
            var m = new BBMesh();
            BBWorld.Car(m, BBPal.CarPaint[r.Next(BBPal.CarPaint.Length)], r.NextDouble() < 0.25);
            var go = m.Emit("Traffic Car", parent);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true;
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.9f, 0); bc.size = new Vector3(1.9f, 1.8f, 4.4f);
            var car = go.AddComponent<BBCar>();
            car.ia = r.Next(BBWorld.N); car.ib = r.Next(BBWorld.N);
            car.PickDir(r.Next(4));
            car.StartStraight();
            car.t = (float)r.NextDouble() * 0.8f;
            All.Add(car);
        }
    }

    void OnDestroy() { All.Remove(this); }

    static Vector3 Right(Vector3 d) { return new Vector3(d.z, 0, -d.x); }

    void PickDir(int seed)
    {
        int n = BBWorld.N;
        for (int k = 0; k < 4; k++)
        {
            int d = (seed + k) % 4;
            int ndx = d == 0 ? 1 : d == 1 ? -1 : 0, ndz = d == 2 ? 1 : d == 3 ? -1 : 0;
            int na = ia + ndx, nb = ib + ndz;
            if (na < 0 || nb < 0 || na >= n || nb >= n) continue;
            dx = ndx; dz = ndz; return;
        }
    }

    void StartStraight()
    {
        Vector3 d = new Vector3(dx, 0, dz), right = Right(d);
        p0 = BBWorld.I(ia, ib) + d * 6f + right * BBWorld.Lane;
        p2 = BBWorld.I(ia + dx, ib + dz) - d * 6f + right * BBWorld.Lane;
        p1 = (p0 + p2) / 2;
        straightSeg = true; t = 0; len = Vector3.Distance(p0, p2);
    }

    void StartTurn()
    {
        int n = BBWorld.N;
        int na = ia + dx, nb = ib + dz;
        Vector3 d = new Vector3(dx, 0, dz), right = Right(d);
        // choose next heading: mostly straight, sometimes turn, never U-turn unless forced
        var options = new List<int>();
        for (int k = 0; k < 4; k++)
        {
            int ndx = k == 0 ? 1 : k == 1 ? -1 : 0, ndz = k == 2 ? 1 : k == 3 ? -1 : 0;
            if (ndx == -dx && ndz == -dz) continue;
            int a2 = na + ndx, b2 = nb + ndz;
            if (a2 < 0 || b2 < 0 || a2 >= n || b2 >= n) continue;
            options.Add(k);
            if (ndx == dx && ndz == dz) { options.Add(k); options.Add(k); }
        }
        int ch;
        if (options.Count == 0) ch = dx == 1 ? 1 : dx == -1 ? 0 : dz == 1 ? 3 : 2;
        else ch = options[UnityEngine.Random.Range(0, options.Count)];
        int ndx2 = ch == 0 ? 1 : ch == 1 ? -1 : 0, ndz2 = ch == 2 ? 1 : ch == 3 ? -1 : 0;
        Vector3 d2 = new Vector3(ndx2, 0, ndz2), right2 = Right(d2);
        Vector3 c = BBWorld.I(na, nb);
        p0 = c - d * 6f + right * BBWorld.Lane;
        p2 = c + d2 * 6f + right2 * BBWorld.Lane;
        p1 = (ndx2 == dx && ndz2 == dz) ? (p0 + p2) / 2 : (ndx2 == -dx && ndz2 == -dz) ? c + d * 3f : c + (right + right2) * BBWorld.Lane;
        ia = na; ib = nb; dx = ndx2; dz = ndz2;
        straightSeg = false; t = 0;
        len = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2);
    }

    Vector3 Bez(float u) { float a = 1 - u; return a * a * p0 + 2 * a * u * p1 + u * u * p2; }

    void Update()
    {
        // slow for anything ahead (player, other cars)
        float want = straightSeg ? 9f : 5f;
        Vector3 fwd = transform.forward;
        Vector3 pp = BBGame.I.player.transform.position - transform.position; pp.y = 0;
        if (Vector3.Dot(pp, fwd) > 0 && pp.magnitude < 9f && Vector3.Cross(fwd, pp).magnitude < 1.8f) want = 0;
        foreach (var o in All)
        {
            if (o == this) continue;
            Vector3 op = o.transform.position - transform.position; op.y = 0;
            if (Vector3.Dot(op, fwd) > 0 && op.magnitude < 7.5f && Vector3.Cross(fwd, op).magnitude < 1.6f) { want = 0; break; }
        }
        speed = Mathf.MoveTowards(speed, want, (want < speed ? 18f : 5f) * Time.deltaTime);
        t += speed * Time.deltaTime / Mathf.Max(0.1f, len);
        if (t >= 1f) { if (straightSeg) StartTurn(); else StartStraight(); }
        Vector3 pos = Bez(Mathf.Clamp01(t)), ahead = Bez(Mathf.Clamp01(t + 0.02f));
        Vector3 dir = ahead - pos; dir.y = 0;
        transform.position = pos;
        if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
    }
}

public class BBCop : MonoBehaviour
{
    public bool chasing;
    CharacterController cc;
    Vector3 target, lastSeen, lastPos;
    float lostFor, vy, stuck;
    int ia, ib;
    Transform siren;
    BBHuman body;

    public static BBCop Spawn(int a, int b, Transform parent)
    {
        var go = new GameObject("Cop");
        go.transform.SetParent(parent, false);
        go.transform.position = BBWorld.I(a, b) + Vector3.up * 0.1f;
        var cop = go.AddComponent<BBCop>();
        cop.body = BBHuman.Create(go.transform, BBHumanStyle.Cop(new System.Random(a * 31 + b * 7 + UnityEngine.Random.Range(0, 999))));
        cop.ia = a; cop.ib = b;
        return cop;
    }

    void Start()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.4f; cc.center = new Vector3(0, 1f, 0); cc.stepOffset = 0.4f;
        siren = BB.Prim(PrimitiveType.Sphere, new Vector3(0, 2.45f, 0), Vector3.one * 0.22f, BB.Mat(BBPal.Bad, 3f), transform, false).transform;
        siren.gameObject.SetActive(false);
        NextPatrol();
    }

    void NextPatrol()
    {
        int n = BBWorld.N;
        for (int tries = 0; tries < 10; tries++)
        {
            int d = UnityEngine.Random.Range(0, 4);
            int na = ia + (d == 0 ? 1 : d == 1 ? -1 : 0), nb = ib + (d == 2 ? 1 : d == 3 ? -1 : 0);
            if (na < 0 || nb < 0 || na >= n || nb >= n) continue;
            ia = na; ib = nb; break;
        }
        // walk the sidewalk edge rather than the middle of the road
        float sx = ia == BBWorld.N - 1 ? -1 : 1, sz = ib == BBWorld.N - 1 ? -1 : 1;
        target = BBWorld.I(ia, ib) + new Vector3(sx * (BBWorld.RoadHalf + 0.9f), 0, sz * (BBWorld.RoadHalf + 0.9f));
    }

    void SnapToGrid()
    {
        ia = Mathf.Clamp(Mathf.RoundToInt(transform.position.x / BBWorld.S), 0, BBWorld.N - 1);
        ib = Mathf.Clamp(Mathf.RoundToInt(transform.position.z / BBWorld.S), 0, BBWorld.N - 1);
        target = BBWorld.I(ia, ib);
    }

    public bool CanSee(Vector3 playerPos, float range)
    {
        Vector3 eye = transform.position + Vector3.up * 1.8f;
        Vector3 head = playerPos + Vector3.up * 1.5f;
        if ((head - eye).magnitude > range) return false;
        RaycastHit hit;
        if (Physics.Linecast(eye, head, out hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<BBPlayer>() != null;
        return true;
    }

    public void StartChase()
    {
        if (chasing) return;
        chasing = true; lostFor = 0;
        lastSeen = BBGame.I.player.transform.position;
        siren.gameObject.SetActive(true);
        BBGame.I.OnChaseStart();
    }

    public void StopChase()
    {
        chasing = false;
        siren.gameObject.SetActive(false);
        SnapToGrid();
    }

    float MoveToward(Vector3 t, float speed)
    {
        Vector3 to = t - transform.position; to.y = 0;
        Vector3 v = to.magnitude > 0.05f ? to.normalized * speed : Vector3.zero;
        if (cc.isGrounded) vy = -1f; else vy -= 18f * Time.deltaTime;
        cc.Move((v + Vector3.up * vy) * Time.deltaTime);
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
        return v.magnitude;
    }

    void Update()
    {
        if (cc == null) return;
        var g = BBGame.I;
        Vector3 pp = g.player.transform.position;
        float dist = Vector3.Distance(transform.position, pp);
        float sp;

        if (!chasing)
        {
            if (g.Curfew && !g.PlayerInHideout && CanSee(pp, g.SpotRange)) { StartChase(); return; }
            sp = MoveToward(target, 1.7f);
            Vector3 flat = target - transform.position; flat.y = 0;
            if (flat.magnitude < 0.6f) NextPatrol();
            stuck = (transform.position - lastPos).sqrMagnitude < 0.0001f ? stuck + Time.deltaTime : 0;
            if (stuck > 2.5f)
            {
                stuck = 0;
                if (dist > 25f) { cc.enabled = false; transform.position = target + Vector3.up * 0.1f; cc.enabled = true; }
                else NextPatrol();
            }
        }
        else
        {
            if (CanSee(pp, 60f)) { lastSeen = pp; lostFor = 0; }
            else lostFor += Time.deltaTime;
            siren.GetComponent<Renderer>().sharedMaterial = BB.Mat(Mathf.Repeat(Time.time, 0.5f) < 0.25f ? BBPal.Bad : BBPal.Moon, 3f);
            if (lostFor > 6f || dist > 55f || g.PlayerInHideout) { StopChase(); g.OnCopLost(); sp = 0; }
            else
            {
                sp = MoveToward(lastSeen, 6.9f);
                if (dist < 1.6f) g.Busted();
            }
        }
        body.speed = sp;
        body.lookTarget = chasing || dist < 8f ? g.player.cam.transform : null;
        lastPos = transform.position;
    }
}
