// Blind Box Wars - core game state: time, lighting, safety, spawning. Split across partial files:
//   BBGame.cs         core loop, day/night, sky, bust / pass out / sleep
//   BBGameItems.cs    hotbar inventory, stations, placement, stash, shop, ATM, properties
//   BBGamePeople.cs   customers, deals, samples, counteroffers, dealers
//   BBGameUI.cs       HUD, phone and every window
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
    public BBHumanStyle style;
    public int[] prefs;
    public float obs;                 // obsession 0..1: pays more, buys more
    public bool unlocked, potential, referred;
    public int state;                 // 0 idle, 1 asking, 2 deal arranged
    public int wantQty;
    public float reqExpire, dealDeadline, sampleRetryAt;
    public string dealKey, spotName;
    public int dealQty, dealPrice;
    public bool counter; public string counterKey; public int counterQty, counterPrice;
    public int homeA, homeB, workA, workB;
    public Vector3 spot;
    public BBCustomerNpc npc;
    public GameObject beacon;
    public BBDealer dealer;
    public List<string> log = new List<string>();
    public int unread;
}

public class BBDealer
{
    public string name;
    public BBHumanStyle style;
    public Vector3 spot;
    public int fee, unlockDeals, cash;
    public bool available, hired;
    public Dictionary<string, int> stock = new Dictionary<string, int>();
    public List<BBCustomer> clients = new List<BBCustomer>();
    public GameObject npc;
    public float nextSaleAt;
    public List<string> log = new List<string>();
}

public partial class BBGame : MonoBehaviour
{
    public static BBGame I;

    // ----- tuning -----
    public static readonly string[] Ing = { "Glitter", "Bubblegum Spray", "Tiny Top Hat", "Googly Eyes", "Energy Drink", "Holo Sticker" };
    public static readonly string[] Fx = { "Sparkly", "Sweet-Scented", "Dapper", "Unhinged", "Hyper", "Limited Edition" };
    static readonly string[] Adj = { "Sparkly", "Bubblegum", "Dapper", "Unhinged", "Turbo", "Holo" };
    static readonly int[] IngCost = { 6, 8, 14, 5, 10, 18 };
    static readonly float[] FxMult = { 0.30f, 0.35f, 0.55f, 0.25f, 0.45f, 0.70f };
    static readonly Color[] FxCol = { BBPal.Butter, BBPal.Bubblegum, BBPal.Lilac, BBPal.Peach, BBPal.Good, BBPal.Mint };
    const int BaseValue = 40, KitCost = 60, BoxPackCost = 20, StorePrice = 50000, DailyDepositLimit = 5000;

    // ----- references -----
    public BBPlayer player;
    BBWorldRefs w;
    Transform root, npcRoot;
    Light sun, moon;
    readonly List<BBCop> cops = new List<BBCop>();
    readonly List<BBStation> stations = new List<BBStation>();
    AudioSource sfx, sirenSrc;
    AudioClip clipDing, clipCash, clipBust, clipDone;

    // ----- state -----
    enum UI { None, Phone, Mixer, Packager, Shop, Stash, Message, ATM, Dealer }
    UI ui = UI.None;
    public bool UIOpen { get { return ui != UI.None; } }
    public bool PhoneOpen { get { return ui == UI.Phone; } }
    public string prompt, placePrompt;

    int cash = 400, bank = 0, day = 1, dealsDone, lastPassOutDay = 0, depositedToday;
    float minutes = 8 * 60;
    readonly Dictionary<string, BBProduct> catalog = new Dictionary<string, BBProduct>();
    bool upPress, upHoodie, upBlades, upPhone, ownsStore, everPressed, everMixed, everPacked;
    float nextTextAt;
    string msgTitle, msgBody;

    class Toast { public string text; public float until; public Color col; }
    readonly List<Toast> toasts = new List<Toast>();

    public float T { get { return day * 1440f + minutes; } }
    public bool Curfew { get { return minutes >= 21 * 60 || minutes < 5 * 60; } }
    public float SpotRange { get { return upHoodie ? 13f : 22f; } }
    public bool PlayerInHideout
    {
        get
        {
            Vector3 p = player.transform.position + Vector3.up;
            if (w.hideout.Contains(p)) return true;
            foreach (var pr in w.props) if (pr.owned && pr.bounds.Contains(p)) return true;
            return false;
        }
    }
    int PressTime { get { return upPress ? 14 : 30; } }
    int PressBatch { get { return upPress ? 10 : 6; } }

