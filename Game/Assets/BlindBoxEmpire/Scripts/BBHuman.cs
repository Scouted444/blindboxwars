// Chunky low-poly people with expressive faces, hair styles, walk cycles, blinking and head tracking.
using UnityEngine;

public class BBHumanStyle
{
    public Color skin, hair, shirt, pants, shoes, hat;
    public int hairStyle;      // 0 short, 1 long, 2 bun, 3 bald, 4 beanie, 5 cap, 6 mohawk
    public bool cop, clerk, jacket;
    public float height = 1f, width = 1f;

    public static BBHumanStyle Random(System.Random r)
    {
        var s = new BBHumanStyle();
        s.skin = BBPal.Skin[r.Next(BBPal.Skin.Length)];
        s.hair = BBPal.Hair[r.Next(BBPal.Hair.Length)];
        s.shirt = BBPal.Clothes[r.Next(BBPal.Clothes.Length)];
        s.pants = BBPal.Pants[r.Next(BBPal.Pants.Length)];
        s.shoes = r.NextDouble() < 0.5 ? BBPal.Hex("#2E2C35") : BBPal.Hex("#EDE6F2");
        s.hat = BBPal.Clothes[r.Next(BBPal.Clothes.Length)];
        s.hairStyle = r.Next(7);
        s.jacket = r.NextDouble() < 0.35;
        s.height = 0.92f + (float)r.NextDouble() * 0.16f;
        s.width = 0.9f + (float)r.NextDouble() * 0.25f;
        return s;
    }

    public static BBHumanStyle Cop(System.Random r)
    {
        var s = Random(r);
        s.cop = true; s.jacket = false;
        s.shirt = BBPal.Hex("#3F5BB0"); s.pants = BBPal.Hex("#1F2A55"); s.shoes = BBPal.Hex("#1A1A20"); s.hat = BBPal.Cop;
        s.hairStyle = 0;
        return s;
    }
}

public class BBHuman : MonoBehaviour
{
    public float speed;               // set by whoever moves us (m/s)
    public Transform lookTarget;      // head turns toward this when close
    Transform legL, legR, armL, armR, head, torso, eyes;
    float phase, sm, blinkAt, blinkUntil;
    Quaternion headRest = Quaternion.identity;

    public static BBHuman Create(Transform parent, BBHumanStyle s)
    {
        var root = new GameObject("Body").transform;
        root.SetParent(parent, false);
        root.localScale = new Vector3(s.width, s.height, s.width);
        var h = root.gameObject.AddComponent<BBHuman>();
        h.Build(s);
        return h;
    }

