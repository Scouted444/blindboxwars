// Blind Box Empire - game state, economy, phone, stations, day cycle and HUD.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BBProduct
{
    public string key, name;
    public List<int> fx = new List<int>();
    public int value;
}

public class BBCustomer
{
    public string name;
    public Color skin, shirt, hat;
    public BBHumanStyle style;
    public int[] prefs;
    public float obs;              // obsession 0..1: higher pays more and texts more
    public bool unlocked, referred;
    public int state;              // 0 idle, 1 asking, 2 deal arranged
    public int wantQty;
    public float reqExpire, dealDeadline;
    public string dealKey, spotName;
    public int dealQty, dealPrice;
    public Vector3 spot;
    public BBCustomerNpc npc;
    public GameObject beacon;
    public List<string> log = new List<string>();
    public int unread;
}

public class BBGame : MonoBehaviour
{
    public static BBGame I;

    // ----- tuning -----
    public static readonly string[] Ing = { "Glitter", "Bubblegum Spray", "Tiny Top Hat", "Googly Eyes", "Energy Drink", "Holo Sticker" };
    public static readonly string[] Fx = { "Sparkly", "Sweet-Scented", "Dapper", "Unhinged", "Hyper", "Limited Edition" };
    static readonly string[] Adj = { "Sparkly", "Bubblegum", "Dapper", "Unhinged", "Turbo", "Holo" };
    static readonly int[] IngCost = { 6, 8, 14, 5, 10, 18 };
    static readonly float[] FxMult = { 0.30f, 0.35f, 0.55f, 0.25f, 0.45f, 0.70f };
    static readonly Color[] FxCol = { BBPal.Butter, BBPal.Bubblegum, BBPal.Lilac, BBPal.Peach, BBPal.Good, BBPal.Mint };
    const int BaseValue = 40, KitCost = 60, StorePrice = 25000;
    static readonly string[] Names = { "Mira", "Dex", "Grandma June", "Kenji", "Priya", "Big Rob", "Skyler", "Off-Duty Dan", "Lulu", "Tobias", "Wren", "Coach Patti" };

    // ----- references -----
    public BBPlayer player;
    BBWorldRefs w;
    Transform root, npcRoot;
    Light sun, moon;
    readonly List<BBCop> cops = new List<BBCop>();
    AudioSource sfx, sirenSrc;
    AudioClip clipDing, clipCash, clipBust, clipDone;

    // ----- state -----
    enum UI { None, Phone, Mixer, Shop, Stash, Message }
    UI ui = UI.None;
    public bool UIOpen { get { return ui != UI.None; } }
    public bool PhoneOpen { get { return ui == UI.Phone; } }
    public int CarryCount { get { return Count(carry); } }
    public string prompt;

    int cash = 300, kits = 1, day = 1, dealsDone, lastPassOutDay = 0;
    float minutes = 8 * 60;
    readonly int[] ing = new int[6];
    readonly Dictionary<string, BBProduct> catalog = new Dictionary<string, BBProduct>();
    readonly Dictionary<string, int> carry = new Dictionary<string, int>();
    readonly Dictionary<string, int> stash = new Dictionary<string, int>();
    readonly List<BBCustomer> customers = new List<BBCustomer>();
    bool pressing; float pressEnd; int pressReady;
    bool upPress, upHoodie, upBlades, upPhone, ownsStore, everPressed, everMixed;
    float nextTextAt;
    string msgTitle, msgBody;

    class Toast { public string text; public float until; public Color col; }
    readonly List<Toast> toasts = new List<Toast>();

    public float T { get { return day * 1440f + minutes; } }
    public bool Curfew { get { return minutes >= 21 * 60 || minutes < 5 * 60; } }
    public float SpotRange { get { return upHoodie ? 13f : 22f; } }
    public bool PlayerInHideout { get { return w.hideout.Contains(player.transform.position + Vector3.up); } }
    int PressTime { get { return upPress ? 14 : 30; } }
    int PressBatch { get { return upPress ? 10 : 6; } }

    // =====================================================================
    void Awake()
    {
        I = this;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) l.gameObject.SetActive(false);

        root = new GameObject("Plushville").transform;
        npcRoot = new GameObject("People").transform;
        w = BBWorld.Build(root);

        var sg = new GameObject("Sun"); sun = sg.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.color = BBPal.Sun;
        var mg = new GameObject("Moon"); moon = mg.AddComponent<Light>();
        moon.type = LightType.Directional; moon.shadows = LightShadows.None; moon.color = BBPal.Moon;
        moon.transform.rotation = Quaternion.Euler(55, 150, 0);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 80; RenderSettings.fogEndDistance = 520;
        QualitySettings.shadowDistance = 90f;
        QualitySettings.shadowCascades = 2;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.antiAliasing = 4;
        QualitySettings.pixelLightCount = 4;
        sun.shadowStrength = 0.85f; sun.shadowBias = 0.04f; sun.shadowNormalBias = 0.3f;
        BuildSky();

        var pg = new GameObject("Player");
        player = pg.AddComponent<BBPlayer>();
        player.Teleport(w.spawn, 0);

        cops.Add(BBCop.Spawn(2, 2, npcRoot));
        cops.Add(BBCop.Spawn(4, 1, npcRoot));
        cops.Add(BBCop.Spawn(1, 4, npcRoot));
        BBPed.SpawnMany(20, npcRoot, new System.Random(5));
        BBCar.SpawnMany(9, npcRoot, new System.Random(9));

        sfx = gameObject.AddComponent<AudioSource>();
        sirenSrc = gameObject.AddComponent<AudioSource>();
        sirenSrc.loop = true; sirenSrc.volume = 0.18f;
        sirenSrc.clip = BB.Tone("siren", new float[] { 720, 960 }, 0.28f, 0.5f, true);
        clipDing = BB.Tone("ding", new float[] { 988, 1319 }, 0.09f, 0.35f);
        clipCash = BB.Tone("cash", new float[] { 659, 880, 1319 }, 0.07f, 0.35f);
        clipBust = BB.Tone("bust", new float[] { 330, 220, 147 }, 0.2f, 0.4f, true);
        clipDone = BB.Tone("done", new float[] { 523, 659, 784 }, 0.09f, 0.35f);
        var amb = gameObject.AddComponent<AudioSource>();
        amb.clip = BB.Ambience(); amb.loop = true; amb.volume = 0.12f; amb.Play();