    // =====================================================================
    void Awake()
    {
        I = this;
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.gameObject.SetActive(false);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
        for (int i = 0; i < hot.Length; i++) hot[i] = new Slot();

        root = new GameObject("Plushville").transform;
        npcRoot = new GameObject("People").transform;
        w = BBWorld.Build(root);

        var sg = new GameObject("Sun"); sun = sg.AddComponent<Light>();
        sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.color = BBPal.Sun;
        var mg = new GameObject("Moon"); moon = mg.AddComponent<Light>();
        moon.type = LightType.Directional; moon.shadows = LightShadows.None; moon.color = BBPal.Moon;
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

        GetProduct(new List<int>());
        Vector3 o = w.hqOrigin;
        PlaceStation(BBStationKind.Press, o + new Vector3(-4.5f, 0.1f, 4.2f), 0);
        PlaceStation(BBStationKind.Mixer, o + new Vector3(0, 0.1f, 4.7f), 0);
        PlaceStation(BBStationKind.Packager, o + new Vector3(-4.9f, 0.1f, -3.6f), -90);
        WireFixed();
        Vehicle = BBVehicle.SpawnVan(w.hqOrigin + new Vector3(0, 0.1f, -11.5f), 180, root);
        MakeCustomers();
        MakeDealers();
        Add("kit", 1); Add("boxes", 10); Add("ing0", 3);
        nextTextAt = Time.time + 20f;
        ShowMessage("BLIND BOX WARS",
            "Plushville banned Labubus. Collectors are desperate.\n\n" +
            "1. Load a Vinyl Kit into the Plush Press.\n2. Mix in ingredients at the Mixing Station to add effects.\n" +
            "3. Box them at the Packaging Station - buyers only take boxed product.\n4. Answer texts on your phone [Tab] and meet buyers.\n\n" +
            "WASD move · Shift sprint · E use · 1-8 / scroll hotbar · R rotate · Tab phone · Esc pause\n\n" +
            "Cops chase you if they see a deal, and after the 9 PM curfew they chase anyone.");
    }

    // =====================================================================
    void Update()
    {
        if (BBInp.Esc && UIOpen) CloseUI();
        else if (BBInp.Esc) ShowMessage("PAUSED", "WASD move · Shift sprint · Space jump · E use · 1-8 / scroll hotbar · R rotate · Tab phone · Esc close/pause");
        else if (BBInp.Phone && (ui == UI.None || ui == UI.Phone)) { ui = ui == UI.Phone ? UI.None : UI.Phone; }

        if (!UIOpen)
        {
            int k = BBInp.SlotKey();
            if (k >= 0) sel = k;
            float sc = BBInp.Scroll();
            if (sc != 0) sel = (sel - (int)sc + hot.Length) % hot.Length;
        }
        UpdatePlacement();

        float prevMinutes = minutes;
        minutes += Time.deltaTime * 2f; // one in-game day = 12 real minutes
        if (minutes >= 1440) { minutes -= 1440; day++; OnNewDay(); }
        if (minutes >= 4 * 60 && minutes < 5 * 60 && lastPassOutDay != day) PassOut();

        if (Time.time >= nextTextAt) { TrySendText(); nextTextAt = Time.time + Random.Range(25f, 45f) / (upPhone ? 1.8f : 1f); }
        UpdateCustomers();
        UpdateDealers();

        UpdateLaw();
        bool chased = cops.Any(c => c.chasing) || (policeCar != null && !policeCar.leaving);
        if (chased && !sirenSrc.isPlaying) sirenSrc.Play();
        if (!chased && sirenSrc.isPlaying) sirenSrc.Stop();

        UpdateLighting();
        toasts.RemoveAll(t => Time.time > t.until);
    }

    void CloseUI() { ui = UI.None; activeStation = null; activeDealer = null; }

    void OnNewDay()
    {
        depositedToday = 0;
        int want = Mathf.Min(6, 3 + day / 2);
        if (cops.Count < want)
        {
            cops.Add(BBCop.Spawn(Random.Range(0, BBWorld.N), BBWorld.N - 1, npcRoot));
            AddToast("Word is the cops added another patrol.", BBPal.Warn);
        }
    }

