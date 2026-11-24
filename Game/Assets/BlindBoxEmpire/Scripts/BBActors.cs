// Player controller, interactables, customers and cops.
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

    void Awake()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.4f;
        var camGo = new GameObject("Player Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.65f, 0);
        cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 72; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.AddComponent<AudioListener>();
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

        if (!ui)
        {
            Vector2 look = BBInp.Look();
            yaw += look.x; pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
        }
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        Vector2 mv = ui ? Vector2.zero : BBInp.Move();
        Sprinting = !ui && BBInp.Sprint && mv.y > 0;
        float speed = (Sprinting ? 7.5f : 4.5f) * speedMult;
        Vector3 dir = (transform.forward * mv.y + transform.right * mv.x);
        if (dir.sqrMagnitude > 1) dir.Normalize();
        if (cc.isGrounded) { vy = -1f; if (!ui && BBInp.Jump) vy = 5.5f; }
        else vy -= 18f * Time.deltaTime;
        cc.Move((dir * speed + Vector3.up * vy) * Time.deltaTime);

        // Head bob: a little life in the walk.
        bob += dir.magnitude * speed * Time.deltaTime * 1.6f;
        float b = cc.isGrounded ? Mathf.Sin(bob) * 0.04f * dir.magnitude : 0;
        cam.transform.localPosition = new Vector3(0, 1.65f + b, 0);
        cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, Sprinting ? 80f : 72f, Time.deltaTime * 6f);

        g.prompt = null;
        if (ui) return;
        RaycastHit hit;
        if (Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, 3.4f))
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

public class BBCustomerNpc : MonoBehaviour
{
    public BBCustomer data;
    public List<Vector3> path = new List<Vector3>();
    public bool leaving, waiting;
    int idx;
    const float Speed = 3.2f;

    public static BBCustomerNpc Spawn(BBCustomer c, List<Vector3> path, Transform parent)
    {
        var go = new GameObject("Customer " + c.name);
        go.transform.SetParent(parent, false);
        go.transform.position = path[0];
        BBNpcLook.Build(go.transform, c.skin, c.shirt, c.hat, false);
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 2.1f; col.radius = 0.4f; col.center = new Vector3(0, 1.05f, 0);
        var npc = go.AddComponent<BBCustomerNpc>();
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
        if (idx < path.Count)
        {
            Vector3 p = transform.position, t = path[idx];
            Vector3 flat = new Vector3(t.x - p.x, 0, t.z - p.z);
            float step = Speed * Time.deltaTime;
            if (flat.magnitude <= step) { p.x = t.x; p.z = t.z; idx++; }
            else
            {
                p += flat.normalized * step;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), Time.deltaTime * 8f);
            }
            p.y = BBWorld.GroundY(p);
            transform.position = p;
            // A tiny waddle.
            transform.GetChild(0).localPosition = new Vector3(0, 0.75f + Mathf.Abs(Mathf.Sin(Time.time * 9f)) * 0.06f, 0);
        }
        else if (leaving) Destroy(gameObject);
        else
        {
            waiting = true;
            Vector3 to = BBGame.I.player.transform.position - transform.position; to.y = 0;
            if (to.magnitude < 12f && to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 4f);
        }
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

    public static BBCop Spawn(int a, int b, Transform parent)
    {
        var go = new GameObject("Cop");
        go.transform.SetParent(parent, false);
        go.transform.position = BBWorld.I(a, b) + Vector3.up * 0.1f;
        BBNpcLook.Build(go.transform, BBPal.Skin[UnityEngine.Random.Range(0, BBPal.Skin.Length)], BBPal.Cop, BBPal.Cop, true);
        var cop = go.AddComponent<BBCop>();
        cop.ia = a; cop.ib = b;
        return cop;
    }

    void Start()
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.height = 2f; cc.radius = 0.4f; cc.center = new Vector3(0, 1f, 0); cc.stepOffset = 0.4f;
        siren = BB.Prim(PrimitiveType.Sphere, new Vector3(0, 2.45f, 0), Vector3.one * 0.25f, BB.Mat(BBPal.Bad, 3f), transform, false).transform;
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
        target = BBWorld.I(ia, ib);
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
        if (Physics.Linecast(eye, head, out hit))
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

    void MoveToward(Vector3 t, float speed)
    {
        Vector3 to = t - transform.position; to.y = 0;
        Vector3 v = to.magnitude > 0.05f ? to.normalized * speed : Vector3.zero;
        if (cc.isGrounded) vy = -1f; else vy -= 18f * Time.deltaTime;
        cc.Move((v + Vector3.up * vy) * Time.deltaTime);
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 8f);
        transform.GetChild(0).localPosition = new Vector3(0, 0.75f + Mathf.Abs(Mathf.Sin(Time.time * speed * 2.5f)) * 0.06f, 0);
    }

    void Update()
    {
        if (cc == null) return;
        var g = BBGame.I;
        Vector3 pp = g.player.transform.position;
        float dist = Vector3.Distance(transform.position, pp);

        if (!chasing)
        {
            if (g.Curfew && !g.PlayerInHideout && CanSee(pp, g.SpotRange)) { StartChase(); return; }
            MoveToward(target, 3.4f);
            Vector3 flat = target - transform.position; flat.y = 0;
            if (flat.magnitude < 0.6f) NextPatrol();
            stuck = (transform.position - lastPos).sqrMagnitude < 0.0004f ? stuck + Time.deltaTime : 0;
            if (stuck > 2.5f)
            {
                stuck = 0;
                // Wedged on a building after a chase: quietly hop back onto the road if the player isn't close.
                if (dist > 25f) { cc.enabled = false; transform.position = target + Vector3.up * 0.1f; cc.enabled = true; }
                else NextPatrol();
            }
        }
        else
        {
            if (CanSee(pp, 60f)) { lastSeen = pp; lostFor = 0; }
            else lostFor += Time.deltaTime;
            siren.GetComponent<Renderer>().sharedMaterial = BB.Mat(Mathf.Repeat(Time.time, 0.5f) < 0.25f ? BBPal.Bad : BBPal.Moon, 3f);
            if (lostFor > 6f || dist > 55f || g.PlayerInHideout) { StopChase(); g.OnCopLost(); }
            else
            {
                MoveToward(lastSeen, 6.9f);
                if (dist < 1.6f) g.Busted();
            }
        }
        lastPos = transform.position;
    }
}