        WireStations();
        MakeCustomers();
        ing[0] = 2;
        nextTextAt = Time.time + 20f;
        ShowMessage("BLIND BOX WARS",
            "Plushville banned Labubus. Collectors are desperate.\n\n" +
            "You've got a garage, a Plush Press and $300.\n\n" +
            "WASD move, Shift sprint, Space jump, E use, Tab phone.\n\n" +
            "Press plushies, mix in ingredients to make them worth more, answer texts and meet buyers around town. " +
            "Cops patrol the streets, and after 9 PM curfew they chase anyone they see.");
    }

    void WireStations()
    {
        Add(w.press, () =>
        {
            if (pressReady > 0) return "[E] Collect " + pressReady + " Plain Labubus";
            if (pressing) return "Pressing... " + Mathf.CeilToInt(pressEnd - Time.time) + "s";
            return kits > 0 ? "[E] Press a Vinyl Kit (" + kits + " left) into " + PressBatch + " Labubus" : "Plush Press - need a Vinyl Kit from Gas-n-Glam";
        }, UsePress);
        Add(w.mixer, () => "[E] Mixing Table", () => Open(UI.Mixer));
        Add(w.stash, () => "[E] Stash (" + Count(stash) + " stored)", () => Open(UI.Stash));
        Add(w.bed, () => Curfew || minutes >= 20 * 60 ? "[E] Sleep until morning" : "Bed - you can sleep after 8 PM", Sleep);
        Add(w.counter, () => "[E] Shop at Gas-n-Glam", () => Open(UI.Shop));
    }

    static void Add(GameObject go, System.Func<string> p, System.Action u)
    {
        var it = go.AddComponent<BBInteract>(); it.prompt = p; it.use = u;
    }

    void MakeCustomers()
    {
        foreach (var n in Names)
        {
            var c = new BBCustomer();
            c.name = n;
            c.skin = BBPal.Skin[Random.Range(0, BBPal.Skin.Length)];
            c.shirt = BBPal.Clothes[Random.Range(0, BBPal.Clothes.Length)];
            c.hat = BBPal.Walls[Random.Range(0, BBPal.Walls.Length)];
            c.style = BBHumanStyle.Random(new System.Random(n.GetHashCode() & 0x7fffffff));
            int a = Random.Range(0, 6), b; do { b = Random.Range(0, 6); } while (b == a);
            c.prefs = new[] { a, b };
            customers.Add(c);
        }
        customers[0].unlocked = customers[1].unlocked = true;
    }

    // =====================================================================
    // Products
    BBProduct GetProduct(List<int> fx)
    {
        var s = fx.Distinct().OrderBy(x => x).ToList();
        string key = s.Count == 0 ? "plain" : string.Join("-", s.Select(x => x.ToString()).ToArray());
        BBProduct p;
        if (catalog.TryGetValue(key, out p)) return p;
        p = new BBProduct();
        p.key = key; p.fx = s;
        p.name = s.Count == 0 ? "Plain Labubu" : string.Join(" ", s.Select(x => Adj[x]).ToArray()) + " Labubu";
        float m = 1; foreach (int x in s) m += FxMult[x];
        p.value = Mathf.RoundToInt(BaseValue * m);
        catalog[key] = p;
        return p;
    }

    static int Get(Dictionary<string, int> d, string k) { int v; return d.TryGetValue(k, out v) ? v : 0; }
    static void AddTo(Dictionary<string, int> d, string k, int n) { int v = Get(d, k) + n; if (v <= 0) d.Remove(k); else d[k] = v; }
    static int Count(Dictionary<string, int> d) { int t = 0; foreach (var kv in d) t += kv.Value; return t; }

    int Matches(BBCustomer c, BBProduct p) { int m = 0; foreach (int f in c.prefs) if (p.fx.Contains(f)) m++; return m; }
    int Fair(BBCustomer c, BBProduct p) { return Mathf.RoundToInt(p.value * (1 + 0.35f * Matches(c, p)) * (0.85f + 0.6f * c.obs)); }
    static float Odds(int price, int fair) { float r = (float)price / Mathf.Max(1, fair); return r <= 1f ? 1f : Mathf.Clamp01(1f - (r - 1f) * 2.5f); }

    // =====================================================================
    void Update()
    {
        if (BBInp.Esc && UIOpen) ui = UI.None;
        else if (BBInp.Esc) ShowMessage("PAUSED", "WASD move · Shift sprint · Space jump · E use · Tab phone · Esc close/pause");
        else if (BBInp.Phone && (ui == UI.None || ui == UI.Phone)) { ui = ui == UI.Phone ? UI.None : UI.Phone; }

        minutes += Time.deltaTime * 2f; // one in-game day = 12 real minutes
        if (minutes >= 1440) { minutes -= 1440; day++; OnNewDay(); }
        if (minutes >= 4 * 60 && minutes < 5 * 60 && lastPassOutDay != day) PassOut();

        if (Time.time >= nextTextAt) { TrySendText(); nextTextAt = Time.time + Random.Range(25f, 45f) / (upPhone ? 1.8f : 1f); }

        foreach (var c in customers)
        {
            if (c.state == 1 && T > c.reqExpire) { c.state = 0; Say(c, c.name + ": nvm found someone else"); }
            if (c.state == 2 && T > c.dealDeadline)
            {
                c.state = 0; c.obs = Mathf.Max(0, c.obs - 0.1f);
                EndDealVisuals(c);
                Say(c, c.name + ": u ghosted me. wow.");
                AddToast(c.name + " gave up waiting.", BBPal.Bad);
            }
        }

        if (pressing && Time.time >= pressEnd)
        {
            pressing = false; pressReady = PressBatch;
            AddToast("The Plush Press is done.", BBPal.Good); Play(clipDone);
        }
        Color pc = pressing ? BBPal.Accent * (1.2f + Mathf.Sin(Time.time * 6f)) : pressReady > 0 ? BBPal.Good * 2.5f : BBPal.Accent * 0.2f;
        BB.SetEmit(w.pressMat, pc);

        bool chased = cops.Any(c => c.chasing);
        if (chased && !sirenSrc.isPlaying) sirenSrc.Play();
        if (!chased && sirenSrc.isPlaying) sirenSrc.Stop();

        UpdateLighting();
        toasts.RemoveAll(t => Time.time > t.until);
    }

    // ---- sky dome ----
    Mesh skyMesh;
    Transform skyT, sunDisc, moonDisc;
    Vector3[] skyDirs;
    float skyRefreshAt;

    void BuildSky()
    {
        var b = new BBMesh();
        b.Blob(Vector3.zero, Vector3.one * 900f, Color.white, 24, 12);
        skyMesh = b.Build("Sky");
        var verts = skyMesh.vertices;
        skyDirs = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++) skyDirs[i] = verts[i].normalized;
        var sh = Shader.Find("BB/Sky");
        var skyMat = new Material(sh != null ? sh : Shader.Find("Unlit/Color"));
        skyMat.renderQueue = 1010;
        var discMat = new Material(skyMat); discMat.renderQueue = 1020;
        var go = new GameObject("Sky");
        go.AddComponent<MeshFilter>().sharedMesh = skyMesh;
        var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = skyMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        skyT = go.transform;
        var sb = new BBMesh(); sb.Blob(Vector3.zero, Vector3.one * 22f, BBPal.Hex("#FFF6E0"), 10, 6);
        sunDisc = new GameObject("SunDisc").transform; sunDisc.SetParent(skyT, false);
        sunDisc.gameObject.AddComponent<MeshFilter>().sharedMesh = sb.Build("SunDisc");
        sunDisc.gameObject.AddComponent<MeshRenderer>().sharedMaterial = discMat;
        var mb = new BBMesh(); mb.Blob(Vector3.zero, Vector3.one * 14f, BBPal.Hex("#DDE6FF"), 10, 6);
        moonDisc = new GameObject("MoonDisc").transform; moonDisc.SetParent(skyT, false);
        moonDisc.gameObject.AddComponent<MeshFilter>().sharedMesh = mb.Build("MoonDisc");
        moonDisc.gameObject.AddComponent<MeshRenderer>().sharedMaterial = discMat;
    }

    void UpdateLighting()
    {
        float f = minutes / 1440f;
        sun.transform.rotation = Quaternion.Euler(f * 360f - 90f, -35f, 0);
        moon.transform.rotation = Quaternion.Euler(f * 360f + 90f, -35f, 0);
        float s = Mathf.Sin(f * Mathf.PI * 2f - Mathf.PI / 2f);
        float dayK = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.2f, 0.35f, s));
        float dusk = Mathf.Clamp01(1f - Mathf.Abs(s) * 3.2f);           // strongest at sunrise / sunset
        sun.intensity = 1.2f * dayK;
        sun.color = Color.Lerp(BBPal.Sunset, BBPal.Sun, Mathf.InverseLerp(0f, 0.55f, s));
        moon.intensity = 0.28f * (1 - dayK);

        Color zenith = Color.Lerp(BBPal.SkyNight, BBPal.SkyDay, dayK);
        Color horizon = Color.Lerp(BBPal.SkyNightHorizon, BBPal.SkyHorizon, dayK);
        horizon = Color.Lerp(horizon, BBPal.Sunset, dusk * 0.65f);
        player.cam.backgroundColor = horizon;
        RenderSettings.fogColor = Color.Lerp(horizon, zenith, 0.25f);

        Shader.SetGlobalColor("_BBSky", Color.Lerp(BBPal.Hex("#2A2A52"), BBPal.Hex("#9AA8C6"), dayK) + BBPal.Sunset * dusk * 0.12f);
        Shader.SetGlobalColor("_BBGround", Color.Lerp(BBPal.Hex("#141020"), BBPal.Hex("#5E5250"), dayK));
        Shader.SetGlobalFloat("_BBGlow", Mathf.Clamp01((1 - dayK) * 1.3f));
        Shader.SetGlobalColor("_BBGlowColor", BBPal.WindowLit);

        BBPost.Exposure = Mathf.Lerp(1.18f, 1.0f, dayK);
        BBPost.Bloom = Mathf.Lerp(0.95f, 0.45f, dayK);
        BBPost.Tint = Color.Lerp(new Color(0.86f, 0.92f, 1.12f), new Color(1.04f, 1.0f, 0.95f), dayK);
        BBPost.Saturation = Mathf.Lerp(1.0f, 1.14f, dayK);

        if (skyT != null)
        {
            skyT.position = player.cam.transform.position;
            Vector3 sd = -sun.transform.forward;
            sunDisc.localPosition = sd * 820f;
            moonDisc.localPosition = sun.transform.forward * 820f;
            if (Time.time >= skyRefreshAt)
            {
                skyRefreshAt = Time.time + 0.25f;
                var cols = new Color[skyDirs.Length];
                for (int i = 0; i < skyDirs.Length; i++)
                {
                    Vector3 d = skyDirs[i];
                    float e = Mathf.Clamp01(d.y);
                    Color c = Color.Lerp(horizon, zenith, Mathf.Pow(e, 0.55f));
                    if (d.y < 0) c = Color.Lerp(horizon, BBPal.Dim(horizon, 0.7f), -d.y * 2f);
                    float sunGlow = Mathf.Pow(Mathf.Max(0, Vector3.Dot(d, sd)), 10f);
                    c += BBPal.Sunset * sunGlow * (0.25f + dusk * 0.6f) * Mathf.Clamp01(dayK + dusk);
                    if (dayK < 0.5f && d.y > 0.1f && (BB.Hash(i, 7) % 23) == 0) c += Color.white * (0.5f - dayK) * 1.2f; // stars
                    c.a = 1;
                    cols[i] = c;
                }
                skyMesh.colors = cols;
            }
            sunDisc.gameObject.SetActive(s > -0.1f);
            moonDisc.gameObject.SetActive(s < 0.15f);
        }
        w.garageLight.intensity = Mathf.Lerp(2.6f, 0.9f, dayK);
        if (w.storeLight != null) w.storeLight.intensity = Mathf.Lerp(2.0f, 1.0f, dayK);
    }

    void OnNewDay()
    {
        int want = Mathf.Min(6, 3 + day / 2);
        if (cops.Count < want)
        {
            cops.Add(BBCop.Spawn(Random.Range(0, BBWorld.N), BBWorld.N - 1, npcRoot));
            AddToast("Word is the cops added another patrol.", BBPal.Warn);
        }
    }

    // =====================================================================
    // Stations
    void UsePress()
    {
        if (pressReady > 0)
        {
            AddTo(carry, GetProduct(new List<int>()).key, pressReady);
            AddToast("+" + pressReady + " Plain Labubu", BBPal.Good);
            pressReady = 0; Play(clipDing);
            return;
        }
        if (pressing) return;
        if (kits <= 0) { AddToast("You need a Vinyl Kit. Gas-n-Glam is in the far corner of town.", BBPal.Warn); return; }
        kits--; pressing = true; pressEnd = Time.time + PressTime; everPressed = true;
        AddToast("Pressing " + PressBatch + " Labubus...", BBPal.Ink);
    }

    void Sleep()
    {
        if (!(Curfew || minutes >= 20 * 60)) { AddToast("You're not tired yet.", BBPal.Muted); return; }
        if (minutes >= 20 * 60) { day++; OnNewDay(); }
        minutes = 7 * 60; lastPassOutDay = day;
        ShowMessage("DAY " + day, "You wake up in the garage, well rested.\nCash: " + BB.Money(cash) + "   Deals done: " + dealsDone);
    }

    void PassOut()
    {
        lastPassOutDay = day;
        string extra = "";
        if (!PlayerInHideout)
        {
            int lost = cash / 10; cash -= lost;
            extra = "\n\nSomeone went through your pockets while you slept on the sidewalk. -" + BB.Money(lost);
        }
        foreach (var c in cops) if (c.chasing) c.StopChase();
        player.Teleport(w.spawn, 0);
        minutes = 7 * 60;
        ShowMessage("YOU PASSED OUT", "It's 4 AM and your body gave up. You woke up back at HQ." + extra + "\n\nSleep in your bed before 4 AM next time.");
    }

    public void Busted()
    {
        foreach (var c in cops) if (c.chasing) c.StopChase();
        int units = Count(carry);
        carry.Clear();
        int fine = cash / 4; cash -= fine;
        player.Teleport(w.spawn, 0);
        minutes += 180; if (minutes >= 1440) { minutes -= 1440; day++; OnNewDay(); }
        Play(clipBust);
        ShowMessage("BUSTED", "The Bureau of Plush Enforcement confiscated " + units + " Labubus from your pockets and fined you " + BB.Money(fine) +
            ".\n\nThey let you go with a warning. Tip: keep extra product in your stash.");
    }

    public void OnChaseStart() { AddToast("COPS! Break line of sight or get back to HQ!", BBPal.Bad); }
    public void OnCopLost() { if (!cops.Any(c => c.chasing)) AddToast("You lost them.", BBPal.Good); }

    // =====================================================================
    // Customers
    void TrySendText()
    {
        var pool = customers.Where(c => c.unlocked && c.state == 0 && c.npc == null).ToList();
        if (pool.Count == 0) return;
        var c2 = pool[Random.Range(0, pool.Count)];
        c2.state = 1;
        c2.wantQty = Random.Range(1, 3 + Mathf.RoundToInt(c2.obs * 6));
        c2.reqExpire = T + 360;
        string fx = Fx[c2.prefs[Random.Range(0, 2)]].ToLower();
        string[] t = {
            "yo got anything {f}? need like {q}",
            "hey. it's me. need {q} labubus. {f} if u have it",
            "my bag charm is lonely. {q} pls. bonus if {f}",
            "don't tell my mom. {q}x, {f} ones",
            "is this the labubu guy. {q}. {f}. asap"
        };
        Say(c2, c2.name + ": " + t[Random.Range(0, t.Length)].Replace("{f}", fx).Replace("{q}", c2.wantQty.ToString()));
        c2.unread++;
        AddToast("New text from " + c2.name + "  [Tab]", BBPal.Accent);
        Play(clipDing);
    }

    void Say(BBCustomer c, string line) { c.log.Add(line); if (c.log.Count > 30) c.log.RemoveAt(0); }

    void SendOffer(BBCustomer c, BBProduct p, int qty, int price)
    {
        Say(c, "You: " + qty + "x " + p.name + " for " + BB.Money(price * qty));
        if (Random.value > Odds(price, Fair(c, p)))
        {
            c.obs = Mathf.Max(0, c.obs - 0.03f);
            string[] no = { "lol no. too expensive", "bro that's robbery", "i'm broke not stupid", "counter-offer: no" };
            Say(c, c.name + ": " + no[Random.Range(0, no.Length)]);
            return;
        }
        c.state = 2; c.dealKey = p.key; c.dealQty = qty; c.dealPrice = price;
        c.dealDeadline = T + 240;

        int n = BBWorld.N;
        int cx, cz;
        do { cx = Random.Range(0, n); cz = Random.Range(0, n); } while (cx == 0 && cz == 0);
        float sx = cx < n - 1 ? 1 : -1, sz = cz < n - 1 ? 1 : -1;
        c.spot = BBWorld.I(cx, cz) + new Vector3(sx * 5.2f, 0, sz * 5.2f);
        c.spot.y = BBWorld.GroundY(c.spot);
        c.spotName = BBWorld.XStreets[cx] + " & " + BBWorld.ZStreets[cz];
        int ax, az;
        do { ax = Random.Range(0, n); az = Random.Range(0, n); } while (Mathf.Abs(ax - cx) + Mathf.Abs(az - cz) < 2);
        var path = new List<Vector3> { BBWorld.I(ax, az), BBWorld.I(cx, az), BBWorld.I(cx, cz), c.spot };
        c.npc = BBCustomerNpc.Spawn(c, path, npcRoot);
        c.beacon = BB.Prim(PrimitiveType.Cylinder, c.spot + new Vector3(0, 20, 0), new Vector3(0.3f, 20, 0.3f), BB.Mat(BBPal.Accent, 2f), npcRoot, false, "Deal Beacon");
        Say(c, c.name + ": bet. meet me at " + c.spotName + " before " + Clock(c.dealDeadline % 1440));
        AddToast("Deal set: " + c.spotName + " (pink beacon)", BBPal.Good);
    }

    public string DealPrompt(BBCustomer c)
    {
        if (c.state != 2 || c.npc == null || c.npc.leaving) return null;
        if (!c.npc.waiting) return c.name + " is on the way";
        var p = catalog[c.dealKey];
        int have = Get(carry, c.dealKey);
        return have >= c.dealQty
            ? "[E] Hand " + c.name + " " + c.dealQty + "x " + p.name + " for " + BB.Money(c.dealQty * c.dealPrice)
            : c.name + " wants " + c.dealQty + "x " + p.name + " - you only have " + have + " on you";
    }

    public void CompleteDeal(BBCustomer c)
    {
        if (c.state != 2 || c.npc == null || !c.npc.waiting) return;
        if (Get(carry, c.dealKey) < c.dealQty) { AddToast("You don't have enough on you.", BBPal.Warn); return; }
        var p = catalog[c.dealKey];
        AddTo(carry, c.dealKey, -c.dealQty);
        int total = c.dealQty * c.dealPrice;
        cash += total; dealsDone++;
        int m = Matches(c, p);
        c.obs = Mathf.Clamp01(c.obs + 0.08f + 0.07f * m);
        c.state = 0;
        string[] thx = { "omg it's so ugly. i love it", "pleasure doing business", "my collection grows", "same time tomorrow?" };
        Say(c, c.name + ": " + (m > 0 ? "EXACTLY what i wanted!! " : "") + thx[Random.Range(0, thx.Length)]);
        AddToast("+" + BB.Money(total) + (m > 0 ? "  (" + c.name + " loved it)" : ""), BBPal.Good);
        Play(clipCash);
        EndDealVisuals(c);

        if (!Curfew)
            foreach (var cop in cops)
                if (!cop.chasing && cop.CanSee(player.transform.position, SpotRange * 0.9f)) { cop.StartChase(); break; }

        var locked = customers.FirstOrDefault(x => !x.unlocked);
        if (locked != null && ((c.obs >= 0.3f && !c.referred) || dealsDone % 4 == 0))
        {
            if (c.obs >= 0.3f) c.referred = true;
            locked.unlocked = true;
            Say(c, c.name + ": btw my friend " + locked.name + " wants in. gave them ur number");
            Say(locked, locked.name + ": " + c.name + " said u can get me labubus??");
            locked.unread++;
            AddToast("New customer: " + locked.name, BBPal.Accent);
        }
    }

    void EndDealVisuals(BBCustomer c)
    {
        if (c.beacon != null) Destroy(c.beacon);
        if (c.npc != null)
        {
            int n = BBWorld.N;
            int cx = Mathf.Clamp(Mathf.RoundToInt(c.spot.x / BBWorld.S), 0, n - 1), cz = Mathf.Clamp(Mathf.RoundToInt(c.spot.z / BBWorld.S), 0, n - 1);
            int ex = Random.Range(0, n), ez = Random.Range(0, n);
            c.npc.Leave(new List<Vector3> { BBWorld.I(cx, cz), BBWorld.I(ex, cz), BBWorld.I(ex, ez) });
            c.npc = null;
        }
    }

    // =====================================================================
    // Small helpers
    void Play(AudioClip c) { if (c != null) sfx.PlayOneShot(c); }
    void AddToast(string t, Color c) { toasts.Add(new Toast { text = t, until = Time.time + 4.5f, col = c }); if (toasts.Count > 5) toasts.RemoveAt(0); }
    void ShowMessage(string t, string b) { msgTitle = t; msgBody = b; ui = UI.Message; }
    void Open(UI u) { ui = u; mixProduct = null; mixIng = -1; }

    static string Clock(float m)
    {
        int h = (int)(m / 60) % 24, mm = (int)(m % 60);
        int h12 = h % 12 == 0 ? 12 : h % 12;
        return h12 + ":" + mm.ToString("00") + (h < 12 ? " AM" : " PM");
    }

    string Objective()
    {
        if (!everPressed) return "Walk to the Plush Press in your garage and press E.";
        if (Count(carry) == 0 && pressReady == 0 && pressing) return "While it presses, check your phone [Tab] for texts.";
        if (!everMixed) return "Mix ingredients into Labubus at the Mixing Table. Buy more at Gas-n-Glam.";
        if (dealsDone == 0) return "Answer a text [Tab], then meet the buyer at the pink beacon.";
        if (ownsStore) return "You went legit. Keep stacking cash.";
        return "Build the empire. Goal: buy the Gas-n-Glam toy aisle for " + BB.Money(StorePrice) + ".";
    }

    // =====================================================================
    // GUI
    GUIStyle h1, h2, body, small, btn, btnSel, label;
    float sc = 1f;
    Vector2 scrollA, scrollB;
    BBCustomer phoneSel;
    int phoneTab, offerIdx, offerQty = 1, offerPrice;
    BBProduct mixProduct; int mixIng = -1;

    GUIStyle St(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true };
        s.normal.textColor = c;
        return s;
    }

    void InitStyles()
    {
        if (h1 != null) return;
        h1 = St(30, BBPal.Ink, FontStyle.Bold);
        h2 = St(19, BBPal.Ink, FontStyle.Bold);
        body = St(15, BBPal.Ink);
        small = St(13, BBPal.Muted);
        label = St(15, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
        btn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, wordWrap = true };
        btn.normal.background = BB.Tex(BBPal.Hex("#3A2A52")); btn.hover.background = BB.Tex(BBPal.Hex("#4C3870"));
        btn.active.background = BB.Tex(BBPal.Accent);
        btn.normal.textColor = btn.hover.textColor = btn.active.textColor = BBPal.Ink;
        btn.padding = new RectOffset(10, 10, 7, 7);
        btnSel = new GUIStyle(btn);
        btnSel.normal.background = btnSel.hover.background = BB.Tex(BBPal.Accent);
    }

    static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = o; }

    void Panel(Rect r) { Fill(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), BBPal.Accent); Fill(r, BBPal.Panel); }

    // Clicks are applied on the next Layout pass so the layout never changes mid-event (avoids IMGUI control-count errors).
    int btnCount, fireId = -1, pendingClick = -1;
    bool Btn(string t, bool sel = false, params GUILayoutOption[] o)
    {
        int id = btnCount++;
        if (GUILayout.Button(t, sel ? btnSel : btn, o)) pendingClick = id;
        return id == fireId;
    }

    void OnGUI()
    {
        InitStyles();
        if (Event.current.type == EventType.Layout) { fireId = pendingClick; pendingClick = -1; } else fireId = -1;
        btnCount = 0;
        sc = Mathf.Max(0.75f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1));
        float W = Screen.width / sc, H = Screen.height / sc;

        // Floating labels over buyers
        foreach (var c in customers)
        {
            if (c.npc == null) continue;
            Vector3 sp = player.cam.WorldToScreenPoint(c.npc.transform.position + Vector3.up * 2.6f);
            if (sp.z <= 0 || sp.z > 60) continue;
            float x = sp.x / sc, y = (Screen.height - sp.y) / sc;
            var r = new Rect(x - 90, y - 26, 180, 26);
            Fill(r, new Color(BBPal.Panel.r, BBPal.Panel.g, BBPal.Panel.b, 0.75f));
            GUI.Label(r, c.name + " - " + c.dealQty + "x", label);
        }

        // Top-left: clock and cash
        Panel(new Rect(16, 16, 300, 96));
        GUI.Label(new Rect(28, 22, 290, 36), "Day " + day + "  ·  " + Clock(minutes), h2);
        GUI.Label(new Rect(28, 48, 290, 40), BB.Money(cash), St(28, BBPal.Good, FontStyle.Bold));
        GUI.Label(new Rect(170, 56, 140, 30), Curfew ? "CURFEW" : "", St(16, BBPal.Bad, FontStyle.Bold, TextAnchor.UpperRight));
        GUI.Label(new Rect(18, 120, 520, 50), Objective(), St(15, BBPal.Ink, FontStyle.Bold));

        // Wanted banner
        if (cops.Any(c => c.chasing))
        {
            var r = new Rect(W / 2 - 230, 18, 460, 44);
            Fill(r, Mathf.Repeat(Time.time, 0.6f) < 0.3f ? BBPal.Bad : BBPal.Cop);
            GUI.Label(r, "COPS ON YOU - break line of sight!", St(19, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
        }

        // On-you inventory
        float iy = H - 30;
        int lines = 0;
        foreach (var kv in carry) { lines++; }
        Panel(new Rect(16, H - 64 - lines * 22, 300, 48 + lines * 22));
        GUI.Label(new Rect(28, H - 60 - lines * 22, 280, 24), "On you  ·  Kits " + kits + "  ·  Ingredients " + ing.Sum(), small);
        foreach (var kv in carry)
        {
            GUI.Label(new Rect(28, H - 38 - lines * 22, 290, 24), kv.Value + "x  " + catalog[kv.Key].name, body);
            lines--;
        }
        if (carry.Count == 0) GUI.Label(new Rect(28, iy - 14, 280, 24), "No product", body);

        // Phone hint
        int unread = customers.Sum(c => c.unread);
        GUI.Label(new Rect(W - 330, H - 44, 310, 30), "[Tab] Phone" + (unread > 0 ? "  (" + unread + " new)" : ""),
            St(17, unread > 0 ? BBPal.Accent : BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleRight));

        // Toasts
        for (int i = 0; i < toasts.Count; i++)
        {
            var r = new Rect(W - 420, 16 + i * 40, 400, 34);
            Fill(r, new Color(BBPal.Panel.r, BBPal.Panel.g, BBPal.Panel.b, 0.9f));
            Fill(new Rect(r.x, r.y, 5, r.height), toasts[i].col);
            GUI.Label(new Rect(r.x + 14, r.y, r.width - 18, r.height), toasts[i].text, St(15, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleLeft));
        }

        // Crosshair and prompt
        if (!UIOpen)
        {
            Fill(new Rect(W / 2 - 3, H / 2 - 3, 6, 6), BBPal.Ink);
            if (!string.IsNullOrEmpty(prompt))
            {
                var r = new Rect(W / 2 - 260, H / 2 + 26, 520, 34);
                Fill(r, new Color(BBPal.Panel.r, BBPal.Panel.g, BBPal.Panel.b, 0.85f));
                GUI.Label(r, prompt, St(16, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
        }

        switch (ui)
        {
            case UI.Phone: DrawPhone(W, H); break;
            case UI.Mixer: DrawMixer(W, H); break;
            case UI.Shop: DrawShop(W, H); break;
            case UI.Stash: DrawStash(W, H); break;
            case UI.Message: DrawMessage(W, H); break;
        }
    }

    void DrawMessage(float W, float H)
    {
        Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
        var r = new Rect(W / 2 - 280, H / 2 - 220, 560, 440);
        Panel(r);
        GUILayout.BeginArea(new Rect(r.x + 28, r.y + 24, r.width - 56, r.height - 48));
        GUILayout.Label(msgTitle, St(34, BBPal.Accent, FontStyle.Bold));
        GUILayout.Space(8);
        GUILayout.Label(msgBody, St(16, BBPal.Ink));
        GUILayout.FlexibleSpace();
        if (Btn("Continue", true, GUILayout.Height(44))) ui = UI.None;
        if (msgTitle == "PAUSED" && Btn("Quit game", false, GUILayout.Height(40))) Application.Quit();
        GUILayout.EndArea();
    }

    // ---------------- Phone ----------------
    void DrawPhone(float W, float H)
    {
        var r = new Rect(W - 420, H / 2 - 320, 390, 640);
        Fill(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), BBPal.Hex("#110C18"));
        Fill(r, BBPal.Panel);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
        GUILayout.Label(Clock(minutes) + "   ·   Plush Phone", small);
        GUILayout.BeginHorizontal();
        if (Btn("Messages", phoneTab == 0)) phoneTab = 0;
        if (Btn("Map", phoneTab == 1)) phoneTab = 1;
        if (Btn("Products", phoneTab == 2)) phoneTab = 2;
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        if (phoneTab == 0) PhoneMessages();
        else if (phoneTab == 1) PhoneMap(r);
        else PhoneProducts();
        GUILayout.EndArea();
    }

    void PhoneMessages()
    {
        if (phoneSel == null)
        {
            scrollA = GUILayout.BeginScrollView(scrollA);
            foreach (var c in customers.Where(x => x.unlocked))
            {
                string st = c.state == 1 ? "  - wants " + c.wantQty : c.state == 2 ? "  - deal at " + c.spotName : "";
                string hearts = new string('♥', 1 + Mathf.FloorToInt(c.obs * 4));
                if (Btn(c.name + (c.unread > 0 ? "  •" + c.unread : "") + st + "\n" + hearts, false, GUILayout.Height(52)))
                { phoneSel = c; c.unread = 0; offerIdx = 0; offerQty = Mathf.Max(1, c.wantQty); offerPrice = 0; scrollB = new Vector2(0, 9999); }
            }
            int locked = customers.Count(x => !x.unlocked);
            GUILayout.Label(locked + " more potential customers. Keep buyers happy to get referrals.", small);
            GUILayout.EndScrollView();
            return;
        }

        var c2 = phoneSel;
        GUILayout.BeginHorizontal();
        if (Btn("< Back", false, GUILayout.Width(80))) { phoneSel = null; return; }
        GUILayout.Label(c2.name, h2);
        GUILayout.EndHorizontal();
        GUILayout.Label("Likes: " + Fx[c2.prefs[0]] + ", " + Fx[c2.prefs[1]] + "   ·   Obsession " + Mathf.RoundToInt(c2.obs * 100) + "%", small);
        scrollB = GUILayout.BeginScrollView(scrollB, GUILayout.Height(c2.state == 1 ? 230 : 440));
        foreach (var line in c2.log)
            GUILayout.Label(line, St(14, line.StartsWith("You:") ? BBPal.Good : BBPal.Ink));
        GUILayout.EndScrollView();

        if (c2.state == 2)
        {
            GUILayout.Label("Meet at " + c2.spotName + " before " + Clock(c2.dealDeadline % 1440) + ". Follow the pink beacon.", St(14, BBPal.Accent, FontStyle.Bold));
            return;
        }
        if (c2.state != 1) { GUILayout.Label("No open request right now.", small); return; }

        var list = catalog.Values.ToList();
        if (list.Count == 0) { GUILayout.Label("You haven't made any product yet. Use the Plush Press first.", St(14, BBPal.Warn)); return; }
        offerIdx = (offerIdx % list.Count + list.Count) % list.Count;
        var p = list[offerIdx];
        int fair = Fair(c2, p);
        if (offerPrice <= 0) offerPrice = Mathf.Max(5, Mathf.RoundToInt(fair / 5f) * 5);

        GUILayout.Label("Make an offer", h2);
        GUILayout.BeginHorizontal();
        if (Btn("<", false, GUILayout.Width(36))) { offerIdx--; offerPrice = 0; }
        GUILayout.Label(p.name + "\non you: " + Get(carry, p.key) + "  ·  stash: " + Get(stash, p.key), St(14, Matches(c2, p) > 0 ? BBPal.Good : BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
        if (Btn(">", false, GUILayout.Width(36))) { offerIdx++; offerPrice = 0; }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Qty " + offerQty, body, GUILayout.Width(110));
        if (Btn("-", false, GUILayout.Width(40))) offerQty = Mathf.Max(1, offerQty - 1);
        if (Btn("+", false, GUILayout.Width(40))) offerQty = Mathf.Min(20, offerQty + 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Each " + BB.Money(offerPrice), body, GUILayout.Width(110));
        if (Btn("-$5", false, GUILayout.Width(50))) offerPrice = Mathf.Max(5, offerPrice - 5);
        if (Btn("+$5", false, GUILayout.Width(50))) offerPrice += 5;
        if (Btn("+$25", false, GUILayout.Width(56))) offerPrice += 25;
        GUILayout.EndHorizontal();
        float odds = Odds(offerPrice, fair);
        GUILayout.Label("Total " + BB.Money(offerPrice * offerQty) + "   ·   chance they say yes: " + Mathf.RoundToInt(odds * 100) + "%",
            St(14, odds > 0.7f ? BBPal.Good : odds > 0.3f ? BBPal.Warn : BBPal.Bad, FontStyle.Bold));
        if (Btn("Send offer", true, GUILayout.Height(40))) { SendOffer(c2, p, offerQty, offerPrice); scrollB = new Vector2(0, 9999); }
    }

    void PhoneMap(Rect phone)
    {
        float size = phone.width - 28;
        Rect m = GUILayoutUtility.GetRect(size, size);
        float max = BBWorld.S * (BBWorld.N - 1), pad = 10, k = (size - pad * 2) / max;
        System.Func<Vector3, Vector2> P = v => new Vector2(m.x + pad + v.x * k, m.y + pad + (max - v.z) * k);
        Fill(m, BBPal.Asphalt);
        for (int i = 0; i < BBWorld.N - 1; i++)
            for (int j = 0; j < BBWorld.N - 1; j++)
            {
                var a = P(new Vector3(i * BBWorld.S + BBWorld.RoadHalf, 0, (j + 1) * BBWorld.S - BBWorld.RoadHalf));
                float bs = (BBWorld.S - 2 * BBWorld.RoadHalf) * k;
                Color col = i == 0 && j == 0 ? BBPal.Lilac : i == 3 && j == 3 ? BBPal.Butter : i == 1 && j == 2 ? BBPal.Leaf : BBPal.Sidewalk;
                Fill(new Rect(a.x, a.y, bs, bs), col);
            }
        var hq = P(new Vector3(20, 0, 20)); GUI.Label(new Rect(hq.x - 40, hq.y - 10, 80, 20), "HQ", St(13, BBPal.Panel, FontStyle.Bold, TextAnchor.MiddleCenter));
        var sh = P(new Vector3(140, 0, 140)); GUI.Label(new Rect(sh.x - 40, sh.y - 10, 80, 20), "SHOP", St(13, BBPal.Panel, FontStyle.Bold, TextAnchor.MiddleCenter));
        foreach (var c in customers.Where(x => x.state == 2))
        {
            var s = P(c.spot); Fill(new Rect(s.x - 6, s.y - 6, 12, 12), BBPal.Accent);
            GUI.Label(new Rect(s.x + 6, s.y - 9, 120, 20), c.name, St(12, BBPal.Ink, FontStyle.Bold));
        }
        foreach (var cop in cops.Where(x => x.chasing)) { var s = P(cop.transform.position); Fill(new Rect(s.x - 4, s.y - 4, 8, 8), BBPal.Bad); }
        var pp = P(player.transform.position); Fill(new Rect(pp.x - 5, pp.y - 5, 10, 10), BBPal.Good);
        GUILayout.Label("Green: you  ·  Pink: deal spots  ·  Red: cops chasing you", small);
    }

    void PhoneProducts()
    {
        scrollA = GUILayout.BeginScrollView(scrollA);
        if (catalog.Count == 0) GUILayout.Label("Nothing yet. Press some Labubus!", body);
        foreach (var p in catalog.Values.OrderByDescending(x => x.value))
        {
            GUILayout.Label(p.name + "  ·  base " + BB.Money(p.value), St(15, BBPal.Ink, FontStyle.Bold));
            GUILayout.Label((p.fx.Count == 0 ? "No effects" : string.Join(", ", p.fx.Select(x => Fx[x]).ToArray())) +
                "\non you " + Get(carry, p.key) + "  ·  stash " + Get(stash, p.key), small);
            GUILayout.Space(6);
        }
        GUILayout.EndScrollView();
    }

    // ---------------- Stations ----------------
    Rect Window(float W, float H, string title)
    {
        Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.45f));
        var r = new Rect(W / 2 - 330, H / 2 - 270, 660, 540);
        Panel(r);
        GUI.Label(new Rect(r.x + 24, r.y + 14, 500, 40), title, h1);
        if (GUI.Button(new Rect(r.xMax - 100, r.y + 18, 80, 34), "Close", btn)) ui = UI.None;
        return new Rect(r.x + 24, r.y + 64, r.width - 48, r.height - 84);
    }

    void DrawMixer(float W, float H)
    {
        var a = Window(W, H, "Mixing Table");
        GUILayout.BeginArea(a);
        GUILayout.Label("Add an ingredient to a Labubu to give it an effect. Effects stack (up to 4) and make it worth more.", small);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("1. Product on you", h2);
        if (carry.Count == 0) GUILayout.Label("You aren't carrying any Labubus.", body);
        foreach (var kv in carry.ToList())
        {
            var p = catalog[kv.Key];
            if (Btn(kv.Value + "x " + p.name, mixProduct == p)) mixProduct = p;
        }
        GUILayout.EndVertical();
        GUILayout.Space(16);
        GUILayout.BeginVertical();
        GUILayout.Label("2. Ingredient", h2);
        for (int i = 0; i < Ing.Length; i++)
        {
            GUI.enabled = ing[i] > 0;
            if (Btn(Ing[i] + " (" + ing[i] + ")  →  " + Fx[i], mixIng == i)) mixIng = i;
            GUI.enabled = true;
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();

        if (mixProduct != null && mixIng >= 0 && Get(carry, mixProduct.key) > 0)
        {
            var fx = new List<int>(mixProduct.fx);
            if (fx.Contains(mixIng)) { GUILayout.Label("It already has " + Fx[mixIng] + ".", St(15, BBPal.Warn)); }
            else
            {
                fx.Add(mixIng); if (fx.Count > 4) fx.RemoveAt(0);
                var res = GetProductPreview(fx);
                GUILayout.Label("→ " + res.Key + "   (base " + BB.Money(res.Value) + " each, was " + BB.Money(mixProduct.value) + ")", St(17, BBPal.Good, FontStyle.Bold));
                int maxN = Mathf.Min(Get(carry, mixProduct.key), ing[mixIng]);
                GUILayout.BeginHorizontal();
                if (Btn("Mix 1", true, GUILayout.Height(40))) DoMix(fx, 1);
                if (Btn("Mix all I can (" + maxN + ")", true, GUILayout.Height(40))) DoMix(fx, maxN);
                GUILayout.EndHorizontal();
            }
        }
        else GUILayout.Label("Pick a product and an ingredient.", body);
        GUILayout.EndArea();
    }

    KeyValuePair<string, int> GetProductPreview(List<int> fx)
    {
        var s = fx.Distinct().OrderBy(x => x).ToList();
        float m = 1; foreach (int x in s) m += FxMult[x];
        string name = s.Count == 0 ? "Plain Labubu" : string.Join(" ", s.Select(x => Adj[x]).ToArray()) + " Labubu";
        return new KeyValuePair<string, int>(name, Mathf.RoundToInt(BaseValue * m));
    }

    void DoMix(List<int> fx, int n)
    {
        n = Mathf.Min(n, Get(carry, mixProduct.key), ing[mixIng]);
        if (n <= 0) return;
        var result = GetProduct(fx);
        AddTo(carry, mixProduct.key, -n);
        AddTo(carry, result.key, n);
        ing[mixIng] -= n;
        everMixed = true;
        AddToast("Made " + n + "x " + result.name, BBPal.Good);
        Play(clipDing);
        mixProduct = Get(carry, mixProduct.key) > 0 ? mixProduct : result;
        if (ing[mixIng] <= 0) mixIng = -1;
    }

    void DrawShop(float W, float H)
    {
        var a = Window(W, H, "Gas-n-Glam");
        GUILayout.BeginArea(a);
        GUILayout.Label("\"We don't ask questions. We barely ask for ID.\"   You have " + BB.Money(cash), small);
        scrollA = GUILayout.BeginScrollView(scrollA);
        ShopRow("Vinyl Kit (presses into " + PressBatch + " Labubus)", KitCost, () => kits++);
        for (int i = 0; i < Ing.Length; i++)
        {
            int k = i;
            ShopRow(Ing[i] + "  →  " + Fx[i] + " (+" + Mathf.RoundToInt(FxMult[i] * 100) + "% value)", IngCost[i], () => ing[k]++);
        }
        GUILayout.Space(10);
        GUILayout.Label("Upgrades", h2);
        Upgrade("Industrial Plush Press - 10 per kit, twice as fast", 800, upPress, () => upPress = true);
        Upgrade("Hoodie of Anonymity - cops spot you from much closer", 500, upHoodie, () => upHoodie = true);
        Upgrade("Rollerblades - move 30% faster", 400, upBlades, () => { upBlades = true; player.speedMult = 1.3f; });
        Upgrade("Second Burner Phone - customers text more often", 1200, upPhone, () => upPhone = true);
        Upgrade("Buy the Gas-n-Glam toy aisle - go legit (win)", StorePrice, ownsStore, () =>
        {
            ownsStore = true;
            ShowMessage("YOU WENT LEGIT", "You bought the toy aisle at Gas-n-Glam. The Labubus are on the shelf now, technically legally.\n\n" +
                "Day " + day + ", " + dealsDone + " deals. You win! Keep playing if you want.");
        });
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void ShopRow(string name, int cost, System.Action give)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name + "  ·  " + BB.Money(cost), body, GUILayout.Width(400));
        GUI.enabled = cash >= cost;
        if (Btn("Buy 1", false, GUILayout.Width(80))) { cash -= cost; give(); Play(clipCash); }
        GUI.enabled = cash >= cost * 5;
        if (Btn("Buy 5", false, GUILayout.Width(80))) { cash -= cost * 5; for (int i = 0; i < 5; i++) give(); Play(clipCash); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void Upgrade(string name, int cost, bool owned, System.Action give)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name + "  ·  " + BB.Money(cost), body, GUILayout.Width(400));
        GUI.enabled = !owned && cash >= cost;
        if (Btn(owned ? "Owned" : "Buy", false, GUILayout.Width(164))) { cash -= cost; give(); Play(clipCash); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void DrawStash(float W, float H)
    {
        var a = Window(W, H, "Stash");
        GUILayout.BeginArea(a);
        GUILayout.Label("Cops take whatever is in your pockets. Whatever is in here is safe.", small);
        GUILayout.BeginHorizontal();
        if (Btn("Stash everything", true, GUILayout.Height(36))) { foreach (var kv in carry.ToList()) { AddTo(stash, kv.Key, kv.Value); } carry.Clear(); }
        if (Btn("Take everything", false, GUILayout.Height(36))) { foreach (var kv in stash.ToList()) { AddTo(carry, kv.Key, kv.Value); } stash.Clear(); }
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        scrollA = GUILayout.BeginScrollView(scrollA);
        var keys = carry.Keys.Union(stash.Keys).ToList();
        if (keys.Count == 0) GUILayout.Label("Empty. Go press some Labubus.", body);
        foreach (var k in keys)
        {
            var p = catalog[k];
            GUILayout.BeginHorizontal();
            GUILayout.Label(p.name + "\non you " + Get(carry, k) + "  ·  stashed " + Get(stash, k), body, GUILayout.Width(300));
            GUI.enabled = Get(carry, k) > 0;
            if (Btn("Store all", false, GUILayout.Width(90))) { AddTo(stash, k, Get(carry, k)); carry.Remove(k); }
            GUI.enabled = Get(stash, k) > 0;
            if (Btn("Take 5", false, GUILayout.Width(80))) { int n = Mathf.Min(5, Get(stash, k)); AddTo(stash, k, -n); AddTo(carry, k, n); }
            if (Btn("Take all", false, GUILayout.Width(90))) { AddTo(carry, k, Get(stash, k)); stash.Remove(k); }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