    Transform Pivot(string name, Transform parent, Vector3 pos)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    void Build(BBHumanStyle s)
    {
        Color dark = BBPal.Hex("#2B2024");
        // legs
        legL = Pivot("LegL", transform, new Vector3(-0.11f, 0.92f, 0));
        legR = Pivot("LegR", transform, new Vector3(0.11f, 0.92f, 0));
        foreach (var leg in new[] { legL, legR })
        {
            BB.CBox(leg, new Vector3(0, -0.41f, 0), new Vector3(0.19f, 0.82f, 0.21f), s.pants);
            BB.CBox(leg, new Vector3(0, -0.85f, 0.05f), new Vector3(0.21f, 0.12f, 0.31f), s.shoes);
        }
        BB.CBox(transform, new Vector3(0, 0.96f, 0), new Vector3(0.44f, 0.2f, 0.25f), s.pants);

        // torso
        torso = Pivot("Torso", transform, new Vector3(0, 1.0f, 0));
        BB.CBox(torso, new Vector3(0, 0.3f, 0), new Vector3(0.5f, 0.58f, 0.3f), s.shirt);
        if (s.jacket)
        {
            Color j = BBPal.Dim(s.hat, 0.75f);
            BB.CBox(torso, new Vector3(-0.17f, 0.3f, 0.005f), new Vector3(0.18f, 0.6f, 0.32f), j);
            BB.CBox(torso, new Vector3(0.17f, 0.3f, 0.005f), new Vector3(0.18f, 0.6f, 0.32f), j);
        }
        if (s.cop)
        {
            BB.CBox(torso, new Vector3(0, 0.05f, 0), new Vector3(0.53f, 0.08f, 0.33f), dark);                   // belt
            BB.CBox(torso, new Vector3(0.13f, 0.43f, 0.155f), new Vector3(0.08f, 0.09f, 0.02f), BBPal.Butter);   // badge
            BB.CBox(torso, new Vector3(-0.13f, 0.45f, 0.155f), new Vector3(0.06f, 0.1f, 0.02f), BBPal.Lamp(BBPal.Bad)); // radio light
        }
        // arms
        armL = Pivot("ArmL", torso, new Vector3(-0.31f, 0.53f, 0));
        armR = Pivot("ArmR", torso, new Vector3(0.31f, 0.53f, 0));
        foreach (var arm in new[] { armL, armR })
        {
            BB.CBox(arm, new Vector3(0, -0.25f, 0), new Vector3(0.14f, 0.5f, 0.16f), s.jacket ? BBPal.Dim(s.hat, 0.75f) : s.shirt);
            BB.CBox(arm, new Vector3(0, -0.57f, 0), new Vector3(0.13f, 0.15f, 0.13f), s.skin);
        }
        armL.localRotation = Quaternion.Euler(0, 0, -4); armR.localRotation = Quaternion.Euler(0, 0, 4);

        // head
        BB.CBox(torso, new Vector3(0, 0.62f, 0), new Vector3(0.16f, 0.09f, 0.16f), s.skin);
        head = Pivot("Head", torso, new Vector3(0, 0.65f, 0));
        BB.CBox(head, new Vector3(0, 0.21f, 0), new Vector3(0.4f, 0.42f, 0.38f), s.skin);
        BB.CBox(head, new Vector3(-0.21f, 0.2f, 0), new Vector3(0.04f, 0.1f, 0.08f), BBPal.Dim(s.skin, 0.92f));
        BB.CBox(head, new Vector3(0.21f, 0.2f, 0), new Vector3(0.04f, 0.1f, 0.08f), BBPal.Dim(s.skin, 0.92f));
        eyes = Pivot("Eyes", head, new Vector3(0, 0.24f, 0.19f));
        foreach (float x in new[] { -0.085f, 0.085f })
        {
            BB.CBox(eyes, new Vector3(x, 0, 0), new Vector3(0.1f, 0.08f, 0.02f), Color.white);
            BB.CBox(eyes, new Vector3(x + 0.005f, -0.005f, 0.008f), new Vector3(0.05f, 0.06f, 0.02f), dark);
            BB.CBox(head, new Vector3(x, 0.315f, 0.198f), new Vector3(0.11f, 0.025f, 0.02f), s.hairStyle == 3 ? BBPal.Dim(s.skin, 0.7f) : s.hair);
        }
        BB.CBox(head, new Vector3(0, 0.17f, 0.21f), new Vector3(0.06f, 0.08f, 0.06f), BBPal.Dim(s.skin, 0.93f));
        BB.CBox(head, new Vector3(0, 0.085f, 0.192f), new Vector3(0.13f, 0.025f, 0.02f), BBPal.Hex("#8A3A3A"));

        // hair / hats
        Color hc = s.hair;
        switch (s.cop ? 7 : s.hairStyle)
        {
            case 0:
                BB.CBox(head, new Vector3(0, 0.44f, 0), new Vector3(0.43f, 0.1f, 0.41f), hc);
                BB.CBox(head, new Vector3(0, 0.32f, -0.17f), new Vector3(0.43f, 0.26f, 0.08f), hc);
                break;
            case 1:
                BB.CBox(head, new Vector3(0, 0.44f, 0), new Vector3(0.43f, 0.1f, 0.41f), hc);
                BB.CBox(head, new Vector3(0, 0.2f, -0.17f), new Vector3(0.44f, 0.5f, 0.09f), hc);
                BB.CBox(head, new Vector3(-0.2f, 0.28f, 0.02f), new Vector3(0.05f, 0.34f, 0.3f), hc);
                BB.CBox(head, new Vector3(0.2f, 0.28f, 0.02f), new Vector3(0.05f, 0.34f, 0.3f), hc);
                break;
            case 2:
                BB.CBox(head, new Vector3(0, 0.44f, 0), new Vector3(0.43f, 0.1f, 0.41f), hc);
                BB.CBox(head, new Vector3(0, 0.55f, -0.08f), new Vector3(0.18f, 0.16f, 0.18f), hc);
                break;
            case 3: break;
            case 4:
                BB.CBox(head, new Vector3(0, 0.46f, 0), new Vector3(0.44f, 0.18f, 0.42f), s.hat);
                BB.CBox(head, new Vector3(0, 0.38f, 0), new Vector3(0.45f, 0.06f, 0.43f), BBPal.Dim(s.hat, 0.8f));
                break;
            case 5:
                BB.CBox(head, new Vector3(0, 0.45f, 0), new Vector3(0.43f, 0.12f, 0.41f), s.hat);
                BB.CBox(head, new Vector3(0, 0.41f, 0.25f), new Vector3(0.32f, 0.03f, 0.18f), s.hat);
                break;
            case 6:
                BB.CBox(head, new Vector3(0, 0.5f, -0.02f), new Vector3(0.1f, 0.18f, 0.38f), hc);
                break;
            case 7: // police cap
                BB.CBox(head, new Vector3(0, 0.47f, 0), new Vector3(0.45f, 0.13f, 0.43f), BBPal.Cop);
                BB.CBox(head, new Vector3(0, 0.42f, 0.25f), new Vector3(0.36f, 0.03f, 0.17f), dark);
                BB.CBox(head, new Vector3(0, 0.5f, 0.22f), new Vector3(0.07f, 0.07f, 0.02f), BBPal.Butter);
                break;
        }
        if (s.clerk) BB.CBox(torso, new Vector3(0, 0.25f, 0.155f), new Vector3(0.4f, 0.45f, 0.02f), BBPal.Accent); // apron

        headRest = head.localRotation;
        blinkAt = Time.time + UnityEngine.Random.Range(1f, 4f);
        phase = UnityEngine.Random.Range(0f, 6f);
    }