    // =====================================================================
    // Sleep / pass out / bust
    void Sleep()
    {
        if (!(Curfew || minutes >= 20 * 60)) { AddToast("You're not tired yet. (after 8 PM)", BBPal.Muted); return; }
        if (minutes >= 20 * 60) { day++; OnNewDay(); }
        minutes = 7 * 60; lastPassOutDay = day;
        ShowMessage("DAY " + day, "You wake up in the garage, well rested.\n\nCash " + BB.Money(cash) + "   Bank " + BB.Money(bank) + "   Deals " + dealsDone);
    }

    void PassOut()
    {
        lastPassOutDay = day;
        ResetLaw();
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
        ResetLaw();
        foreach (var c in cops) if (c.chasing) c.StopChase();
        int units = 0;
        foreach (var s in hot)
            if (s.n > 0 && (s.id.StartsWith("L:") || s.id.StartsWith("B:"))) { units += s.n; s.n = 0; s.id = null; }
        int fine = cash / 4; cash -= fine;
        player.Teleport(w.spawn, 0);
        minutes += 180; if (minutes >= 1440) { minutes -= 1440; day++; OnNewDay(); }
        Play(clipBust);
        ShowMessage("BUSTED", "The Bureau of Plush Enforcement confiscated " + units + " Labubus from your pockets and fined you " + BB.Money(fine) +
            ".\n\nThey let you go with a warning. Money in the bank and product in your stash are safe.");
    }

    public void OnChaseStart() { AddToast("COPS! Break line of sight or get inside a property you own!", BBPal.Bad); }
    public void OnCopLost() { if (!cops.Any(c => c.chasing)) AddToast("You lost them.", BBPal.Good); }

    // =====================================================================
    // Helpers
    void Play(AudioClip c) { if (c != null) sfx.PlayOneShot(c); }
    public void AddToast(string t, Color c) { toasts.Add(new Toast { text = t, until = Time.time + 4.5f, col = c }); if (toasts.Count > 6) toasts.RemoveAt(0); }
    void ShowMessage(string t, string b) { msgTitle = t; msgBody = b; ui = UI.Message; }

    static string Clock(float m)
    {
        int h = (int)(m / 60) % 24, mm = (int)(m % 60);
        int h12 = h % 12 == 0 ? 12 : h % 12;
        return h12 + ":" + mm.ToString("00") + (h < 12 ? " AM" : " PM");
    }

    static Vector3 Corner(int a, int b)
    {
        int n = BBWorld.N;
        float sx = a < n - 1 ? 1 : -1, sz = b < n - 1 ? 1 : -1;
        Vector3 p = BBWorld.I(a, b) + new Vector3(sx * 5.2f, 0, sz * 5.2f);
        p.y = BBWorld.GroundY(p);
        return p;
    }

    string Objective()
    {
        if (!everPressed) return "Load the Vinyl Kit into the Plush Press in your garage (E).";
        if (!everMixed) return "Mix an ingredient into your Labubus at the Mixing Station.";
        if (!everPacked) return "Box them at the Packaging Station - buyers only take boxed product.";
        if (dealsDone == 0) return "Answer a text [Tab], then meet the buyer at the pink beacon.";
        if (customers.Any(c => c.potential)) return "Someone wants a free sample - find the yellow beacon.";
        if (ownsStore) return "You went legit. Keep stacking cash.";
        return "Grow the empire: buy properties, hire dealers. Goal: buy the Gas-n-Glam for " + BB.Money(StorePrice) + ".";
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

    KeyValuePair<string, int> PreviewProduct(List<int> fx)
    {
        var s = fx.Distinct().OrderBy(x => x).ToList();
        float m = 1; foreach (int x in s) m += FxMult[x];
        string name = s.Count == 0 ? "Plain Labubu" : string.Join(" ", s.Select(x => Adj[x]).ToArray()) + " Labubu";
        return new KeyValuePair<string, int>(name, Mathf.RoundToInt(BaseValue * m));
    }

    static string KeyOf(string id) { return id.Substring(2); }

    // =====================================================================
    // Sky + lighting
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

}
