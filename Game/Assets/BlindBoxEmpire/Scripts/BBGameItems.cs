// Hotbar inventory, production stations, equipment placement, stash, shop, ATM and properties.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class BBGame
{
    public class Slot { public string id; public int n; }
    public readonly Slot[] hot = new Slot[8];
    public int sel;
    readonly Dictionary<string, int> stash = new Dictionary<string, int>();
    BBStation activeStation;

    // ---------------- item catalogue ----------------
    // ids: kit · boxes · ing0..ing5 · L:<product> (loose) · B:<product> (boxed, sellable) · S:<kind> (equipment crate)
    public string HeldId { get { var s = hot[sel]; return s.n > 0 ? s.id : null; } }

    static int StackMax(string id)
    {
        if (id.StartsWith("S:")) return 1;
        if (id == "kit") return 10;
        if (id == "boxes") return 40;
        return 20;
    }

    public string ItemName(string id)
    {
        if (id == null) return "";
        if (id == "kit") return "Vinyl Kit";
        if (id == "boxes") return "Empty Blind Box";
        if (id.StartsWith("ing")) return Ing[int.Parse(id.Substring(3))];
        if (id.StartsWith("S:")) return BBStation.Names[int.Parse(id.Substring(2))] + " (crate)";
        BBProduct p;
        if (catalog.TryGetValue(KeyOf(id), out p)) return p.name + (id.StartsWith("B:") ? " (boxed)" : " (loose)");
        return id;
    }

    public Color ItemColor(string id)
    {
        if (id == null) return BBPal.Panel;
        if (id == "kit") return BBPal.Bubblegum;
        if (id == "boxes") return BBPal.Ink;
        if (id.StartsWith("ing")) return FxCol[int.Parse(id.Substring(3))];
        if (id.StartsWith("S:")) return BBPal.Wood;
        BBProduct p;
        if (catalog.TryGetValue(KeyOf(id), out p) && p.fx.Count > 0) return FxCol[p.fx[p.fx.Count - 1]];
        return id.StartsWith("B:") ? BBPal.Accent : BBPal.Bubblegum;
    }

    public string ItemShort(string id)
    {
        if (id == null) return "";
        if (id == "kit") return "KIT";
        if (id == "boxes") return "BOX";
        if (id.StartsWith("ing")) return Ing[int.Parse(id.Substring(3))].Substring(0, 3).ToUpper();
        if (id.StartsWith("S:")) return new[] { "PRS", "MIX", "PKG" }[int.Parse(id.Substring(2))];
        BBProduct p;
        string pre = id.StartsWith("B:") ? "#" : "";
        if (catalog.TryGetValue(KeyOf(id), out p)) return pre + (p.fx.Count == 0 ? "PLN" : string.Join("", p.fx.Select(x => Adj[x].Substring(0, 1)).ToArray()));
        return "?";
    }

    // ---------------- inventory ops ----------------
    public int Has(string id) { int t = 0; foreach (var s in hot) if (s.n > 0 && s.id == id) t += s.n; return t; }

    // returns how many could NOT be added
    public int Add(string id, int n)
    {
        int max = StackMax(id);
        foreach (var s in hot)
        {
            if (n <= 0) break;
            if (s.n > 0 && s.id == id && s.n < max) { int k = Mathf.Min(n, max - s.n); s.n += k; n -= k; }
        }
        foreach (var s in hot)
        {
            if (n <= 0) break;
            if (s.n <= 0) { int k = Mathf.Min(n, max); s.id = id; s.n = k; n -= k; }
        }
        return n;
    }

    public bool Take(string id, int n)
    {
        if (Has(id) < n) return false;
        for (int i = hot.Length - 1; i >= 0 && n > 0; i--)
        {
            var s = hot[i];
            if (s.n > 0 && s.id == id) { int k = Mathf.Min(n, s.n); s.n -= k; n -= k; if (s.n == 0) s.id = null; }
        }
        return true;
    }

    int RoomFor(string id)
    {
        int max = StackMax(id), room = 0;
        foreach (var s in hot) room += s.n <= 0 ? max : s.id == id ? max - s.n : 0;
        return room;
    }

    List<string> HotIds(string prefix)
    {
        var l = new List<string>();
        foreach (var s in hot) if (s.n > 0 && s.id.StartsWith(prefix) && !l.Contains(s.id)) l.Add(s.id);
        return l;
    }

    // ---------------- stations ----------------
    BBStation PlaceStation(BBStationKind k, Vector3 pos, float yaw)
    {
        var go = BBStation.Create(k, pos, yaw, root);
        var st = go.GetComponent<BBStation>();
        stations.Add(st);
        var it = go.AddComponent<BBInteract>();
        it.prompt = () => StationPrompt(st);
        it.use = () => UseStation(st);
        return st;
    }

    string StationPrompt(BBStation st)
    {
        string name = BBStation.Names[(int)st.kind];
        if (st.Busy) return name + " working... " + Mathf.RoundToInt(st.Fraction * 100) + "%  (" + st.outCount + "/" + (st.inCount + st.outCount) + ")";
        if (st.HasOutput) return "[E] Collect " + st.outCount + "x " + ItemName(st.outId);
        switch (st.kind)
        {
            case BBStationKind.Press:
                return Has("kit") > 0 ? "[E] Load a Vinyl Kit (" + Has("kit") + ") - makes " + PressBatch + " Labubus" : "Plush Press - needs a Vinyl Kit (Gas-n-Glam)";
            case BBStationKind.Mixer: return "[E] Use Mixing Station";
            default: return "[E] Use Packaging Station";
        }
    }

    void UseStation(BBStation st)
    {
        if (st.Busy) return;
        if (st.HasOutput)
        {
            int left = Add(st.outId, st.outCount);
            int got = st.outCount - left;
            st.outCount = left;
            if (got > 0) { AddToast("+" + got + " " + ItemName(st.outId), BBPal.Good); Play(clipDing); }
            if (left > 0) AddToast("Hotbar full - " + left + " left on the station.", BBPal.Warn);
            return;
        }
        switch (st.kind)
        {
            case BBStationKind.Press:
                if (!Take("kit", 1)) { AddToast("You need a Vinyl Kit. Gas-n-Glam sells them.", BBPal.Warn); return; }
                st.inCount = PressBatch; st.outCount = 0; st.progress = 0;
                st.outId = "L:" + GetProduct(new List<int>()).key;
                st.unitTime = (float)PressTime / PressBatch;
                everPressed = true;
                AddToast("Pressing " + PressBatch + " Labubus...", BBPal.Ink);
                break;
            case BBStationKind.Mixer: activeStation = st; mixProduct = null; mixIng = -1; ui = UI.Mixer; break;
            default: activeStation = st; mixProduct = null; ui = UI.Packager; break;
        }
    }

    public void OnStationDone(BBStation st)
    {
        AddToast(BBStation.Names[(int)st.kind] + " finished.", BBPal.Good);
        Play(clipDone);
    }

    void StartMix(BBProduct src, int ingIdx, int n)
    {
        var fx = new List<int>(src.fx);
        if (fx.Contains(ingIdx)) return;
        fx.Add(ingIdx); if (fx.Count > 4) fx.RemoveAt(0);
        var res = GetProduct(fx);
        n = Mathf.Min(n, Has("L:" + src.key), Has("ing" + ingIdx));
        if (n <= 0) return;
        Take("L:" + src.key, n); Take("ing" + ingIdx, n);
        var st = activeStation;
        st.inCount = n; st.outCount = 0; st.progress = 0; st.outId = "L:" + res.key; st.unitTime = 1.4f;
        everMixed = true;
        AddToast("Mixing " + n + "x " + res.name + "...", BBPal.Ink);
        CloseUI();
    }

    void StartPack(BBProduct src, int n)
    {
        n = Mathf.Min(n, Has("L:" + src.key), Has("boxes"));
        if (n <= 0) return;
        Take("L:" + src.key, n); Take("boxes", n);
        var st = activeStation;
        st.inCount = n; st.outCount = 0; st.progress = 0; st.outId = "B:" + src.key; st.unitTime = 0.7f;
        everPacked = true;
        AddToast("Boxing " + n + "x " + src.name + "...", BBPal.Ink);
        CloseUI();
    }

    // ---------------- placement of bought equipment ----------------
    GameObject ghost; int ghostKind = -1; float ghostYaw; Material ghostMat;

    void UpdatePlacement()
    {
        string h = HeldId;
        int kind = (!UIOpen && !PlayerDriving && h != null && h.StartsWith("S:")) ? int.Parse(h.Substring(2)) : -1;
        if (kind != ghostKind)
        {
            if (ghost != null) Destroy(ghost);
            ghost = null; ghostKind = kind;
            if (kind >= 0)
            {
                ghost = BBStation.Create((BBStationKind)kind, Vector3.zero, 0, null, true);
                ghostMat = BB.NewMat(BBPal.Good);
                ghost.GetComponent<MeshRenderer>().sharedMaterial = ghostMat;
            }
        }
        placePrompt = null;
        if (ghost == null) return;
        if (BBInp.Rotate) ghostYaw += 90;
        var cam = player.cam.transform;
        RaycastHit hit;
        bool valid = false;
        Vector3 pos = cam.position + cam.forward * 3f; pos.y = 0;
        if (Physics.Raycast(cam.position, cam.forward, out hit, 7f, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
        {
            pos = hit.point + Vector3.up * 0.1f;
            pos.x = Mathf.Round(pos.x * 4) / 4; pos.z = Mathf.Round(pos.z * 4) / 4;
            Vector3 size = BBStation.Size[kind];
            Quaternion rot = Quaternion.Euler(0, ghostYaw, 0);
            bool inside = false;
            var bounds = new List<Bounds> { w.hideout };
            foreach (var pr in w.props) if (pr.owned) bounds.Add(pr.bounds);
            foreach (var b in bounds)
            {
                bool all = true;
                for (int cx = -1; cx <= 1; cx += 2)
                    for (int cz = -1; cz <= 1; cz += 2)
                    {
                        Vector3 corner = pos + rot * new Vector3(cx * size.x / 2, 1f, cz * size.z / 2);
                        if (!b.Contains(corner)) all = false;
                    }
                if (all) { inside = true; break; }
            }
            var hits = Physics.OverlapBox(pos + Vector3.up * (size.y / 2 + 0.06f), size * 0.47f, rot, ~0, QueryTriggerInteraction.Ignore);
            bool clear = true;
            foreach (var c in hits) if (c.GetComponentInParent<BBPlayer>() == null) { clear = false; break; }
            valid = inside && clear;
            placePrompt = !inside ? "Equipment must go inside a property you own" : !clear ? "Not enough room here" : "[E] Place " + BBStation.Names[kind] + "   [R] Rotate";
            if (valid && BBInp.Interact)
            {
                Take(h, 1);
                PlaceStation((BBStationKind)kind, pos, ghostYaw);
                AddToast(BBStation.Names[kind] + " placed.", BBPal.Good);
                Play(clipDone);
            }
        }
        else placePrompt = "Look at the floor of a property you own";
        ghost.transform.position = pos;
        ghost.transform.rotation = Quaternion.Euler(0, ghostYaw, 0);
        BB.SetCol(ghostMat, valid ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.45f, 0.45f));
    }

    // ---------------- fixed interactables: stash, bed, shop, ATMs, for-sale signs ----------------
    void WireFixed()
    {
        Hook(w.stash, () => "[E] Stash (" + stash.Values.Sum() + " items stored)", () => ui = UI.Stash);
        Hook(w.bed, () => Curfew || minutes >= 20 * 60 ? "[E] Sleep until morning" : "Bed - you can sleep after 8 PM", Sleep);
        Hook(w.counter, () => "[E] Shop at Gas-n-Glam", () => ui = UI.Shop);
        foreach (var a in w.atms) Hook(a, () => "[E] Use ATM", () => ui = UI.ATM);
        foreach (var pr in w.props)
        {
            var p = pr;
            var go = new GameObject("For Sale " + p.name);
            go.transform.SetParent(root, false);
            go.transform.position = p.signPos;
            go.transform.rotation = Quaternion.Euler(0, p.signYaw, 0);
            BB.CBox(go.transform, new Vector3(0, 0.6f, 0), new Vector3(0.1f, 1.2f, 0.1f), BBPal.WoodDark);
            BB.CBox(go.transform, new Vector3(0, 1.35f, 0), new Vector3(1.3f, 0.8f, 0.08f), BBPal.Trim);
            BB.CBox(go.transform, new Vector3(0, 1.35f, -0.045f), new Vector3(1.2f, 0.18f, 0.02f), BBPal.Bad);
            BB.Sign("FOR SALE\n" + BB.Money(p.price), go.transform.position + go.transform.rotation * new Vector3(0, 1.32f, -0.07f), p.signYaw, 0.035f, BBPal.Panel, go.transform);
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 1f, 0); bc.size = new Vector3(1.3f, 2f, 0.4f);
            p.sign = go;
            Hook(go, () => "[E] Buy the " + p.name + " for " + BB.Money(p.price) + " (from bank)", () => BuyProperty(p));
        }
    }

    static void Hook(GameObject go, System.Func<string> p, System.Action u)
    {
        var it = go.AddComponent<BBInteract>(); it.prompt = p; it.use = u;
    }

    void BuyProperty(BBPropertyDef p)
    {
        if (p.owned) return;
        if (bank < p.price) { AddToast("Not enough in the bank (" + BB.Money(bank) + "). Deposit cash at an ATM.", BBPal.Warn); return; }
        bank -= p.price; p.owned = true;
        if (p.sign != null) Destroy(p.sign);
        Play(clipCash);
        ShowMessage(p.name.ToUpper() + " IS YOURS", "You bought the " + p.name + ".\n\nBuy equipment at Gas-n-Glam, select the crate in your hotbar and place it inside. Cops can't follow you into property you own.");
    }

    void BuyItem(string id, int n, int cost)
    {
        if (cash < cost) { AddToast("Not enough cash.", BBPal.Warn); return; }
        if (RoomFor(id) < n) { AddToast("Hotbar full - stash something first.", BBPal.Warn); return; }
        cash -= cost; Add(id, n); Play(clipCash);
    }

    void Deposit(int amount)
    {
        amount = Mathf.Min(amount, cash, DailyDepositLimit - depositedToday);
        if (amount <= 0) { AddToast("Daily deposit limit reached (" + BB.Money(DailyDepositLimit) + ").", BBPal.Warn); return; }
        cash -= amount; bank += amount; depositedToday += amount; Play(clipCash);
    }

    void Withdraw(int amount)
    {
        amount = Mathf.Min(amount, bank);
        if (amount <= 0) return;
        bank -= amount; cash += amount; Play(clipCash);
    }

    void StashStore(string id, int n)
    {
        n = Mathf.Min(n, Has(id));
        if (n <= 0) return;
        Take(id, n);
        int v; stash.TryGetValue(id, out v); stash[id] = v + n;
    }

    void StashTake(string id, int n)
    {
        int v; stash.TryGetValue(id, out v);
        n = Mathf.Min(n, v, RoomFor(id));
        if (n <= 0) { if (v > 0) AddToast("Hotbar full.", BBPal.Warn); return; }
        Add(id, n);
        if (v - n <= 0) stash.Remove(id); else stash[id] = v - n;
    }
}
