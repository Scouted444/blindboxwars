// HUD (clock, money, hotbar, toasts, prompts), the phone, and every window.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BBGame
{
    GUIStyle h1, h2, body, small, btn, btnSel, label;
    readonly Dictionary<string, GUIStyle> styleCache = new Dictionary<string, GUIStyle>();
    float uiScale = 1f;
    Vector2 scrollA, scrollB, scrollC;
    BBCustomer phoneSel;
    int phoneTab, offerIdx, offerQty = 1, offerPrice;
    BBProduct mixProduct; int mixIng = -1;

    GUIStyle St(int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft)
    {
        string key = size + ColorUtility.ToHtmlStringRGBA(c) + fs + a;
        GUIStyle s;
        if (styleCache.TryGetValue(key, out s)) return s;
        s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, wordWrap = true, richText = true };
        s.normal.textColor = c;
        styleCache[key] = s;
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
    static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
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
        uiScale = Mathf.Max(0.75f, Screen.height / 900f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1));
        float W = Screen.width / uiScale, H = Screen.height / uiScale;

        WorldLabels();

        // top-right: time & money (Schedule I style corner readout)
        var tr = new Rect(W - 250, 14, 236, 96);
        Fill(tr, A(BBPal.Panel, 0.82f));
        GUI.Label(new Rect(tr.x + 12, tr.y + 6, 220, 28), Clock(minutes) + "   Day " + day, St(19, BBPal.Ink, FontStyle.Bold, TextAnchor.UpperRight));
        GUI.Label(new Rect(tr.x + 12, tr.y + 34, 220, 30), BB.Money(cash), St(24, BBPal.Good, FontStyle.Bold, TextAnchor.UpperRight));
        GUI.Label(new Rect(tr.x + 12, tr.y + 64, 220, 24), "Bank " + BB.Money(bank) + (Curfew ? "   CURFEW" : ""), St(14, Curfew ? BBPal.Bad : BBPal.Muted, FontStyle.Bold, TextAnchor.UpperRight));

        // top-left objective
        GUI.Label(new Rect(18, 16, 560, 50), Objective(), St(15, BBPal.Ink, FontStyle.Bold));

        if (cops.Any(c => c.chasing))
        {
            var r = new Rect(W / 2 - 240, 16, 480, 44);
            Fill(r, Mathf.Repeat(Time.time, 0.6f) < 0.3f ? BBPal.Bad : BBPal.Cop);
            GUI.Label(r, "COPS ON YOU - break line of sight!", St(19, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
        }

        // toasts (left)
        for (int i = 0; i < toasts.Count; i++)
        {
            var r = new Rect(16, 76 + i * 38, 420, 32);
            Fill(r, A(BBPal.Panel, 0.88f));
            Fill(new Rect(r.x, r.y, 5, r.height), toasts[i].col);
            GUI.Label(new Rect(r.x + 14, r.y, r.width - 18, r.height), toasts[i].text, St(14, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleLeft));
        }

        DrawHotbar(W, H);

        int unread = customers.Sum(c => c.unread);
        GUI.Label(new Rect(W - 330, H - 40, 310, 28), "[Tab] Phone" + (unread > 0 ? "  (" + unread + " new)" : ""),
            St(16, unread > 0 ? BBPal.Accent : BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleRight));

        if (!UIOpen)
        {
            Fill(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), BBPal.Ink);
            string pr = !string.IsNullOrEmpty(prompt) ? prompt : placePrompt;
            if (!string.IsNullOrEmpty(pr))
            {
                var r = new Rect(W / 2 - 300, H / 2 + 26, 600, 34);
                Fill(r, A(BBPal.Panel, 0.85f));
                GUI.Label(r, pr, St(16, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
            }
        }

        switch (ui)
        {
            case UI.Phone: DrawPhone(W, H); break;
            case UI.Mixer: DrawMixer(W, H); break;
            case UI.Packager: DrawPackager(W, H); break;
            case UI.Shop: DrawShop(W, H); break;
            case UI.Stash: DrawStash(W, H); break;
            case UI.ATM: DrawATM(W, H); break;
            case UI.Dealer: DrawDealer(W, H); break;
            case UI.Message: DrawMessage(W, H); break;
        }
    }

    void WorldLabels()
    {
        foreach (var c in customers)
        {
            if (c.npc == null) continue;
            Vector3 sp = player.cam.WorldToScreenPoint(c.npc.transform.position + Vector3.up * 2.5f);
            if (sp.z <= 0 || sp.z > 60) continue;
            float x = sp.x / uiScale, y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 100, y - 26, 200, 26);
            Fill(r, A(BBPal.Panel, 0.75f));
            GUI.Label(r, c.potential ? c.name + " - sample?" : c.name + " - " + c.dealQty + "x", label);
        }
        foreach (var d in dealers)
        {
            if (d.npc == null) continue;
            Vector3 sp = player.cam.WorldToScreenPoint(d.npc.transform.position + Vector3.up * 2.5f);
            if (sp.z <= 0 || sp.z > 40) continue;
            float x = sp.x / uiScale, y = (Screen.height - sp.y) / uiScale;
            var r = new Rect(x - 90, y - 26, 180, 26);
            Fill(r, A(BBPal.Panel, 0.75f));
            GUI.Label(r, d.name + (d.hired ? " (your dealer)" : " (dealer)"), label);
        }
    }

    void DrawHotbar(float W, float H)
    {
        float s = 64, gap = 6, total = hot.Length * s + (hot.Length - 1) * gap;
        float x0 = W / 2 - total / 2, y = H - s - 18;
        string held = HeldId;
        if (held != null) GUI.Label(new Rect(W / 2 - 300, y - 30, 600, 26), ItemName(held), St(16, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
        for (int i = 0; i < hot.Length; i++)
        {
            var r = new Rect(x0 + i * (s + gap), y, s, s);
            Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), i == sel ? BBPal.Accent : A(Color.black, 0.4f));
            Fill(r, A(BBPal.Panel, 0.85f));
            var sl = hot[i];
            if (sl.n > 0)
            {
                Color c = ItemColor(sl.id);
                bool boxed = sl.id.StartsWith("B:");
                Fill(new Rect(r.x + 10, r.y + 8, s - 20, s - 26), boxed ? BBPal.Bubblegum : c);
                if (boxed) Fill(new Rect(r.x + 16, r.y + 16, s - 32, s - 42), c);
                GUI.Label(new Rect(r.x, r.y + 12, s, 20), ItemShort(sl.id), St(12, BBPal.Panel, FontStyle.Bold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(r.x + 4, r.y + s - 22, s - 8, 20), sl.n > 1 ? sl.n.ToString() : "", St(13, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleRight));
            }
            GUI.Label(new Rect(r.x + 4, r.y + 2, 20, 16), (i + 1).ToString(), St(11, BBPal.Muted));
        }
    }

    void DrawMessage(float W, float H)
    {
        Fill(new Rect(0, 0, W, H), A(Color.black, 0.55f));
        var r = new Rect(W / 2 - 290, H / 2 - 240, 580, 480);
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

    Rect Window(float W, float H, string title, float ww = 680, float wh = 560)
    {
        Fill(new Rect(0, 0, W, H), A(Color.black, 0.45f));
        var r = new Rect(W / 2 - ww / 2, H / 2 - wh / 2, ww, wh);
        Panel(r);
        GUI.Label(new Rect(r.x + 24, r.y + 14, ww - 140, 40), title, h1);
        if (GUI.Button(new Rect(r.xMax - 100, r.y + 18, 80, 34), "Close", btn)) CloseUI();
        return new Rect(r.x + 24, r.y + 64, r.width - 48, r.height - 84);
    }

    // ---------------- phone ----------------
    void DrawPhone(float W, float H)
    {
        var r = new Rect(W - 440, H / 2 - 330, 410, 660);
        Fill(new Rect(r.x - 10, r.y - 10, r.width + 20, r.height + 20), BBPal.Hex("#110C18"));
        Fill(r, BBPal.Panel);
        GUILayout.BeginArea(new Rect(r.x + 14, r.y + 12, r.width - 28, r.height - 24));
        GUILayout.Label(Clock(minutes) + "   ·   Plush Phone   ·   " + BB.Money(cash) + " / bank " + BB.Money(bank), small);
        GUILayout.BeginHorizontal();
        if (Btn("Texts", phoneTab == 0)) phoneTab = 0;
        if (Btn("Map", phoneTab == 1)) phoneTab = 1;
        if (Btn("Products", phoneTab == 2)) phoneTab = 2;
        if (Btn("Dealers", phoneTab == 3)) phoneTab = 3;
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        if (phoneTab == 0) PhoneMessages();
        else if (phoneTab == 1) PhoneMap(r);
        else if (phoneTab == 2) PhoneProducts();
        else PhoneDealers();
        GUILayout.EndArea();
    }

    void PhoneMessages()
    {
        if (phoneSel == null)
        {
            scrollA = GUILayout.BeginScrollView(scrollA);
            foreach (var c in customers.Where(x => x.unlocked || x.potential || x.log.Count > 0))
            {
                string st = c.potential ? "  - wants a sample" : c.dealer != null ? "  - buys from " + c.dealer.name : c.counter ? "  - countered!" : c.state == 1 ? "  - wants " + c.wantQty : c.state == 2 ? "  - deal at " + c.spotName : "";
                string hearts = c.unlocked ? new string('♥', 1 + Mathf.FloorToInt(c.obs * 4)) : "";
                if (Btn(c.name + (c.unread > 0 ? "  •" + c.unread : "") + st + "\n" + hearts, false, GUILayout.Height(52)))
                { phoneSel = c; c.unread = 0; offerIdx = 0; offerQty = Mathf.Max(1, c.wantQty); offerPrice = 0; scrollB = new Vector2(0, 9999); }
            }
            GUILayout.Label(customers.Count(x => !x.unlocked && !x.potential) + " more people in town. Happy buyers refer friends.", small);
            GUILayout.EndScrollView();
            return;
        }

        var c2 = phoneSel;
        GUILayout.BeginHorizontal();
        if (Btn("< Back", false, GUILayout.Width(80))) { phoneSel = null; return; }
        GUILayout.Label(c2.name, h2);
        GUILayout.EndHorizontal();
        GUILayout.Label("Likes: " + Fx[c2.prefs[0]] + ", " + Fx[c2.prefs[1]] + "   ·   Obsession " + Mathf.RoundToInt(c2.obs * 100) + "%", small);
        bool composing = c2.unlocked && c2.dealer == null && c2.state == 1;
        scrollB = GUILayout.BeginScrollView(scrollB, GUILayout.Height(composing ? 230 : 450));
        foreach (var line in c2.log)
            GUILayout.Label(line, St(14, line.StartsWith("You:") ? BBPal.Good : BBPal.Ink));
        GUILayout.EndScrollView();

        if (c2.potential) { GUILayout.Label("Bring a boxed Labubu to " + BBWorld.XStreets[c2.homeA] + " & " + BBWorld.ZStreets[c2.homeB] + " (yellow beacon).", St(14, BBPal.Butter, FontStyle.Bold)); return; }
        if (c2.dealer != null) { GUILayout.Label(c2.dealer.name + " handles this customer.", small); return; }
        if (c2.state == 2)
        {
            GUILayout.Label("Meet at " + c2.spotName + " before " + Clock(c2.dealDeadline % 1440) + ". Bring " + c2.dealQty + "x boxed " + catalog[c2.dealKey].name + ".", St(14, BBPal.Accent, FontStyle.Bold));
            return;
        }
        if (c2.state != 1) { GUILayout.Label("No open request right now.", small); return; }

        if (c2.counter)
        {
            var cp = catalog[c2.counterKey];
            GUILayout.Label("Counteroffer: " + c2.counterQty + "x " + cp.name + " at " + BB.Money(c2.counterPrice) + " each (" + BB.Money(c2.counterPrice * c2.counterQty) + ")", St(15, BBPal.Warn, FontStyle.Bold));
            GUILayout.BeginHorizontal();
            if (Btn("Accept", true, GUILayout.Height(40))) { AcceptCounter(c2); scrollB = new Vector2(0, 9999); }
            if (Btn("Make a new offer", false, GUILayout.Height(40))) c2.counter = false;
            GUILayout.EndHorizontal();
            return;
        }

        var list = catalog.Values.ToList();
        offerIdx = (offerIdx % list.Count + list.Count) % list.Count;
        var p = list[offerIdx];
        int fair = Fair(c2, p);
        if (offerPrice <= 0) offerPrice = Round5(fair);

        GUILayout.Label("Make an offer", h2);
        GUILayout.BeginHorizontal();
        if (Btn("<", false, GUILayout.Width(36))) { offerIdx--; offerPrice = 0; }
        GUILayout.Label(p.name + "\nboxed on you: " + Has("B:" + p.key) + "  ·  stash: " + StashCount("B:" + p.key), St(14, Matches(c2, p) > 0 ? BBPal.Good : BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
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
        GUILayout.Label("Total " + BB.Money(offerPrice * offerQty) + "   ·   chance of yes: " + Mathf.RoundToInt(odds * 100) + "%",
            St(14, odds > 0.7f ? BBPal.Good : odds > 0.3f ? BBPal.Warn : BBPal.Bad, FontStyle.Bold));
        if (Btn("Send offer", true, GUILayout.Height(40))) { SendOffer(c2, p, offerQty, offerPrice); scrollB = new Vector2(0, 9999); }
    }

    int StashCount(string id) { int v; return stash.TryGetValue(id, out v) ? v : 0; }

    void PhoneMap(Rect phone)
    {
        float size = phone.width - 28;
        Rect m = GUILayoutUtility.GetRect(size, size);
        float max = BBWorld.Max, pad = 10, k = (size - pad * 2) / max;
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
        System.Action<Vector3, string, Color> Mark = (v, t, c) =>
        {
            var s = P(v); Fill(new Rect(s.x - 5, s.y - 5, 10, 10), c);
            GUI.Label(new Rect(s.x + 6, s.y - 9, 130, 20), t, St(11, BBPal.Ink, FontStyle.Bold));
        };
        Mark(new Vector3(20, 0, 20), "HQ", BBPal.Accent);
        Mark(new Vector3(140, 0, 140), "SHOP", BBPal.Warn);
        foreach (var pr in w.props) Mark(pr.bounds.center, pr.name + (pr.owned ? "" : " $"), pr.owned ? BBPal.Accent : BBPal.Muted);
        foreach (var a in w.atms) Mark(a.transform.position, "ATM", BBPal.Cop);
        foreach (var d in dealers.Where(x => x.available)) Mark(d.spot, d.name, BBPal.Lilac);
        foreach (var c in customers.Where(x => x.npc != null)) Mark(c.spot, c.name, c.potential ? BBPal.Butter : BBPal.Bad);
        foreach (var cop in cops.Where(x => x.chasing)) { var s = P(cop.transform.position); Fill(new Rect(s.x - 4, s.y - 4, 8, 8), BBPal.Cop); }
        var pp = P(player.transform.position); Fill(new Rect(pp.x - 6, pp.y - 6, 12, 12), BBPal.Good);
        GUILayout.Label("Green: you · Red: deals · Yellow: samples · Blue: ATMs · Purple: dealers", small);
    }

    void PhoneProducts()
    {
        scrollA = GUILayout.BeginScrollView(scrollA);
        foreach (var p in catalog.Values.OrderByDescending(x => x.value))
        {
            GUILayout.Label(p.name + "  ·  base " + BB.Money(p.value), St(15, BBPal.Ink, FontStyle.Bold));
            GUILayout.Label((p.fx.Count == 0 ? "No effects" : string.Join(", ", p.fx.Select(x => Fx[x]).ToArray())) +
                "\nloose " + Has("L:" + p.key) + " · boxed " + Has("B:" + p.key) + " · stash " + (StashCount("L:" + p.key) + StashCount("B:" + p.key)), small);
            GUILayout.Space(6);
        }
        GUILayout.EndScrollView();
    }

    void PhoneDealers()
    {
        scrollA = GUILayout.BeginScrollView(scrollA);
        if (!dealers.Any(d => d.available)) GUILayout.Label("No dealers yet. Do a few deals and word will get around.", body);
        foreach (var d in dealers.Where(x => x.available))
        {
            GUILayout.Label(d.name + (d.hired ? "  ·  working for you" : "  ·  for hire " + BB.Money(d.fee)), St(16, BBPal.Ink, FontStyle.Bold));
            GUILayout.Label("At " + SpotName(d.spot) + (d.hired ? "  ·  holding " + BB.Money(d.cash) + "  ·  stock " + d.stock.Values.Sum() + "  ·  clients " + d.clients.Count : ""), small);
            foreach (var l in d.log.Skip(Mathf.Max(0, d.log.Count - 3))) GUILayout.Label(l, St(13, BBPal.Ink));
            GUILayout.Space(8);
        }
        GUILayout.EndScrollView();
    }

    // ---------------- stations ----------------
    void DrawMixer(float W, float H)
    {
        if (activeStation == null) { CloseUI(); return; }
        var a = Window(W, H, "Mixing Station");
        GUILayout.BeginArea(a);
        GUILayout.Label("Pick loose Labubus from your hotbar and an ingredient. Each ingredient adds an effect (up to 4) and raises the value.", small);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(300));
        GUILayout.Label("1. Loose product", h2);
        var loose = HotIds("L:");
        if (loose.Count == 0) GUILayout.Label("No loose Labubus in your hotbar.", body);
        foreach (var id in loose)
        {
            var p = catalog[KeyOf(id)];
            if (Btn(Has(id) + "x " + p.name, mixProduct == p)) mixProduct = p;
        }
        GUILayout.EndVertical();
        GUILayout.Space(16);
        GUILayout.BeginVertical();
        GUILayout.Label("2. Ingredient", h2);
        for (int i = 0; i < Ing.Length; i++)
        {
            GUI.enabled = Has("ing" + i) > 0;
            if (Btn(Ing[i] + " (" + Has("ing" + i) + ")  →  " + Fx[i], mixIng == i)) mixIng = i;
            GUI.enabled = true;
        }
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.FlexibleSpace();
        if (mixProduct != null && mixIng >= 0 && Has("L:" + mixProduct.key) > 0)
        {
            if (mixProduct.fx.Contains(mixIng)) GUILayout.Label("It already has " + Fx[mixIng] + ".", St(15, BBPal.Warn));
            else
            {
                var fx = new List<int>(mixProduct.fx); fx.Add(mixIng); if (fx.Count > 4) fx.RemoveAt(0);
                var res = PreviewProduct(fx);
                GUILayout.Label("→ " + res.Key + "   (base " + BB.Money(res.Value) + ", was " + BB.Money(mixProduct.value) + ")", St(17, BBPal.Good, FontStyle.Bold));
                int maxN = Mathf.Min(10, Has("L:" + mixProduct.key), Has("ing" + mixIng));
                GUILayout.BeginHorizontal();
                if (Btn("Mix 1", true, GUILayout.Height(40))) StartMix(mixProduct, mixIng, 1);
                if (Btn("Mix " + maxN + " (max 10 per batch)", true, GUILayout.Height(40))) StartMix(mixProduct, mixIng, maxN);
                GUILayout.EndHorizontal();
            }
        }
        else GUILayout.Label("Pick a product and an ingredient.", body);
        GUILayout.EndArea();
    }

    void DrawPackager(float W, float H)
    {
        if (activeStation == null) { CloseUI(); return; }
        var a = Window(W, H, "Packaging Station", 600, 480);
        GUILayout.BeginArea(a);
        GUILayout.Label("Seal loose Labubus into blind boxes. Buyers only accept boxed product. Empty Blind Boxes: " + Has("boxes"), small);
        GUILayout.Space(6);
        var loose = HotIds("L:");
        if (loose.Count == 0) GUILayout.Label("No loose Labubus in your hotbar.", body);
        foreach (var id in loose)
        {
            var p = catalog[KeyOf(id)];
            if (Btn(Has(id) + "x " + p.name, mixProduct == p)) mixProduct = p;
        }
        GUILayout.FlexibleSpace();
        if (mixProduct != null && Has("L:" + mixProduct.key) > 0)
        {
            int maxN = Mathf.Min(20, Has("L:" + mixProduct.key), Has("boxes"));
            if (maxN == 0) GUILayout.Label("You're out of Empty Blind Boxes (Gas-n-Glam sells packs of 10).", St(15, BBPal.Warn));
            else if (Btn("Box " + maxN + "x " + mixProduct.name, true, GUILayout.Height(44))) StartPack(mixProduct, maxN);
        }
        GUILayout.EndArea();
    }

    // ---------------- shop ----------------
    void DrawShop(float W, float H)
    {
        var a = Window(W, H, "Gas-n-Glam", 720, 600);
        GUILayout.BeginArea(a);
        GUILayout.Label("\"We don't ask questions.\"   Cash only. You have " + BB.Money(cash) + ".", small);
        scrollA = GUILayout.BeginScrollView(scrollA);
        GUILayout.Label("Supplies", h2);
        ShopRow("Vinyl Kit - presses into " + PressBatch + " Labubus", "kit", 1, KitCost);
        ShopRow("Empty Blind Boxes (pack of 10)", "boxes", 10, BoxPackCost);
        for (int i = 0; i < Ing.Length; i++)
            ShopRow(Ing[i] + "  →  " + Fx[i] + " (+" + Mathf.RoundToInt(FxMult[i] * 100) + "% value)", "ing" + i, 1, IngCost[i]);
        GUILayout.Space(10);
        GUILayout.Label("Equipment (place it inside a property you own)", h2);
        for (int k = 0; k < 3; k++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(BBStation.Names[k] + "  ·  " + BB.Money(BBStation.Prices[k]), body, GUILayout.Width(440));
            GUI.enabled = cash >= BBStation.Prices[k];
            if (Btn("Buy", false, GUILayout.Width(80))) BuyItem("S:" + k, 1, BBStation.Prices[k]);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(10);
        GUILayout.Label("Upgrades", h2);
        Upgrade("Industrial Presses - 10 per kit, twice as fast", 800, upPress, () => upPress = true);
        Upgrade("Hoodie of Anonymity - cops spot you from much closer", 500, upHoodie, () => upHoodie = true);
        Upgrade("Rollerblades - move 30% faster", 400, upBlades, () => { upBlades = true; player.speedMult = 1.3f; });
        Upgrade("Second Burner Phone - customers text more often", 1200, upPhone, () => upPhone = true);
        Upgrade("Buy the Gas-n-Glam - go legit (win)", StorePrice, ownsStore, () =>
        {
            ownsStore = true;
            ShowMessage("YOU WENT LEGIT", "You bought the Gas-n-Glam. The Labubus are on the shelf now, technically legally.\n\nDay " + day + ", " + dealsDone + " deals. You win! Keep playing if you want.");
        });
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void ShopRow(string name, string id, int n, int cost)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name + "  ·  " + BB.Money(cost), body, GUILayout.Width(440));
        GUI.enabled = cash >= cost;
        if (Btn("Buy 1", false, GUILayout.Width(80))) BuyItem(id, n, cost);
        GUI.enabled = cash >= cost * 5;
        if (Btn("Buy 5", false, GUILayout.Width(80))) BuyItem(id, n * 5, cost * 5);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void Upgrade(string name, int cost, bool owned, System.Action give)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name + "  ·  " + BB.Money(cost), body, GUILayout.Width(440));
        GUI.enabled = !owned && cash >= cost;
        if (Btn(owned ? "Owned" : "Buy", false, GUILayout.Width(164))) { cash -= cost; give(); Play(clipCash); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    // ---------------- stash ----------------
    void DrawStash(float W, float H)
    {
        var a = Window(W, H, "Stash");
        GUILayout.BeginArea(a);
        GUILayout.Label("Cops take product from your pockets. Anything in here is safe. (Every property shares this stash.)", small);
        GUILayout.BeginHorizontal();
        if (Btn("Store all product", true, GUILayout.Height(36)))
            foreach (var id in HotIds("L:").Concat(HotIds("B:")).ToList()) StashStore(id, Has(id));
        if (Btn("Store everything", false, GUILayout.Height(36)))
            foreach (var s in hot.Where(x => x.n > 0).Select(x => x.id).Distinct().ToList()) StashStore(s, Has(s));
        GUILayout.EndHorizontal();
        GUILayout.Space(8);
        scrollA = GUILayout.BeginScrollView(scrollA);
        var keys = hot.Where(x => x.n > 0).Select(x => x.id).Union(stash.Keys).Distinct().ToList();
        if (keys.Count == 0) GUILayout.Label("Empty.", body);
        foreach (var k in keys)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(ItemName(k) + "\non you " + Has(k) + "  ·  stashed " + StashCount(k), body, GUILayout.Width(300));
            GUI.enabled = Has(k) > 0;
            if (Btn("Store", false, GUILayout.Width(80))) StashStore(k, Has(k));
            GUI.enabled = StashCount(k) > 0;
            if (Btn("Take 5", false, GUILayout.Width(80))) StashTake(k, 5);
            if (Btn("Take all", false, GUILayout.Width(90))) StashTake(k, StashCount(k));
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ---------------- ATM ----------------
    void DrawATM(float W, float H)
    {
        var a = Window(W, H, "Plushville Savings ATM", 520, 420);
        GUILayout.BeginArea(a);
        GUILayout.Label("Cash " + BB.Money(cash) + "      Bank " + BB.Money(bank), St(22, BBPal.Good, FontStyle.Bold));
        GUILayout.Label("Cops can fine your cash, never your bank. Properties are paid from the bank. Deposits today: " + BB.Money(depositedToday) + " / " + BB.Money(DailyDepositLimit), small);
        GUILayout.Space(10);
        GUILayout.Label("Deposit", h2);
        GUILayout.BeginHorizontal();
        if (Btn("$100", false, GUILayout.Height(40))) Deposit(100);
        if (Btn("$500", false, GUILayout.Height(40))) Deposit(500);
        if (Btn("$1,000", false, GUILayout.Height(40))) Deposit(1000);
        if (Btn("Max", true, GUILayout.Height(40))) Deposit(cash);
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.Label("Withdraw", h2);
        GUILayout.BeginHorizontal();
        if (Btn("$100", false, GUILayout.Height(40))) Withdraw(100);
        if (Btn("$500", false, GUILayout.Height(40))) Withdraw(500);
        if (Btn("$1,000", false, GUILayout.Height(40))) Withdraw(1000);
        if (Btn("All", false, GUILayout.Height(40))) Withdraw(bank);
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    // ---------------- dealer ----------------
    void DrawDealer(float W, float H)
    {
        var d = activeDealer;
        if (d == null) { CloseUI(); return; }
        var a = Window(W, H, d.name + " - your dealer", 760, 600);
        GUILayout.BeginArea(a);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Holding " + BB.Money(d.cash) + " for you (after their 20% cut)", St(17, BBPal.Good, FontStyle.Bold));
        GUI.enabled = d.cash > 0;
        if (Btn("Collect cash", true, GUILayout.Width(160), GUILayout.Height(36))) { cash += d.cash; d.cash = 0; Play(clipCash); }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(340));
        GUILayout.Label("Give boxed product", h2);
        var boxed = HotIds("B:");
        if (boxed.Count == 0) GUILayout.Label("No boxed product in your hotbar.", small);
        foreach (var id in boxed)
            if (Btn("Give " + Has(id) + "x " + catalog[KeyOf(id)].name, false))
            {
                int n = Has(id); Take(id, n);
                int v; d.stock.TryGetValue(id, out v); d.stock[id] = v + n;
                if (d.nextSaleAt < Time.time + 5) d.nextSaleAt = Time.time + 8f;
            }
        GUILayout.Space(8);
        GUILayout.Label("Their stock", h2);
        if (d.stock.Count == 0) GUILayout.Label("Empty.", small);
        foreach (var kv in d.stock.ToList())
            if (Btn("Take back " + kv.Value + "x " + catalog[KeyOf(kv.Key)].name, false))
            {
                int left = Add(kv.Key, kv.Value);
                if (left > 0) d.stock[kv.Key] = left; else d.stock.Remove(kv.Key);
            }
        GUILayout.EndVertical();
        GUILayout.Space(14);
        GUILayout.BeginVertical();
        GUILayout.Label("Clients (" + d.clients.Count + "/8)", h2);
        GUILayout.Label("Assigned customers buy from " + d.name + " instead of texting you.", small);
        scrollC = GUILayout.BeginScrollView(scrollC, GUILayout.Height(330));
        foreach (var c in customers.Where(x => x.unlocked))
        {
            bool mine = c.dealer == d;
            GUI.enabled = mine || c.dealer == null;
            if (Btn((mine ? "* " : "") + c.name + (c.dealer != null && !mine ? " (" + c.dealer.name + ")" : ""), mine)) AssignClient(d, c, !mine);
            GUI.enabled = true;
        }
        GUILayout.EndScrollView();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }
}
