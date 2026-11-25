// Customers (texts, offers, counteroffers, deals, routines, samples) and dealers you hire.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BBGame
{
    static readonly string[] Names = { "Mira", "Dex", "Grandma June", "Kenji", "Priya", "Big Rob", "Skyler", "Off-Duty Dan", "Lulu", "Tobias", "Wren", "Coach Patti", "Mateo", "Ivy" };
    readonly List<BBCustomer> customers = new List<BBCustomer>();
    readonly List<BBDealer> dealers = new List<BBDealer>();
    BBDealer activeDealer;

    void MakeCustomers()
    {
        int n = BBWorld.N;
        foreach (var nm in Names)
        {
            var c = new BBCustomer();
            c.name = nm;
            c.style = BBHumanStyle.Random(new System.Random(nm.GetHashCode() & 0x7fffffff));
            int a = Random.Range(0, 6), b; do { b = Random.Range(0, 6); } while (b == a);
            c.prefs = new[] { a, b };
            do { c.homeA = Random.Range(0, n); c.homeB = Random.Range(0, n); } while (c.homeA == 0 && c.homeB == 0);
            do { c.workA = Random.Range(0, n); c.workB = Random.Range(0, n); } while (c.workA == 0 && c.workB == 0);
            customers.Add(c);
        }
        customers[0].unlocked = customers[1].unlocked = true;
    }

    int Matches(BBCustomer c, BBProduct p) { int m = 0; foreach (int f in c.prefs) if (p.fx.Contains(f)) m++; return m; }
    int Fair(BBCustomer c, BBProduct p) { return Mathf.RoundToInt(p.value * (1 + 0.35f * Matches(c, p)) * (0.85f + 0.6f * c.obs)); }
    static float Odds(int price, int fair) { float r = (float)price / Mathf.Max(1, fair); return r <= 1f ? 1f : Mathf.Clamp01(1f - (r - 1f) * 2.5f); }
    static int Round5(float v) { return Mathf.Max(5, Mathf.RoundToInt(v / 5f) * 5); }

    void Say(BBCustomer c, string line) { c.log.Add(line); if (c.log.Count > 30) c.log.RemoveAt(0); }

    // ---------------- per-frame ----------------
    void UpdateCustomers()
    {
        foreach (var c in customers)
        {
            if (c.state == 1 && T > c.reqExpire) { c.state = 0; c.counter = false; Say(c, c.name + ": nvm found someone else"); }
            if (c.state == 2 && T > c.dealDeadline)
            {
                c.state = 0; c.obs = Mathf.Max(0, c.obs - 0.1f);
                EndNpc(c);
                Say(c, c.name + ": u ghosted me. wow.");
                AddToast(c.name + " gave up waiting.", BBPal.Bad);
            }
            // potential customers wait at home for a free sample
            if (c.potential && c.npc == null && Time.time >= c.sampleRetryAt && !Curfew)
            {
                c.spot = Corner(c.homeA, c.homeB);
                c.spotName = BBWorld.XStreets[c.homeA] + " & " + BBWorld.ZStreets[c.homeB];
                SpawnNpc(c, BBPal.Butter);
            }
        }
    }

    void TrySendText()
    {
        var pool = customers.Where(c => c.unlocked && c.dealer == null && c.state == 0 && c.npc == null).ToList();
        if (pool.Count == 0) return;
        var c2 = pool[Random.Range(0, pool.Count)];
        c2.state = 1; c2.counter = false;
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

    // ---------------- offers ----------------
    void SendOffer(BBCustomer c, BBProduct p, int qty, int price)
    {
        Say(c, "You: " + qty + "x " + p.name + " for " + BB.Money(price * qty));
        int fair = Fair(c, p);
        if (Random.value <= Odds(price, fair)) { ArrangeDeal(c, p.key, qty, price); return; }
        if (Random.value < 0.65f)
        {
            c.counter = true; c.counterKey = p.key; c.counterQty = qty;
            c.counterPrice = Round5(fair * Random.Range(0.85f, 1.0f));
            string[] ct = { "too steep. {p} each and we got a deal", "how about {p} each?", "i can do {p} a pop. final offer" };
            Say(c, c.name + ": " + ct[Random.Range(0, ct.Length)].Replace("{p}", BB.Money(c.counterPrice)));
            return;
        }
        c.obs = Mathf.Max(0, c.obs - 0.03f);
        string[] no = { "lol no. too expensive", "bro that's robbery", "i'm broke not stupid", "counter-offer: no" };
        Say(c, c.name + ": " + no[Random.Range(0, no.Length)]);
    }

    void AcceptCounter(BBCustomer c)
    {
        c.counter = false;
        Say(c, "You: deal.");
        ArrangeDeal(c, c.counterKey, c.counterQty, c.counterPrice);
    }

    void ArrangeDeal(BBCustomer c, string key, int qty, int price)
    {
        c.state = 2; c.counter = false; c.dealKey = key; c.dealQty = qty; c.dealPrice = price;
        c.dealDeadline = T + 240;
        bool atWork = minutes >= 8 * 60 && minutes < 17 * 60;
        int a = atWork ? c.workA : c.homeA, b = atWork ? c.workB : c.homeB;
        c.spot = Corner(a, b);
        c.spotName = BBWorld.XStreets[a] + " & " + BBWorld.ZStreets[b];
        SpawnNpc(c, BBPal.Accent);
        Say(c, c.name + ": bet. meet me at " + c.spotName + (atWork ? " (near work)" : " (near my place)") + " before " + Clock(c.dealDeadline % 1440));
        AddToast("Deal set: " + c.spotName + " (pink beacon)", BBPal.Good);
    }

    void SpawnNpc(BBCustomer c, Color beacon)
    {
        int n = BBWorld.N;
        int cx = Mathf.Clamp(Mathf.RoundToInt(c.spot.x / BBWorld.S), 0, n - 1), cz = Mathf.Clamp(Mathf.RoundToInt(c.spot.z / BBWorld.S), 0, n - 1);
        int ax, az;
        do { ax = Random.Range(0, n); az = Random.Range(0, n); } while (Mathf.Abs(ax - cx) + Mathf.Abs(az - cz) < 2);
        var path = new List<Vector3> { BBWorld.I(ax, az), BBWorld.I(cx, az), BBWorld.I(cx, cz), c.spot };
        c.npc = BBCustomerNpc.Spawn(c, path, npcRoot);
        c.beacon = BB.Prim(PrimitiveType.Cylinder, c.spot + new Vector3(0, 20, 0), new Vector3(0.3f, 20, 0.3f), BB.Mat(beacon, 2f), npcRoot, false, "Beacon");
    }

    void EndNpc(BBCustomer c)
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

    // ---------------- meeting in person ----------------
    public string DealPrompt(BBCustomer c)
    {
        if (c.npc == null || c.npc.leaving) return null;
        if (!c.npc.waiting) return c.name + " is on the way";
        if (c.potential)
        {
            string sample = SampleId();
            return sample != null ? "[E] Give " + c.name + " a free sample: " + ItemName(sample) : c.name + " wants a free sample - you need a boxed Labubu";
        }
        if (c.state != 2) return null;
        var p = catalog[c.dealKey];
        int have = Has("B:" + c.dealKey);
        return have >= c.dealQty
            ? "[E] Hand " + c.name + " " + c.dealQty + "x " + p.name + " for " + BB.Money(c.dealQty * c.dealPrice)
            : c.name + " wants " + c.dealQty + "x boxed " + p.name + " - you have " + have + " (loose ones don't count)";
    }

    string SampleId()
    {
        string h = HeldId;
        if (h != null && h.StartsWith("B:")) return h;
        var l = HotIds("B:");
        return l.Count > 0 ? l[0] : null;
    }

    public void CompleteDeal(BBCustomer c)
    {
        if (c.npc == null || !c.npc.waiting) return;
        if (c.potential) { GiveSample(c); return; }
        if (c.state != 2) return;
        if (!Take("B:" + c.dealKey, c.dealQty)) { AddToast("You don't have enough boxed product on you.", BBPal.Warn); return; }
        var p = catalog[c.dealKey];
        int total = c.dealQty * c.dealPrice;
        cash += total; dealsDone++;
        int m = Matches(c, p);
        c.obs = Mathf.Clamp01(c.obs + 0.08f + 0.07f * m);
        c.state = 0;
        string[] thx = { "omg it's so ugly. i love it", "pleasure doing business", "my collection grows", "same time tomorrow?" };
        Say(c, c.name + ": " + (m > 0 ? "EXACTLY what i wanted!! " : "") + thx[Random.Range(0, thx.Length)]);
        AddToast("+" + BB.Money(total) + (m > 0 ? "  (" + c.name + " loved it)" : ""), BBPal.Good);
        Play(clipCash);
        EndNpc(c);
        WitnessCheck();

        var locked = customers.FirstOrDefault(x => !x.unlocked && !x.potential);
        if (locked != null && ((c.obs >= 0.3f && !c.referred) || dealsDone % 4 == 0))
        {
            if (c.obs >= 0.3f) c.referred = true;
            locked.potential = true; locked.sampleRetryAt = 0;
            Say(c, c.name + ": my friend " + locked.name + " wants to try ur stuff. they're near " + BBWorld.XStreets[locked.homeA] + " & " + BBWorld.ZStreets[locked.homeB] + ". bring a sample");
            c.unread++;
            AddToast(locked.name + " wants a free sample (yellow beacon)", BBPal.Butter);
        }
    }

    void GiveSample(BBCustomer c)
    {
        string id = SampleId();
        if (id == null) { AddToast("You need a boxed Labubu to give as a sample.", BBPal.Warn); return; }
        var p = catalog[KeyOf(id)];
        Take(id, 1);
        float chance = Mathf.Clamp(0.35f + 0.18f * Matches(c, p) + (p.value - BaseValue) / 120f, 0.2f, 0.95f);
        if (Random.value < chance)
        {
            c.potential = false; c.unlocked = true;
            Say(c, c.name + ": ok this slaps. i'll text u when i need more");
            AddToast(c.name + " is now a customer!", BBPal.Good);
            Play(clipCash);
        }
        else
        {
            c.sampleRetryAt = Time.time + 150f;
            Say(c, c.name + ": meh. not really my thing. maybe something " + Fx[c.prefs[0]].ToLower() + "?");
            AddToast(c.name + " wasn't impressed. Try something " + Fx[c.prefs[0]] + " later.", BBPal.Warn);
        }
        c.unread++;
        EndNpc(c);
        WitnessCheck();
    }

    void WitnessCheck()
    {
        if (Curfew) return;
        foreach (var cop in cops)
            if (!cop.chasing && cop.CanSee(player.transform.position, SpotRange * 0.9f)) { cop.StartChase(); break; }
    }

    // ---------------- dealers ----------------
    void MakeDealers()
    {
        var data = new[] {
            new { n = "Benji", fee = 500, d = 3, a = 1, b = 2, sx = -1f, sz = 1f },
            new { n = "Molly", fee = 1500, d = 12, a = 3, b = 2, sx = 1f, sz = -1f },
            new { n = "Brad", fee = 3000, d = 25, a = 2, b = 4, sx = 1f, sz = -1f }
        };
        int k = 0;
        foreach (var x in data)
        {
            var d = new BBDealer();
            d.name = x.n; d.fee = x.fee; d.unlockDeals = x.d;
            d.spot = BBWorld.I(x.a, x.b) + new Vector3(x.sx * 5.2f, BBWorld.Curb, x.sz * 5.2f);
            d.style = BBHumanStyle.Random(new System.Random(900 + k++));
            d.style.jacket = true;
            dealers.Add(d);
        }
    }

    void UpdateDealers()
    {
        foreach (var d in dealers)
        {
            if (!d.available && dealsDone >= d.unlockDeals)
            {
                d.available = true;
                var go = new GameObject("Dealer " + d.name);
                go.transform.SetParent(npcRoot, false);
                go.transform.position = d.spot;
                var h = BBHuman.Create(go.transform, d.style);
                h.lookTarget = player.cam.transform;
                var col = go.AddComponent<CapsuleCollider>(); col.height = 2f; col.radius = 0.4f; col.center = new Vector3(0, 1f, 0);
                var dd = d;
                Hook(go, () => dd.hired ? "[E] Talk to " + dd.name + " (dealer)" : "[E] Hire " + dd.name + " for " + BB.Money(dd.fee) + " - keeps 20% of sales", () => UseDealer(dd));
                d.npc = go;
                var tx = customers[0];
                Say(tx, d.name + " (unknown number): heard u got product. i can move it for u. " + BB.Money(d.fee) + " to start. i'm at " + SpotName(d.spot));
                tx.unread++;
                AddToast("A dealer wants to work for you: " + d.name, BBPal.Accent);
                Play(clipDing);
            }
            if (d.npc != null)
            {
                Vector3 to = player.transform.position - d.npc.transform.position; to.y = 0;
                if (to.magnitude < 10f && to.sqrMagnitude > 0.01f)
                    d.npc.transform.rotation = Quaternion.Slerp(d.npc.transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 3f);
            }
            if (!d.hired || d.clients.Count == 0 || Time.time < d.nextSaleAt) continue;
            d.nextSaleAt = Time.time + Random.Range(25f, 50f);
            var c = d.clients[Random.Range(0, d.clients.Count)];
            if (d.stock.Count == 0) { if (Random.value < 0.3f) DealerLog(d, d.name + ": I'm out of product. Come restock me."); continue; }
            string best = null; float bestScore = -1;
            foreach (var kv in d.stock)
            {
                var p = catalog[KeyOf(kv.Key)];
                float sc = Matches(c, p) * 100 + p.value;
                if (sc > bestScore) { bestScore = sc; best = kv.Key; }
            }
            var prod = catalog[KeyOf(best)];
            int qty = Mathf.Min(d.stock[best], Random.Range(1, 3 + Mathf.RoundToInt(c.obs * 4)));
            int total = Fair(c, prod) * qty;
            int cut = Mathf.RoundToInt(total * 0.2f);
            d.stock[best] -= qty; if (d.stock[best] <= 0) d.stock.Remove(best);
            d.cash += total - cut;
            c.obs = Mathf.Clamp01(c.obs + 0.04f + 0.03f * Matches(c, prod));
            dealsDone++;
            DealerLog(d, d.name + " sold " + qty + "x " + prod.name + " to " + c.name + " (+" + BB.Money(total - cut) + " for you)");
            AddToast(d.name + " sold " + qty + "x to " + c.name + " (+" + BB.Money(total - cut) + ")", BBPal.Good);
        }
    }

    void DealerLog(BBDealer d, string s) { d.log.Add(s); if (d.log.Count > 12) d.log.RemoveAt(0); }

    static string SpotName(Vector3 p)
    {
        int a = Mathf.Clamp(Mathf.RoundToInt(p.x / BBWorld.S), 0, BBWorld.N - 1), b = Mathf.Clamp(Mathf.RoundToInt(p.z / BBWorld.S), 0, BBWorld.N - 1);
        return BBWorld.XStreets[a] + " & " + BBWorld.ZStreets[b];
    }

    void UseDealer(BBDealer d)
    {
        if (!d.hired)
        {
            if (cash < d.fee) { AddToast("You need " + BB.Money(d.fee) + " cash to hire " + d.name + ".", BBPal.Warn); return; }
            cash -= d.fee; d.hired = true; Play(clipCash);
            DealerLog(d, d.name + ": let's get it. give me boxed product and some clients.");
        }
        activeDealer = d; ui = UI.Dealer;
    }

    void AssignClient(BBDealer d, BBCustomer c, bool on)
    {
        if (on)
        {
            if (c.dealer != null || d.clients.Count >= 8) return;
            if (c.state == 2) { AddToast("Finish " + c.name + "'s open deal first.", BBPal.Warn); return; }
            c.dealer = d; d.clients.Add(c); c.state = 0; c.counter = false;
            Say(c, c.name + ": ok i'll hit up " + d.name + " from now on");
        }
        else { c.dealer = null; d.clients.Remove(c); }
    }
}
