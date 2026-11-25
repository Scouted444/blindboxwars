// Drivable van (player) and police cruisers (AI). Arcade handling: the body floats on a ground raycast so it
// rolls over curbs, while a raised box collider still bumps into walls, cars, fences and trees.
using UnityEngine;

public class BBVehicle : MonoBehaviour
{
    public bool driving;
    public float speed;
    protected Rigidbody rb;
    protected float yaw;
    protected float maxSpeed = 20f;

    public static BBVehicle SpawnVan(Vector3 pos, float yaw, Transform parent)
    {
        var m = new BBMesh();
        BBWorld.Car(m, BBPal.Lilac, true);
        m.Box(new Vector3(-0.93f, 1.45f, -0.6f), new Vector3(0.02f, 0.7f, 1.6f), BBPal.Accent);
        m.Box(new Vector3(0.93f, 1.45f, -0.6f), new Vector3(0.02f, 0.7f, 1.6f), BBPal.Accent);
        var go = m.Emit("Your Van", parent);
        var v = go.AddComponent<BBVehicle>();
        v.Setup(pos, yaw);
        var it = go.AddComponent<BBInteract>();
        it.prompt = () => v.driving ? null : "[E] Drive your van";
        it.use = () => BBGame.I.EnterVehicle(v);
        return v;
    }

    protected void Setup(Vector3 pos, float y)
    {
        transform.position = pos;
        yaw = y;
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        rb = gameObject.AddComponent<Rigidbody>();
        rb.mass = 1200; rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var bc = gameObject.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 1.2f, 0); bc.size = new Vector3(1.9f, 1.4f, 4.4f);
        var pm = new PhysicMaterial("CarBody") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicMaterialCombine.Minimum };
        bc.material = pm;
    }

    protected float GroundHeight()
    {
        Vector3 p = rb.position;
        var hits = Physics.RaycastAll(p + Vector3.up * 2.5f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
        float best = -100f;
        foreach (var h in hits)
        {
            if (h.collider.attachedRigidbody == rb) continue;
            if (h.point.y > p.y + 0.6f) continue;      // ignore roofs / car tops above us
            if (h.point.y > best) best = h.point.y;
        }
        return best < -50f ? 0f : best;
    }

    protected void Drive(float throttle, float steer, bool brake)
    {
        float dt = Time.fixedDeltaTime;
        float v = speed;
        if (throttle > 0.01f) v += (v < 0 ? 22f : 7.5f) * throttle * dt;
        else if (throttle < -0.01f) v += (v > 0 ? 22f : 5f) * throttle * dt;
        else v = Mathf.MoveTowards(v, 0, 3f * dt);
        if (brake) v = Mathf.MoveTowards(v, 0, 26f * dt);
        v = Mathf.Clamp(v, -6f, maxSpeed);
        float actual = Vector3.Dot(rb.velocity, transform.forward);
        if (Mathf.Abs(speed) > 2f && Mathf.Abs(actual) < Mathf.Abs(v) * 0.5f) v = Mathf.Lerp(v, actual, 0.6f); // hit something
        speed = v;
        yaw += steer * 62f * Mathf.Clamp(v / 5f, -1f, 1f) * dt;
        rb.MoveRotation(Quaternion.Euler(0, yaw, 0));
        Vector3 vel = Quaternion.Euler(0, yaw, 0) * Vector3.forward * v;
        vel.y = (GroundHeight() - rb.position.y) * 12f;
        rb.velocity = vel;
    }

    protected virtual void FixedUpdate()
    {
        float th = 0, st = 0; bool br = false;
        if (driving && !BBGame.I.UIOpen)
        {
            Vector2 mv = BBInp.Move(); th = mv.y; st = mv.x; br = BBInp.Brake;
        }
        Drive(th, st, br || !driving);
    }
}

public class BBPoliceCar : BBVehicle
{
    Material bar;
    float reverseUntil, stuckFor, closeFor;
    public bool leaving;

    public static BBPoliceCar Spawn(Vector3 pos, float yaw, Transform parent)
    {
        var m = new BBMesh();
        BBWorld.Car(m, BBPal.Cop, false);
        m.Box(new Vector3(-0.93f, 0.7f, 0.1f), new Vector3(0.03f, 0.5f, 2.2f), BBPal.Ink);
        m.Box(new Vector3(0.93f, 0.7f, 0.1f), new Vector3(0.03f, 0.5f, 2.2f), BBPal.Ink);
        m.Box(new Vector3(0, 1.52f, -0.2f), new Vector3(1.2f, 0.08f, 0.3f), BBPal.SteelDark);
        var go = m.Emit("Police Car", parent);
        var pc = go.AddComponent<BBPoliceCar>();
        pc.maxSpeed = 17f;
        pc.Setup(pos, yaw);
        pc.bar = BB.NewMat(Color.white);
        BB.Prim(PrimitiveType.Cube, new Vector3(-0.3f, 1.62f, -0.2f), new Vector3(0.55f, 0.14f, 0.28f), pc.bar, go.transform, false, "LightL");
        var barB = BB.NewMat(Color.white);
        BB.Prim(PrimitiveType.Cube, new Vector3(0.3f, 1.62f, -0.2f), new Vector3(0.55f, 0.14f, 0.28f), barB, go.transform, false, "LightR");
        pc.barB = barB;
        return pc;
    }
    Material barB;

    void Update()
    {
        bool f = Mathf.Repeat(Time.time, 0.4f) < 0.2f;
        BB.SetEmit(bar, f ? BBPal.Bad * 3f : Color.black);
        BB.SetEmit(barB, f ? Color.black : BBPal.Cop * 4f + Color.blue);
    }

    protected override void FixedUpdate()
    {
        var g = BBGame.I;
        Vector3 target = g.PlayerPos;
        if (leaving) target = transform.position + transform.forward * 50f;
        Vector3 to = target - rb.position; to.y = 0;
        float dist = to.magnitude;
        float ang = Vector3.SignedAngle(transform.forward, to, Vector3.up);
        float steer = Mathf.Clamp(ang / 35f, -1f, 1f), th = 1f;
        // avoid obstacles straight ahead
        RaycastHit hit;
        if (Physics.Raycast(rb.position + Vector3.up * 1.2f, transform.forward, out hit, 7f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.attachedRigidbody != rb)
        {
            if (hit.collider.GetComponentInParent<BBPlayer>() == null && hit.collider.GetComponentInParent<BBVehicle>() != g.Vehicle)
                steer = Mathf.Abs(steer) < 0.3f ? 1f : Mathf.Sign(steer);
        }
        if (!leaving && dist < 9f && !g.PlayerDriving) th = Mathf.Clamp01((dist - 4f) / 5f);
        if (Mathf.Abs(ang) > 100f && dist < 25f) { th = -1f; steer = -steer; }
        // stuck? back up for a moment
        stuckFor = Mathf.Abs(speed) < 1f && th > 0.5f ? stuckFor + Time.fixedDeltaTime : 0;
        if (stuckFor > 1.5f) { reverseUntil = Time.time + 1.4f; stuckFor = 0; }
        if (Time.time < reverseUntil) { th = -1f; steer = -steer; }
        Drive(th, steer, false);

        if (leaving) return;
        // pulling the player over
        closeFor = dist < 5.5f ? closeFor + Time.fixedDeltaTime : 0;
        if (g.PlayerDriving && closeFor > 2.5f && Mathf.Abs(g.Vehicle.speed) < 6f) { closeFor = 0; g.PulledOver(); }
        if (!g.PlayerDriving && dist < 4.5f && Mathf.Abs(speed) < 3f) g.CopReachedPlayer(null);
    }
}