    void Update()
    {
        sm = Mathf.Lerp(sm, speed, Time.deltaTime * 8f);
        float amp = Mathf.Clamp01(sm / 3f);
        phase += Time.deltaTime * (2f + sm * 2.4f);
        float s = Mathf.Sin(phase);
        legL.localRotation = Quaternion.Euler(s * 34f * amp, 0, 0);
        legR.localRotation = Quaternion.Euler(-s * 34f * amp, 0, 0);
        float idle = Mathf.Sin(Time.time * 1.7f + phase) * 2f * (1 - amp);
        armL.localRotation = Quaternion.Euler(-s * 30f * amp + idle, 0, -4);
        armR.localRotation = Quaternion.Euler(s * 30f * amp - idle, 0, 4);
        torso.localPosition = new Vector3(0, 1.0f + Mathf.Abs(Mathf.Cos(phase)) * 0.035f * amp + Mathf.Sin(Time.time * 2f) * 0.006f, 0);
        torso.localRotation = Quaternion.Euler(amp * 4f, s * 4f * amp, 0);

        // blink
        if (Time.time > blinkAt) { blinkUntil = Time.time + 0.12f; blinkAt = Time.time + UnityEngine.Random.Range(2f, 5f); }
        eyes.localScale = new Vector3(1, Time.time < blinkUntil ? 0.15f : 1f, 1);

        // look at target
        Quaternion want = headRest;
        if (lookTarget != null)
        {
            Vector3 to = lookTarget.position - head.position;
            if (to.magnitude < 9f)
            {
                Vector3 local = transform.InverseTransformDirection(to);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 25f);
                want = Quaternion.Euler(pitch, yaw, 0);
            }
        }
        head.localRotation = Quaternion.Slerp(head.localRotation, want, Time.deltaTime * 5f);
    }
}
