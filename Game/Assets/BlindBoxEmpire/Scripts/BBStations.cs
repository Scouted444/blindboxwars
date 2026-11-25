// Production equipment: Plush Press, Mixing Station, Packaging Station.
// Each station is its own object with a little production queue: load inputs, wait, collect outputs.
using UnityEngine;

public enum BBStationKind { Press, Mixer, Packager }

public class BBStation : MonoBehaviour
{
    public BBStationKind kind;
    public int inCount, outCount;
    public string outId;          // item id produced
    public float unitTime = 2f, progress;
    public Material statusMat;

    public bool Busy { get { return inCount > 0; } }
    public bool HasOutput { get { return outCount > 0; } }
    public float Fraction { get { return inCount + outCount == 0 ? 0 : (float)outCount / (inCount + outCount); } }

    public static readonly Vector3[] Size = { new Vector3(2.3f, 2.4f, 1.7f), new Vector3(2.6f, 1.3f, 1.2f), new Vector3(2.0f, 1.3f, 1.0f) };
    public static readonly string[] Names = { "Plush Press", "Mixing Station", "Packaging Station" };
    public static readonly int[] Prices = { 600, 350, 250 };

    public static GameObject Create(BBStationKind k, Vector3 pos, float yaw, Transform parent, bool ghost = false)
    {
        var m = new BBMesh();
        Build(m, k);
        var go = m.Emit(Names[(int)k], parent);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        if (ghost)
        {
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
        var bc = go.AddComponent<BoxCollider>();
        bc.size = Size[(int)k]; bc.center = new Vector3(0, Size[(int)k].y / 2, 0);
        var st = go.AddComponent<BBStation>();
        st.kind = k;
        st.statusMat = BB.NewMat(Color.white);
        BB.SetEmit(st.statusMat, BBPal.Good * 0.3f);
        Vector3 lightPos = k == BBStationKind.Press ? new Vector3(-0.3f, 0.95f, -0.78f) : new Vector3(0.9f, 1.15f, -Size[(int)k].z / 2 - 0.01f);
        BB.Prim(PrimitiveType.Cube, lightPos, new Vector3(0.4f, 0.14f, 0.04f), st.statusMat, go.transform, false, "Status");
        return go;
    }

    public static void Build(BBMesh m, BBStationKind k)
    {
        switch (k)
        {
            case BBStationKind.Press:
                m.Box(new Vector3(0, 0.15f, 0), new Vector3(2.3f, 0.3f, 1.7f), BBPal.SteelDark);
                m.Box(new Vector3(0, 0.75f, 0), new Vector3(2.1f, 0.9f, 1.5f), BBPal.Bubblegum);
                m.Box(new Vector3(0, 1.3f, 0.35f), new Vector3(1.7f, 0.2f, 0.7f), BBPal.Steel);
                m.Cyl(new Vector3(0, 1.4f, 0.35f), 0.18f, 0.9f, 8, BBPal.Steel);
                m.Box(new Vector3(0, 2.25f, 0.35f), new Vector3(1.9f, 0.35f, 0.9f), BBPal.Bubblegum);
                foreach (float x in new[] { -0.85f, 0.85f }) m.Box(new Vector3(x, 1.75f, 0.35f), new Vector3(0.14f, 1.0f, 0.14f), BBPal.SteelDark);
                m.Box(new Vector3(0.6f, 0.95f, -0.77f), new Vector3(0.6f, 0.4f, 0.06f), BBPal.Panel);
                m.Box(new Vector3(0.45f, 0.98f, -0.81f), new Vector3(0.1f, 0.1f, 0.03f), BB.Lamp(BBPal.Good));
                m.Box(new Vector3(0.75f, 0.98f, -0.81f), new Vector3(0.1f, 0.1f, 0.03f), BB.Lamp(BBPal.Bad));
                break;
            case BBStationKind.Mixer:
                m.Box(new Vector3(0, 0.95f, 0), new Vector3(2.6f, 0.1f, 1.2f), BBPal.Wood);
                foreach (float x in new[] { -1.2f, 1.2f }) foreach (float z in new[] { -0.5f, 0.5f }) m.Box(new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), BBPal.WoodDark);
                m.Box(new Vector3(0, 0.25f, 0), new Vector3(2.4f, 0.06f, 1.0f), BBPal.WoodDark);
                m.Cyl(new Vector3(-0.6f, 1.0f, 0), 0.38f, 0.22f, 10, BBPal.Mint, 0.45f);
                m.Cyl(new Vector3(0.2f, 1.0f, 0.15f), 0.08f, 0.35f, 6, BB.Lamp(BBPal.Butter));
                m.Cyl(new Vector3(0.45f, 1.0f, -0.15f), 0.07f, 0.42f, 6, BB.Lamp(BBPal.Accent));
                m.Cyl(new Vector3(0.7f, 1.0f, 0.2f), 0.09f, 0.3f, 6, BB.Lamp(BBPal.Lilac));
                m.Box(new Vector3(1.0f, 1.08f, -0.2f), new Vector3(0.3f, 0.16f, 0.3f), BBPal.Bubblegum);
                m.Box(new Vector3(0.9f, 1.15f, -0.55f), new Vector3(0.5f, 0.2f, 0.04f), BBPal.Panel);
                break;
            default:
                m.Box(new Vector3(0, 0.95f, 0), new Vector3(2.0f, 0.1f, 1.0f), BBPal.Steel);
                foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -0.4f, 0.4f }) m.Box(new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), BBPal.SteelDark);
                m.Box(new Vector3(-0.5f, 1.08f, 0.1f), new Vector3(0.7f, 0.16f, 0.6f), BBPal.Ink);
                for (int k2 = 0; k2 < 3; k2++) m.Box(new Vector3(0.35f + k2 * 0.18f, 1.1f, 0.2f), new Vector3(0.15f, 0.2f, 0.15f), BBPal.Bubblegum);
                m.Box(new Vector3(0.5f, 1.35f, 0.35f), new Vector3(0.9f, 0.04f, 0.04f), BBPal.SteelDark);
                m.Cyl(new Vector3(0.5f, 1.0f, 0.35f), 0.03f, 0.35f, 6, BBPal.SteelDark);
                m.Box(new Vector3(0.9f, 1.15f, -0.45f), new Vector3(0.45f, 0.2f, 0.04f), BBPal.Panel);
                break;
        }
    }

    void Update()
    {
        if (inCount > 0)
        {
            progress += Time.deltaTime;
            if (progress >= unitTime)
            {
                progress = 0; inCount--; outCount++;
                if (inCount == 0) BBGame.I.OnStationDone(this);
            }
        }
        Color c = inCount > 0 ? BBPal.Warn * (1.2f + Mathf.Sin(Time.time * 6f) * 0.6f) : outCount > 0 ? BBPal.Good * 2.5f : BBPal.Accent * 0.4f;
        BB.SetEmit(statusMat, c);
    }
}
