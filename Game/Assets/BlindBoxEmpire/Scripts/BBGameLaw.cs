// Law & vehicles: wanted levels, body searches, handcuffs (mash E to break free), police cruisers, driving the van.
using System.Linq;
using UnityEngine;

public partial class BBGame
{
    public BBVehicle Vehicle;
    public bool PlayerDriving { get { return Vehicle != null && Vehicle.driving; } }
    public Vector3 PlayerPos { get { return player.transform.position; } }
    public int WantedLevel { get { return wanted; } }
    public bool PlayerFrozen { get { return cuffing; } }
    public bool CanSearch { get { return searchCop == null && !cuffing && Time.time > searchCooldownUntil; } }

    BBPoliceCar policeCar;
    int wanted;
    float chaseTime, noSightFor;
    BBCop searchCop; float searchEnd, searchCooldownUntil; Vector3 searchStart;
    bool cuffing, escapedThisChase; BBCop cuffCop; float cuffEnd; int cuffPresses;

    // ---------------- vehicle ----------------
    public void EnterVehicle(BBVehicle v)
    {
        if (cuffing || searchCop != null) return;
        v.driving = true;
        AddToast("WASD drive · Space brake · E get out", BBPal.Ink);
    }

    public void ExitVehicle()
    {
        if (!PlayerDriving) return;
        var v = Vehicle;
        v.driving = false;
        Vector3[] tries = { -v.transform.right * 2.3f, v.transform.right * 2.3f, -v.transform.forward * 3.6f, v.transform.forward * 3.6f };
        Vector3 basePos = v.transform.position + Vector3.up * 0.2f;
        Vector3 exit = basePos + tries[0];
        foreach (var t in tries)
        {
            Vector3 p = basePos + t;
            if (!Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.5f, 0.35f, ~0, QueryTriggerInteraction.Ignore)) { exit = p; break; }
        }
        exit.y = BBWorld.GroundY(exit) + 0.05f;
        player.Teleport(exit, v.transform.eulerAngles.y);
    }

    // ---------------- per-frame law ----------------
    void UpdateLaw()
    {
        bool chased = cops.Any(c => c.chasing) || (policeCar != null && !policeCar.leaving);
        if (chased)
        {
            chaseTime += Time.deltaTime;
            int nw = 1 + (chaseTime > 20f ? 1 : 0) + (chaseTime > 45f ? 1 : 0);
            if (nw > wanted) { wanted = nw; OnWantedUp(); }
            // a cruiser on its own gives up if it loses you for a while
            if (policeCar != null && !policeCar.leaving && !cops.Any(c => c.chasing))
            {
                bool sees = !Physics.Linecast(policeCar.transform.position + Vector3.up * 1.5f, PlayerPos + Vector3.up * 1.5f, ~0, QueryTriggerInteraction.Ignore)
                            || Vector3.Distance(policeCar.transform.position, PlayerPos) < 15f;
                noSightFor = sees ? 0 : noSightFor + Time.deltaTime;
                if (noSightFor > 10f || PlayerInHideout) { policeCar.leaving = true; OnCopLost(); }
            }
        }
        else if (wanted > 0)
        {
            wanted = 0; chaseTime = 0; escapedThisChase = false;
            if (policeCar != null) policeCar.leaving = true;
        }
        if (policeCar != null && policeCar.leaving && Vector3.Distance(policeCar.transform.position, PlayerPos) > 80f)
        { Destroy(policeCar.gameObject); policeCar = null; }

        // body search in progress
        if (searchCop != null)
        {
            if (PlayerDriving || Vector3.Distance(PlayerPos, searchStart) > 3f)
            {
                var c = searchCop; searchCop = null; c.searching = false;
                AddToast("You ran from a search!", BBPal.Bad);
                c.StartChase();
            }
            else if (Time.time >= searchEnd)
            {
                var c = searchCop; searchCop = null; c.searching = false;
                searchCooldownUntil = Time.time + 90f;
                bool dirty = hot.Any(s => s.n > 0 && (s.id.StartsWith("L:") || s.id.StartsWith("B:")));
                if (dirty) { AddToast("Officer: What's THIS? You're under arrest!", BBPal.Bad); StartCuff(c, false); }
                else AddToast("Officer: You're clean. Move along.", BBPal.Good);
            }
        }

        // being cuffed - mash E to break free
        if (cuffing)
        {
            if (BBInp.Interact) cuffPresses++;
            if (cuffPresses >= 7)
            {
                cuffing = false;
                if (cuffCop != null) { cuffCop.stunUntil = Time.time + 2.5f; if (!cuffCop.chasing) cuffCop.StartChase(); }
                escapedThisChase = true;
                chaseTime = Mathf.Max(chaseTime, 21f);
                AddToast("You broke free! RUN!", BBPal.Warn);
            }
            else if (Time.time >= cuffEnd) { cuffing = false; Busted(); }
        }
    }

    void OnWantedUp()
    {
        if (wanted == 2)
        {
            foreach (var c in cops) if (!c.chasing && Vector3.Distance(c.transform.position, PlayerPos) < 80f) c.StartChase();
            AddToast("Backup is on the way!", BBPal.Bad);
        }
        if (wanted >= 3 && policeCar == null)
        {
            // spawn the cruiser at the nearest intersection that's at least 45 m away
            Vector3 best = BBWorld.I(0, 0); float bestD = 1e9f;
            for (int a = 0; a < BBWorld.N; a++)
                for (int b = 0; b < BBWorld.N; b++)
                {
                    Vector3 p = BBWorld.I(a, b);
                    float d = Vector3.Distance(p, PlayerPos);
                    if (d > 45f && d < bestD) { bestD = d; best = p; }
                }
            Vector3 to = PlayerPos - best; to.y = 0;
            policeCar = BBPoliceCar.Spawn(best + Vector3.up * 0.2f, Quaternion.LookRotation(to).eulerAngles.y, npcRoot);
            noSightFor = 0;
            AddToast("A police cruiser is coming for you!", BBPal.Bad);
        }
    }

    public void BeginSearch(BBCop c)
    {
        if (!CanSearch) return;
        searchCop = c; c.searching = true;
        searchEnd = Time.time + 4f; searchStart = PlayerPos;
        AddToast("Officer: Hold it right there. Body search - don't move.", BBPal.Warn);
    }

    public void CopReachedPlayer(BBCop c)
    {
        if (PlayerDriving || cuffing) return;
        StartCuff(c, !escapedThisChase);
    }

    void StartCuff(BBCop c, bool canEscape)
    {
        cuffing = true; cuffCop = c; cuffPresses = canEscape ? 0 : -1000;
        cuffEnd = Time.time + 2.6f;
        if (canEscape) AddToast("You're being cuffed - MASH E!", BBPal.Bad);
    }

    public void PulledOver()
    {
        if (!PlayerDriving) return;
        Vehicle.speed = 0;
        ExitVehicle();
        AddToast("Pulled over! Step out of the vehicle.", BBPal.Bad);
        StartCuff(null, !escapedThisChase);
    }

    void ResetLaw()
    {
        if (PlayerDriving) { Vehicle.driving = false; Vehicle.speed = 0; }
        cuffing = false; cuffCop = null;
        if (searchCop != null) { searchCop.searching = false; searchCop = null; }
        wanted = 0; chaseTime = 0; escapedThisChase = false;
        if (policeCar != null) policeCar.leaving = true;
    }

    // ---------------- HUD bits ----------------
    void DrawLawHud(float W, float H)
    {
        if (wanted > 0)
        {
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(W / 2 - 54 + i * 38, 66, 30, 30);
                Fill(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), A(Color.black, 0.5f));
                Fill(r, i < wanted ? (Mathf.Repeat(Time.time, 0.5f) < 0.25f ? BBPal.Bad : BBPal.Warn) : A(BBPal.Panel, 0.7f));
            }
        }
        if (searchCop != null)
        {
            var r = new Rect(W / 2 - 230, H / 2 - 110, 460, 54);
            Fill(r, A(BBPal.Panel, 0.92f));
            GUI.Label(r, "BODY SEARCH - stand still (" + Mathf.CeilToInt(searchEnd - Time.time) + ")\nRun and they'll chase you.", St(17, BBPal.Warn, FontStyle.Bold, TextAnchor.MiddleCenter));
        }
        if (cuffing)
        {
            var r = new Rect(W / 2 - 230, H / 2 - 120, 460, 70);
            Fill(r, A(BBPal.Bad, 0.9f));
            bool can = cuffPresses >= 0;
            GUI.Label(new Rect(r.x, r.y + 4, r.width, 36), can ? "BEING CUFFED - MASH E!" : "ARRESTED", St(24, BBPal.Ink, FontStyle.Bold, TextAnchor.MiddleCenter));
            if (can)
            {
                Fill(new Rect(r.x + 30, r.y + 46, r.width - 60, 12), A(Color.black, 0.4f));
                Fill(new Rect(r.x + 30, r.y + 46, (r.width - 60) * Mathf.Clamp01(cuffPresses / 7f), 12), BBPal.Ink);
            }
        }
    }
}
