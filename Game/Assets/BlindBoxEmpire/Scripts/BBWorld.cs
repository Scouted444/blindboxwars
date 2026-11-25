// Builds the town of Plushville out of merged low-poly meshes at runtime.
// Each block is one mesh (one draw call-ish); collisions are separate invisible box colliders.
using System.Collections.Generic;
using UnityEngine;

public class BBWorldRefs
{
    public GameObject press, pressLight, mixer, stash, bed, counter;
    public Material pressMat;
    public Light garageLight, storeLight;
    public Vector3 spawn;
    public Bounds hideout;
}

public static class BBWorld
{
    public const int N = 5;        // intersections per side
    public const float S = 40f;    // block pitch
    public const float RoadHalf = 4f;
    public const float Curb = 0.15f;
    public const float Lane = 1.3f;       // moving traffic lane offset from road centre
    public const float ParkLane = 3.1f;   // parked cars offset from road centre

    public static readonly string[] XStreets = { "Vinyl St", "Blindbox Blvd", "Fluff Ave", "Zipper Rd", "Plush Pkwy" };
    public static readonly string[] ZStreets = { "1st", "2nd", "3rd", "4th", "5th" };
    static readonly string[] ShopNames = { "NOODLE BROS", "THRIFT KING", "SUDS LAUNDRY", "PAWN STARZ", "DONUT HOLE", "VIDEO VAULT", "BARBER", "TACO TOWN", "PET PALACE", "PIZZA PLUS" };

    static Transform root, cols;
    static System.Random rng;
    static int shopIdx;

    public static Vector3 I(int a, int b) { return new Vector3(a * S, 0, b * S); }
    public static float Max { get { return S * (N - 1); } }

    public static bool InBlock(Vector3 p)
    {
        float mx = Mathf.Repeat(p.x, S), mz = Mathf.Repeat(p.z, S);
        bool inTown = p.x > 0 && p.z > 0 && p.x < Max && p.z < Max;
        return inTown && mx > RoadHalf && mx < S - RoadHalf && mz > RoadHalf && mz < S - RoadHalf;
    }

    public static float GroundY(Vector3 p) { return InBlock(p) ? Curb : 0f; }

    static float R(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    static T Pick<T>(T[] arr) { return arr[rng.Next(arr.Length)]; }

    static void Col(BBMesh m, Vector3 localCenter, Vector3 size)
    {
        BB.Solid(cols, m.M.MultiplyPoint3x4(localCenter), size, m.M.rotation.eulerAngles.y);
    }

    static Vector3 W(BBMesh m, Vector3 local) { return m.M.MultiplyPoint3x4(local); }
    static float Yaw(BBMesh m) { return m.M.rotation.eulerAngles.y; }

    // =====================================================================
    public static BBWorldRefs Build(Transform r0)
    {
        root = r0;
        cols = new GameObject("Colliders").transform; cols.SetParent(root, false);
        rng = new System.Random(1337);
        shopIdx = 0;
        var refs = new BBWorldRefs();
        float max = Max;

        // ---------- ground, roads, markings ----------
        var g = new BBMesh();
        g.Box(new Vector3(max / 2, -0.5f, max / 2), new Vector3(1400, 1, 1400), BBPal.Grass);
        g.Box(new Vector3(max / 2, -0.49f, max / 2), new Vector3(max + 2 * RoadHalf + 6, 1, max + 2 * RoadHalf + 6), BBPal.Asphalt);
        for (int k = 0; k < N; k++)
            for (int b = 0; b < N - 1; b++)
            {
                for (float t = 7; t < S - 6; t += 5)
                {
                    g.Box(new Vector3(b * S + t, 0.012f, k * S), new Vector3(2.2f, 0.02f, 0.18f), BBPal.Lane);
                    g.Box(new Vector3(k * S, 0.012f, b * S + t), new Vector3(0.18f, 0.02f, 2.2f), BBPal.Lane);
                }
                // asphalt patches for variety
                g.Box(new Vector3(b * S + R(10, 30), 0.006f, k * S + R(-2, 2)), new Vector3(R(2, 5), 0.01f, R(1, 3)), BBPal.AsphaltDark);
                g.Box(new Vector3(k * S + R(-2, 2), 0.006f, b * S + R(10, 30)), new Vector3(R(1, 3), 0.01f, R(2, 5)), BBPal.AsphaltDark);
            }
        // crosswalks + stop lines at every intersection approach
        for (int a = 0; a < N; a++)
            for (int b = 0; b < N; b++)
            {
                Vector3 c = I(a, b);
                for (int d = 0; d < 4; d++)
                {
                    int dx = d == 0 ? 1 : d == 1 ? -1 : 0, dz = d == 2 ? 1 : d == 3 ? -1 : 0;
                    int na = a + dx, nb = b + dz;
                    if (na < 0 || nb < 0 || na >= N || nb >= N) continue;
                    for (float s = -3.4f; s <= 3.41f; s += 0.85f)
                    {
                        Vector3 p = c + new Vector3(dx * 5.6f + (dx == 0 ? s : 0), 0.013f, dz * 5.6f + (dz == 0 ? s : 0));
                        g.Box(p, dx != 0 ? new Vector3(1.8f, 0.02f, 0.45f) : new Vector3(0.45f, 0.02f, 1.8f), BBPal.Paint);
                    }
                }
            }
        g.Emit("Roads", root, null, false);

        // ---------- blocks ----------
        for (int i = 0; i < N - 1; i++)
            for (int j = 0; j < N - 1; j++)
            {
                var m = new BBMesh();
                Vector3 c = new Vector3(i * S + S / 2, 0, j * S + S / 2);
                float bs = S - 2 * RoadHalf;
                m.Box(c + new Vector3(0, Curb / 2, 0), new Vector3(bs, Curb, bs), BBPal.Curb, BBPal.Sidewalk, 0);
                Solid(c + new Vector3(0, Curb / 2, 0), new Vector3(bs, Curb, bs));
                // sidewalk joints
                for (float t = -bs / 2 + 2; t < bs / 2; t += 2.5f)
                {
                    m.Box(c + new Vector3(t, Curb + 0.003f, -bs / 2 + 1.25f), new Vector3(0.05f, 0.01f, 2.5f), BBPal.Curb);
                    m.Box(c + new Vector3(t, Curb + 0.003f, bs / 2 - 1.25f), new Vector3(0.05f, 0.01f, 2.5f), BBPal.Curb);
                    m.Box(c + new Vector3(-bs / 2 + 1.25f, Curb + 0.003f, t), new Vector3(2.5f, 0.01f, 0.05f), BBPal.Curb);
                    m.Box(c + new Vector3(bs / 2 - 1.25f, Curb + 0.003f, t), new Vector3(2.5f, 0.01f, 0.05f), BBPal.Curb);
                }

                if (i == 0 && j == 0) { m.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(bs - 5, 0.02f, bs - 5), BBPal.GrassDark); BuildHQ(refs, m, c); }
                else if (i == 3 && j == 3) BuildGasStation(refs, m, c);
                else if (i == 1 && j == 2) { m.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(bs - 5, 0.02f, bs - 5), BBPal.Grass); Park(m, c); }
                else if (i == 2 && j == 2 || i == 3 && j == 0) Apartments(m, c, i == 3 ? 180 : 0);
                else if ((i == 1 || i == 2) && j == 1 || i == 0 && j == 3)
                {
                    m.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(bs - 5, 0.02f, bs - 5), BBPal.Concrete);
                    Shop(m, c + new Vector3(-7.5f, Curb, -7.5f), 0); Shop(m, c + new Vector3(7.5f, Curb, -7.5f), 0);
                    Shop(m, c + new Vector3(-7.5f, Curb, 7.5f), 180); Shop(m, c + new Vector3(7.5f, Curb, 7.5f), 180);
                }
                else
                {
                    m.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(bs - 5, 0.02f, bs - 5), BBPal.Grass);
                    for (int q = 0; q < 4; q++)
                    {
                        Vector3 lot = c + new Vector3(q % 2 == 0 ? -7.5f : 7.5f, Curb, q < 2 ? -7.5f : 7.5f);
                        House(m, lot, q < 2 ? 0 : 180);
                    }
                }
                m.SetFrame(Matrix4x4.identity);
                m.Emit("Block " + i + "," + j, root);
            }

        StreetFurniture();
        ParkedCars();
        Scenery();
        return refs;
    }

    static void Solid(Vector3 center, Vector3 size) { BB.Solid(cols, center, size); }

    // =====================================================================
    // Pieces
    static void Window(BBMesh m, Vector3 c, bool sideFacing, float ww, float wh, Color frame, float outSign)
    {
        // c is on the wall surface; outSign pushes outward along the face normal
        Vector3 nrm = sideFacing ? new Vector3(outSign, 0, 0) : new Vector3(0, 0, outSign);
        Vector3 fs = sideFacing ? new Vector3(0.08f, wh + 0.22f, ww + 0.22f) : new Vector3(ww + 0.22f, wh + 0.22f, 0.08f);
        Vector3 gs = sideFacing ? new Vector3(0.08f, wh, ww) : new Vector3(ww, wh, 0.08f);
        m.Box(c + nrm * 0.03f, fs, frame);
        bool lit = rng.NextDouble() < 0.6;
        m.Box(c + nrm * 0.07f, gs, lit ? BB.Glow(BBPal.Glass) : BBPal.Glass);
        // cross mullion
        Vector3 mv = sideFacing ? new Vector3(0.05f, wh, 0.06f) : new Vector3(0.06f, wh, 0.05f);
        Vector3 mh = sideFacing ? new Vector3(0.05f, 0.06f, ww) : new Vector3(ww, 0.06f, 0.05f);
        m.Box(c + nrm * 0.11f, mv, frame);
        m.Box(c + nrm * 0.11f, mh, frame);
        // sill
        Vector3 ss = sideFacing ? new Vector3(0.2f, 0.07f, ww + 0.35f) : new Vector3(ww + 0.35f, 0.07f, 0.2f);
        m.Box(c + nrm * 0.1f + Vector3.down * (wh / 2 + 0.12f), ss, frame);
    }

    static void Fence(BBMesh m, Vector3 a, Vector3 b, Color col, bool picket)
    {
        Vector3 d = b - a; float len = d.magnitude; bool alongX = Mathf.Abs(d.x) > Mathf.Abs(d.z);
        Vector3 dir = d / len;
        for (float t = 0; t <= len + 0.01f; t += 2.4f)
            m.Box(a + dir * t + Vector3.up * 0.55f, new Vector3(0.12f, 1.1f, 0.12f), col);
        Vector3 rs = alongX ? new Vector3(len, 0.07f, 0.05f) : new Vector3(0.05f, 0.07f, len);
        m.Box((a + b) / 2 + Vector3.up * 0.35f, rs, BBPal.Dim(col, 0.9f));
        m.Box((a + b) / 2 + Vector3.up * 0.8f, rs, BBPal.Dim(col, 0.9f));
        if (!picket)
        {
            Vector3 bs = alongX ? new Vector3(len, 0.9f, 0.04f) : new Vector3(0.04f, 0.9f, len);
            m.Box((a + b) / 2 + Vector3.up * 0.55f, bs, col);
            return;
        }
        for (float t = 0.15f; t < len; t += 0.32f)
        {
            Vector3 p = a + dir * t;
            m.Box(p + Vector3.up * 0.5f, alongX ? new Vector3(0.09f, 0.95f, 0.04f) : new Vector3(0.04f, 0.95f, 0.09f), col);
        }
    }

    static void Tree(BBMesh m, Vector3 p, float s)
    {
        if (rng.NextDouble() < 0.3)
        {
            m.Cyl(p, 0.18f * s, 1.2f * s, 6, BBPal.Trunk, 0.14f * s);
            m.Cyl(p + Vector3.up * 1.0f * s, 1.6f * s, 2.0f * s, 7, BBPal.Pine, 0);
            m.Cyl(p + Vector3.up * 2.1f * s, 1.25f * s, 1.8f * s, 7, BBPal.Dim(BBPal.Pine, 1.08f), 0);
            m.Cyl(p + Vector3.up * 3.1f * s, 0.9f * s, 1.6f * s, 7, BBPal.Dim(BBPal.Pine, 1.15f), 0);
        }
        else
        {
            m.Cyl(p, 0.2f * s, 2.0f * s, 6, BBPal.Trunk, 0.14f * s);
            Color leaf = rng.NextDouble() < 0.5 ? BBPal.Leaf : BBPal.LeafDark;
            m.Blob(p + Vector3.up * 2.9f * s, new Vector3(1.6f, 1.35f, 1.6f) * s, leaf, 7, 5);
            m.Blob(p + new Vector3(0.7f, 3.5f, 0.3f) * s, new Vector3(1.05f, 0.95f, 1.05f) * s, BBPal.Dim(leaf, 1.08f), 6, 4);
            m.Blob(p + new Vector3(-0.6f, 3.3f, -0.4f) * s, new Vector3(1.0f, 0.9f, 1.0f) * s, BBPal.Dim(leaf, 0.95f), 6, 4);
        }
        BB.Solid(cols, W(m, p + Vector3.up * 1.2f), new Vector3(0.45f, 2.4f, 0.45f));
    }

    static void Bush(BBMesh m, Vector3 p, float s, Color c)
    {
        m.Blob(p + Vector3.up * 0.35f * s, new Vector3(0.7f, 0.5f, 0.6f) * s, c, 6, 4);
    }

    public static void Car(BBMesh m, Color paint, bool van)
    {
        Color glass = BBPal.Dim(BBPal.Glass, 0.8f);
        m.Box(new Vector3(0, 0.62f, 0), new Vector3(1.84f, 0.62f, 4.3f), paint);
        if (van)
        {
            m.Box(new Vector3(0, 1.45f, -0.35f), new Vector3(1.8f, 1.1f, 3.5f), paint);
            m.Box(new Vector3(0, 1.62f, 1.42f), new Vector3(1.6f, 0.55f, 0.06f), glass);
            m.Box(new Vector3(0, 1.62f, 0.85f), new Vector3(1.84f, 0.5f, 0.9f), glass);
        }
        else
        {
            m.Box(new Vector3(0, 1.2f, -0.25f), new Vector3(1.62f, 0.58f, 2.2f), paint);
            m.Box(new Vector3(0, 1.2f, -0.25f), new Vector3(1.66f, 0.4f, 2.0f), glass);
            m.Box(new Vector3(0, 1.17f, 0.87f), new Vector3(1.5f, 0.42f, 0.06f), glass);
            m.Box(new Vector3(0, 1.17f, -1.37f), new Vector3(1.5f, 0.42f, 0.06f), glass);
        }
        m.Box(new Vector3(0, 0.42f, 2.17f), new Vector3(1.86f, 0.24f, 0.14f), BBPal.SteelDark);
        m.Box(new Vector3(0, 0.42f, -2.17f), new Vector3(1.86f, 0.24f, 0.14f), BBPal.SteelDark);
        m.Box(new Vector3(-0.62f, 0.7f, 2.16f), new Vector3(0.38f, 0.16f, 0.05f), BB.Lamp(BBPal.Hex("#FFF6D8")));
        m.Box(new Vector3(0.62f, 0.7f, 2.16f), new Vector3(0.38f, 0.16f, 0.05f), BB.Lamp(BBPal.Hex("#FFF6D8")));
        m.Box(new Vector3(-0.65f, 0.72f, -2.16f), new Vector3(0.32f, 0.14f, 0.05f), BB.Lamp(BBPal.Bad));
        m.Box(new Vector3(0.65f, 0.72f, -2.16f), new Vector3(0.32f, 0.14f, 0.05f), BB.Lamp(BBPal.Bad));
        m.Box(new Vector3(0, 0.62f, 2.16f), new Vector3(0.5f, 0.2f, 0.04f), BBPal.Steel);
        foreach (float x in new[] { -0.84f, 0.84f })
            foreach (float z in new[] { -1.38f, 1.38f })
                m.CylX(new Vector3(x, 0.36f, z), 0.36f, 0.3f, 8, BBPal.Tire, BBPal.Steel);
    }

    // ---------------- Houses ----------------
    static void House(BBMesh m, Vector3 lot, float yaw)
    {
        m.SetFrame(lot, yaw);
        float w = R(8f, 10.5f), d = R(7f, 8.5f), wallH = 3.0f, z0 = -0.3f;
        Color wall = Pick(BBPal.Walls), roof = Pick(BBPal.Roofs), trim = BBPal.Trim, door = Pick(BBPal.Clothes);
        bool garage = rng.NextDouble() < 0.45;
        float gs = rng.NextDouble() < 0.5 ? -1 : 1;
        float fz = z0 - d / 2;

        m.Box(new Vector3(0, 0.2f, z0), new Vector3(w + 0.2f, 0.4f, d + 0.2f), BBPal.Concrete);
        m.Box(new Vector3(0, 0.4f + wallH / 2, z0), new Vector3(w, wallH, d), wall);
        Col(m, new Vector3(0, 2.5f, z0), new Vector3(w + 0.2f, 5f, d + 0.2f));
        for (float y = 0.85f; y < wallH + 0.3f; y += 0.42f)
        {
            Color line = BBPal.Dim(wall, 0.9f);
            m.Box(new Vector3(0, y, fz - 0.015f), new Vector3(w, 0.035f, 0.03f), line);
            m.Box(new Vector3(0, y, z0 + d / 2 + 0.015f), new Vector3(w, 0.035f, 0.03f), line);
            m.Box(new Vector3(-w / 2 - 0.015f, y, z0), new Vector3(0.03f, 0.035f, d), line);
            m.Box(new Vector3(w / 2 + 0.015f, y, z0), new Vector3(0.03f, 0.035f, d), line);
        }
        foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                m.Box(new Vector3(sx * w / 2, 0.4f + wallH / 2, z0 + sz * d / 2), new Vector3(0.2f, wallH, 0.2f), trim);

        float rb = 0.4f + wallH;
        m.Box(new Vector3(0, rb + 0.06f, z0), new Vector3(w + 0.5f, 0.14f, d + 0.5f), trim);
        m.Gable(new Vector3(0, rb + 0.12f, z0), w + 0.9f, d + 1.3f, R(1.6f, 2.4f), roof, wall);
        m.Box(new Vector3(w * 0.28f * gs, rb + 1.5f, z0 + d * 0.2f), new Vector3(0.6f, 2.4f, 0.6f), BBPal.Brick);
        m.Box(new Vector3(w * 0.28f * gs, rb + 2.75f, z0 + d * 0.2f), new Vector3(0.75f, 0.12f, 0.75f), BBPal.SteelDark);

        // door + porch
        float dx = garage ? -gs * 1.2f : 0;
        m.Box(new Vector3(dx, 0.4f + 1.15f, fz - 0.03f), new Vector3(1.35f, 2.35f, 0.08f), trim);
        m.Box(new Vector3(dx, 0.4f + 1.08f, fz - 0.07f), new Vector3(1.0f, 2.12f, 0.06f), door);
        m.Box(new Vector3(dx + 0.34f, 0.4f + 1.0f, fz - 0.12f), new Vector3(0.08f, 0.08f, 0.06f), BBPal.Butter);
        m.Box(new Vector3(dx, 0.32f, fz - 1.05f), new Vector3(2.8f, 0.3f, 2.1f), BBPal.WoodDark);
        m.Box(new Vector3(dx, 0.1f, fz - 2.35f), new Vector3(1.6f, 0.2f, 0.55f), BBPal.Concrete);
        foreach (float px in new[] { -1.25f, 1.25f })
            m.Box(new Vector3(dx + px, 0.47f + 1.25f, fz - 1.95f), new Vector3(0.15f, 2.5f, 0.15f), trim);
        m.Box(new Vector3(dx, 0.47f + 2.55f, fz - 1.05f), new Vector3(3.1f, 0.14f, 2.4f), roof);
        m.Box(new Vector3(dx, 0.47f + 2.4f, fz - 0.6f), new Vector3(0.25f, 0.12f, 0.25f), BB.Lamp(BBPal.WindowLit)); // porch light
        // walkway
        m.Box(new Vector3(dx, 0.025f, (fz - 2.6f + -6.2f) / 2), new Vector3(1.2f, 0.03f, Mathf.Abs(-6.2f - (fz - 2.6f))), BBPal.Concrete);

        // windows
        float wy = 0.4f + 1.65f;
        Window(m, new Vector3(dx + (garage ? gs * 2.4f : -2.4f), wy, fz), false, 1.3f, 1.2f, trim, -1);
        if (!garage) Window(m, new Vector3(2.4f, wy, fz), false, 1.3f, 1.2f, trim, -1);
        Window(m, new Vector3(-w / 4, wy, z0 + d / 2), false, 1.2f, 1.1f, trim, 1);
        Window(m, new Vector3(w / 4, wy, z0 + d / 2), false, 1.2f, 1.1f, trim, 1);
        if (!(garage && gs < 0)) Window(m, new Vector3(-w / 2, wy, z0), true, 1.3f, 1.1f, trim, -1);
        if (!(garage && gs > 0)) Window(m, new Vector3(w / 2, wy, z0), true, 1.3f, 1.1f, trim, 1);
        // flower bed
        for (float x = -w / 2 + 0.6f; x < w / 2 - 0.5f; x += 0.7f)
        {
            if (Mathf.Abs(x - dx) < 1.6f) continue;
            Bush(m, new Vector3(x, 0, fz - 0.45f), 0.55f, rng.NextDouble() < 0.6 ? BBPal.LeafDark : Pick(new[] { BBPal.Bubblegum, BBPal.Butter, BBPal.Lilac }));
        }

        float driveX;
        if (garage)
        {
            float gw = 3.8f, gd = 6f, gx = gs * (w / 2 + gw / 2);
            float gz = fz + gd / 2;
            m.Box(new Vector3(gx, 1.5f, gz), new Vector3(gw, 3f, gd), wall);
            m.Box(new Vector3(gx, 3.08f, gz), new Vector3(gw + 0.35f, 0.18f, gd + 0.35f), roof);
            m.Box(new Vector3(gx, 1.15f, fz - 0.04f), new Vector3(3f, 2.3f, 0.08f), trim);
            for (float y = 0.35f; y < 2.3f; y += 0.45f)
                m.Box(new Vector3(gx, y, fz - 0.09f), new Vector3(2.9f, 0.05f, 0.03f), BBPal.Dim(trim, 0.85f));
            Col(m, new Vector3(gx, 1.5f, gz), new Vector3(gw, 3.2f, gd));
            driveX = gx;
        }
        else driveX = gs * (w / 2 + 2.2f);
        float dzStart = fz - 0.1f, dzEnd = -8.0f;
        m.Box(new Vector3(driveX, 0.022f, (dzStart + dzEnd) / 2), new Vector3(3.2f, 0.03f, dzStart - dzEnd), BBPal.Concrete);
        if (rng.NextDouble() < 0.45)
        {
            var save = m.M;
            m.SetFrame(save * Matrix4x4.TRS(new Vector3(driveX, 0, (dzStart + dzEnd) / 2 + 0.5f), Quaternion.Euler(0, 180, 0), Vector3.one));
            Car(m, Pick(BBPal.CarPaint), rng.NextDouble() < 0.25);
            Col(m, new Vector3(0, 0.9f, 0), new Vector3(1.9f, 1.8f, 4.4f));
            m.SetFrame(save);
        }

        // yard: fence, tree, mailbox
        bool picket = rng.NextDouble() < 0.6;
        Color fc = picket ? trim : BBPal.WoodDark;
        Fence(m, new Vector3(-7.7f, 0, z0 + 0.5f), new Vector3(-7.7f, 0, 7.7f), fc, picket);
        Fence(m, new Vector3(7.7f, 0, z0 + 0.5f), new Vector3(7.7f, 0, 7.7f), fc, picket);
        Fence(m, new Vector3(-7.7f, 0, 7.7f), new Vector3(7.7f, 0, 7.7f), fc, picket);
        Col(m, new Vector3(-7.7f, 0.6f, (z0 + 0.5f + 7.7f) / 2), new Vector3(0.15f, 1.2f, 7.7f - z0 - 0.5f));
        Col(m, new Vector3(7.7f, 0.6f, (z0 + 0.5f + 7.7f) / 2), new Vector3(0.15f, 1.2f, 7.7f - z0 - 0.5f));
        Col(m, new Vector3(0, 0.6f, 7.7f), new Vector3(15.4f, 1.2f, 0.15f));
        Tree(m, new Vector3(-driveX * 0.75f, 0, -5f + R(-0.5f, 0.5f)), R(0.85f, 1.15f));
        if (rng.NextDouble() < 0.6) Tree(m, new Vector3(R(-5, 5), 0, z0 + d / 2 + 3f), R(0.9f, 1.3f));
        m.Box(new Vector3(dx + 1.4f, 0.55f, -6.6f), new Vector3(0.1f, 1.1f, 0.1f), BBPal.WoodDark);
        m.Box(new Vector3(dx + 1.4f, 1.15f, -6.6f), new Vector3(0.3f, 0.28f, 0.5f), Pick(new[] { BBPal.SteelDark, BBPal.Bad, BBPal.Cop }));
        // trash cans by the garage
        m.Cyl(new Vector3(driveX + 1.9f * gs, 0, fz + 0.2f), 0.32f, 1.0f, 8, BBPal.Hex("#4E6E5A"));
    }

    // ---------------- Shops ----------------
    static void Shop(BBMesh m, Vector3 lot, float yaw)
    {
        m.SetFrame(lot, yaw);
        float w = 14.2f, d = 11f, h = R(4.8f, 6.2f), fz = -3.5f, z0 = fz + d / 2;
        Color wall = rng.NextDouble() < 0.4 ? BBPal.Brick : Pick(BBPal.Walls);
        Color accent = Pick(new[] { BBPal.Accent, BBPal.Cop, BBPal.Hex("#3F9E6B"), BBPal.Hex("#E0703F"), BBPal.Lilac });
        m.Box(new Vector3(0, h / 2, z0), new Vector3(w, h, d), wall);
        Col(m, new Vector3(0, h / 2, z0), new Vector3(w, h, d));
        m.Box(new Vector3(0, h + 0.35f, z0), new Vector3(w + 0.25f, 0.7f, d + 0.25f), BBPal.Dim(wall, 0.85f));
        m.Box(new Vector3(0, h + 0.05f, z0), new Vector3(w - 0.5f, 0.15f, d - 0.5f), BBPal.SteelDark);
        m.Box(new Vector3(-3f, h + 0.6f, z0 + 1f), new Vector3(1.6f, 1.0f, 1.2f), BBPal.Steel);
        m.Box(new Vector3(3.5f, h + 0.5f, z0 + 2f), new Vector3(1.2f, 0.8f, 1.2f), BBPal.Steel);
        // storefront
        m.Box(new Vector3(0, 0.25f, fz - 0.06f), new Vector3(w - 1f, 0.5f, 0.12f), BBPal.Dim(wall, 0.8f));
        m.Box(new Vector3(0, 1.75f, fz - 0.05f), new Vector3(w - 1.6f, 2.5f, 0.1f), BB.Glow(BBPal.Glass));
        for (float x = -w / 2 + 0.8f; x <= w / 2 - 0.7f; x += 2.2f)
            m.Box(new Vector3(x, 1.75f, fz - 0.1f), new Vector3(0.12f, 2.6f, 0.08f), BBPal.SteelDark);
        m.Box(new Vector3(0, 3.05f, fz - 0.1f), new Vector3(w - 1f, 0.12f, 0.1f), BBPal.SteelDark);
        m.Box(new Vector3(w / 2 - 2.4f, 1.3f, fz - 0.13f), new Vector3(1.5f, 2.5f, 0.06f), BBPal.Dim(BBPal.Glass, 0.7f));
        // striped awning
        int stripes = 10;
        float sw = (w - 1f) / stripes;
        for (int k = 0; k < stripes; k++)
            m.BoxRot(new Vector3(-w / 2 + 0.5f + sw * (k + 0.5f), 3.45f, fz - 0.85f), new Vector3(sw + 0.01f, 0.08f, 1.8f),
                k % 2 == 0 ? accent : BBPal.Trim, Quaternion.Euler(-18, 0, 0));
        // sign
        m.Box(new Vector3(0, 4.25f, fz - 0.12f), new Vector3(w * 0.62f, 0.95f, 0.2f), BBPal.Dim(accent, 0.6f));
        m.Box(new Vector3(0, 4.25f, fz - 0.2f), new Vector3(w * 0.66f, 1.05f, 0.05f), BB.Lamp(BBPal.Dim(accent, 0.9f)));
        string name = ShopNames[shopIdx++ % ShopNames.Length];
        BB.Sign(name, W(m, new Vector3(0, 4.25f, fz - 0.25f)), Yaw(m), 0.075f, BBPal.Ink, root);
        // back: dumpster, pallets
        m.Box(new Vector3(w / 2 - 2.2f, 0.7f, z0 + d / 2 + 1.1f), new Vector3(2.3f, 1.4f, 1.3f), BBPal.Hex("#3F6B55"));
        m.Box(new Vector3(w / 2 - 2.2f, 1.45f, z0 + d / 2 + 1.1f), new Vector3(2.4f, 0.1f, 1.4f), BBPal.Hex("#2F4F40"));
        Col(m, new Vector3(w / 2 - 2.2f, 0.75f, z0 + d / 2 + 1.1f), new Vector3(2.3f, 1.5f, 1.3f));
        m.Box(new Vector3(-w / 2 + 2f, 0.15f, z0 + d / 2 + 1f), new Vector3(1.2f, 0.3f, 1.2f), BBPal.Wood);
        // bench + planter out front
        m.Box(new Vector3(-w / 2 + 2f, 0.45f, fz - 1.6f), new Vector3(1.8f, 0.12f, 0.5f), BBPal.Wood);
        m.Box(new Vector3(-w / 2 + 1.3f, 0.2f, fz - 1.6f), new Vector3(0.1f, 0.4f, 0.4f), BBPal.SteelDark);
        m.Box(new Vector3(-w / 2 + 2.7f, 0.2f, fz - 1.6f), new Vector3(0.1f, 0.4f, 0.4f), BBPal.SteelDark);
        m.Box(new Vector3(1.5f, 0.3f, fz - 1.4f), new Vector3(0.9f, 0.6f, 0.9f), BBPal.Concrete);
        Bush(m, new Vector3(1.5f, 0.55f, fz - 1.4f), 0.7f, BBPal.Leaf);
    }

    // ---------------- Apartments ----------------
    static void Apartments(BBMesh m, Vector3 c, float yaw)
    {
        m.SetFrame(c + new Vector3(0, Curb, 0), yaw);
        float w = 22f, d = 12f, fh = 3f; int floors = 4;
        float h = floors * fh + 0.6f, z0 = 2.5f, fz = z0 - d / 2;
        Color wall = rng.NextDouble() < 0.5 ? BBPal.Brick : BBPal.SkyBlueWall;
        m.Box(new Vector3(0, 0.01f, 0), new Vector3(27, 0.02f, 27), BBPal.Concrete);
        m.Box(new Vector3(0, h / 2, z0), new Vector3(w, h, d), wall);
        Col(m, new Vector3(0, h / 2, z0), new Vector3(w, h, d));
        m.Box(new Vector3(0, h + 0.35f, z0), new Vector3(w + 0.3f, 0.7f, d + 0.3f), BBPal.Dim(wall, 0.85f));
        m.Cyl(new Vector3(5f, h + 0.7f, z0 + 1), 1.3f, 2.2f, 8, BBPal.WoodDark);
        m.Cyl(new Vector3(5f, h + 2.9f, z0 + 1), 1.4f, 0.8f, 8, BBPal.Dim(BBPal.WoodDark, 0.8f), 0.2f);
        m.Box(new Vector3(-5f, h + 0.6f, z0), new Vector3(2f, 1f, 1.5f), BBPal.Steel);
        for (int f = 0; f < floors; f++)
        {
            float y = 0.6f + f * fh;
            m.Box(new Vector3(0, y, z0), new Vector3(w + 0.12f, 0.16f, d + 0.12f), BBPal.Trim);
            for (float x = -w / 2 + 1.6f; x < w / 2 - 1f; x += 2.7f)
            {
                if (f == 0 && Mathf.Abs(x) < 2f) continue;
                Window(m, new Vector3(x, y + 1.6f, fz), false, 1.3f, 1.4f, BBPal.Trim, -1);
                Window(m, new Vector3(x, y + 1.6f, z0 + d / 2), false, 1.3f, 1.4f, BBPal.Trim, 1);
            }
            for (float z = z0 - d / 2 + 2f; z < z0 + d / 2 - 1f; z += 2.8f)
            {
                Window(m, new Vector3(-w / 2, y + 1.6f, z), true, 1.3f, 1.4f, BBPal.Trim, -1);
                Window(m, new Vector3(w / 2, y + 1.6f, z), true, 1.3f, 1.4f, BBPal.Trim, 1);
            }
            if (f > 0)
                for (float x = -6f; x <= 6f; x += 12f)
                {
                    m.Box(new Vector3(x, y + 0.05f, fz - 0.7f), new Vector3(3.2f, 0.15f, 1.4f), BBPal.Concrete);
                    m.Box(new Vector3(x, y + 0.6f, fz - 1.35f), new Vector3(3.2f, 0.9f, 0.06f), BBPal.SteelDark);
                }
        }
        // entrance
        m.Box(new Vector3(0, 1.4f, fz - 0.05f), new Vector3(2.2f, 2.6f, 0.1f), BB.Glow(BBPal.Glass));
        m.Box(new Vector3(0, 2.95f, fz - 0.9f), new Vector3(3.4f, 0.18f, 1.9f), BBPal.Accent);
        m.Box(new Vector3(0, 2.75f, fz - 0.4f), new Vector3(0.4f, 0.1f, 0.3f), BB.Lamp(BBPal.WindowLit));
        BB.Sign("PLUSH TOWERS", W(m, new Vector3(0, 3.6f, fz - 0.1f)), Yaw(m), 0.07f, BBPal.Ink, root);
        // parking stripes + cars
        for (int k = -4; k <= 4; k++)
        {
            m.Box(new Vector3(k * 3f, 0.025f, -9f), new Vector3(0.12f, 0.02f, 5f), BBPal.Paint);
            if (k < 4 && rng.NextDouble() < 0.5)
            {
                var save = m.M;
                m.SetFrame(save * Matrix4x4.TRS(new Vector3(k * 3f + 1.5f, 0, -9f), Quaternion.Euler(0, rng.NextDouble() < 0.5 ? 0 : 180, 0), Vector3.one));
                Car(m, Pick(BBPal.CarPaint), rng.NextDouble() < 0.2);
                Col(m, new Vector3(0, 0.9f, 0), new Vector3(1.9f, 1.8f, 4.4f));
                m.SetFrame(save);
            }
        }
        Tree(m, new Vector3(-12f, 0, 8f), 1.1f);
        Tree(m, new Vector3(12f, 0, 9f), 1.2f);
        Tree(m, new Vector3(-12f, 0, -2f), 0.9f);
    }

    // ---------------- Park ----------------
    static void Park(BBMesh m, Vector3 c)
    {
        m.SetFrame(c + new Vector3(0, Curb, 0), 0);
        m.Box(new Vector3(0, 0.03f, 0), new Vector3(3f, 0.03f, 27f), BBPal.Sidewalk);
        m.Box(new Vector3(0, 0.03f, 0), new Vector3(27f, 0.03f, 3f), BBPal.Sidewalk);
        m.Cyl(new Vector3(0, 0, 0), 4.2f, 0.7f, 12, BBPal.Concrete);
        m.Cyl(new Vector3(0, 0.62f, 0), 3.7f, 0.06f, 12, BBPal.Water);
        m.Cyl(new Vector3(0, 0.6f, 0), 0.5f, 1.6f, 8, BBPal.Concrete);
        m.Cyl(new Vector3(0, 2.1f, 0), 1.3f, 0.35f, 10, BBPal.Concrete, 1.6f);
        m.Blob(new Vector3(0, 2.6f, 0), new Vector3(0.6f, 0.4f, 0.6f), BBPal.Water, 6, 4);
        Col(m, new Vector3(0, 0.6f, 0), new Vector3(8f, 1.2f, 8f));
        for (int k = 0; k < 8; k++)
        {
            float a = (k + 0.5f) / 8f * Mathf.PI * 2;
            Tree(m, new Vector3(Mathf.Cos(a) * 10.5f, 0, Mathf.Sin(a) * 10.5f), R(0.9f, 1.3f));
        }
        foreach (var p in new[] { new Vector3(5.5f, 0, 2.2f), new Vector3(-5.5f, 0, -2.2f), new Vector3(2.2f, 0, -5.5f), new Vector3(-2.2f, 0, 5.5f) })
        {
            bool alongX = Mathf.Abs(p.z) > Mathf.Abs(p.x) ? false : true;
            Vector3 seat = alongX ? new Vector3(2f, 0.1f, 0.6f) : new Vector3(0.6f, 0.1f, 2f);
            Vector3 back = alongX ? new Vector3(2f, 0.5f, 0.08f) : new Vector3(0.08f, 0.5f, 2f);
            m.Box(p + Vector3.up * 0.45f, seat, BBPal.Wood);
            m.Box(p + Vector3.up * 0.8f + (alongX ? new Vector3(0, 0, Mathf.Sign(p.z) * 0.3f) : new Vector3(Mathf.Sign(p.x) * 0.3f, 0, 0)), back, BBPal.Wood);
            m.Box(p + Vector3.up * 0.2f, alongX ? new Vector3(1.7f, 0.4f, 0.1f) : new Vector3(0.1f, 0.4f, 1.7f), BBPal.SteelDark);
        }
        // playground slide
        m.Box(new Vector3(8f, 1f, -9f), new Vector3(1.6f, 0.15f, 1.6f), BBPal.Butter);
        foreach (float x in new[] { 7.3f, 8.7f }) foreach (float z in new[] { -9.7f, -8.3f }) m.Box(new Vector3(x, 0.5f, z), new Vector3(0.12f, 1f, 0.12f), BBPal.Accent);
        m.BoxRot(new Vector3(8f, 0.55f, -6.9f), new Vector3(0.8f, 0.08f, 2.8f), BBPal.Mint, Quaternion.Euler(-22, 0, 0));
        Col(m, new Vector3(8f, 0.6f, -8.5f), new Vector3(1.8f, 1.2f, 3.5f));
        // picnic table
        m.Box(new Vector3(-8f, 0.75f, 8f), new Vector3(2f, 0.1f, 0.9f), BBPal.Wood);
        m.Box(new Vector3(-8f, 0.45f, 7.2f), new Vector3(2f, 0.08f, 0.35f), BBPal.Wood);
        m.Box(new Vector3(-8f, 0.45f, 8.8f), new Vector3(2f, 0.08f, 0.35f), BBPal.Wood);
        Col(m, new Vector3(-8f, 0.4f, 8f), new Vector3(2f, 0.8f, 2f));
        BB.Sign("PLUSHVILLE PARK", c + new Vector3(0, 1.4f, -13.6f), 0, 0.05f, BBPal.Ink, root);
        m.Box(new Vector3(0, 0.7f, -13.4f), new Vector3(3.4f, 0.9f, 0.2f), BBPal.LeafDark);
    }

    // ---------------- HQ: your garage ----------------
    static void BuildHQ(BBWorldRefs r, BBMesh m, Vector3 c)
    {
        // Garage interior footprint matches the original game logic (spawn/hideout bounds).
        Vector3 g = c + new Vector3(0, Curb, 2);
        float w = 14, d = 12, h = 4.2f;
        m.SetFrame(g, 0);
        Color wall = BBPal.Cream, inside = BBPal.Hex("#D9D2E6"), trim = BBPal.Trim;
        m.Box(new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), BBPal.Concrete);
        m.Box(new Vector3(-3f, 0.105f, 2f), new Vector3(2.5f, 0.01f, 1.6f), BBPal.Hex("#8E8794")); // oil stain
        // walls (outer colour outside, inner colour inside via inset boxes)
        m.Box(new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.4f), wall);
        m.Box(new Vector3(-w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall);
        m.Box(new Vector3(w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall);
        m.Box(new Vector3(-4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f), wall);
        m.Box(new Vector3(4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f), wall);
        m.Box(new Vector3(0, h - 0.5f, -d / 2), new Vector3(5f, 1f, 0.4f), wall);
        m.Box(new Vector3(0, h / 2, d / 2 - 0.21f), new Vector3(w - 0.4f, h, 0.02f), inside);
        m.Box(new Vector3(-w / 2 + 0.21f, h / 2, 0), new Vector3(0.02f, h, d - 0.4f), inside);
        m.Box(new Vector3(w / 2 - 0.21f, h / 2, 0), new Vector3(0.02f, h, d - 0.4f), inside);
        foreach (var p in new[] { new Vector3(0, h / 2, d / 2), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0) }) { }
        Col(m, new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.4f));
        Col(m, new Vector3(-w / 2, h / 2, 0), new Vector3(0.4f, h, d));
        Col(m, new Vector3(w / 2, h / 2, 0), new Vector3(0.4f, h, d));
        Col(m, new Vector3(-4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f));
        Col(m, new Vector3(4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f));
        // siding lines outside
        for (float y = 0.5f; y < h; y += 0.42f)
        {
            m.Box(new Vector3(-4.75f, y, -d / 2 - 0.215f), new Vector3(4.5f, 0.035f, 0.03f), BBPal.Dim(wall, 0.9f));
            m.Box(new Vector3(4.75f, y, -d / 2 - 0.215f), new Vector3(4.5f, 0.035f, 0.03f), BBPal.Dim(wall, 0.9f));
            m.Box(new Vector3(-w / 2 - 0.215f, y, 0), new Vector3(0.03f, 0.035f, d), BBPal.Dim(wall, 0.9f));
            m.Box(new Vector3(w / 2 + 0.215f, y, 0), new Vector3(0.03f, 0.035f, d), BBPal.Dim(wall, 0.9f));
        }
        // roof + ceiling
        m.Box(new Vector3(0, h + 0.05f, 0), new Vector3(w + 0.6f, 0.2f, d + 0.6f), trim);
        m.Gable(new Vector3(0, h + 0.15f, 0), w + 1f, d + 1.4f, 2.4f, BBPal.Roofs[0], wall);
        Col(m, new Vector3(0, h + 0.1f, 0), new Vector3(w, 0.3f, d));
        // rolled-up garage door + frame
        m.Box(new Vector3(0, h - 0.15f, -d / 2 + 0.5f), new Vector3(5f, 0.5f, 0.6f), BBPal.Steel);
        m.Box(new Vector3(-2.6f, h / 2 - 0.4f, -d / 2 - 0.25f), new Vector3(0.2f, h - 0.8f, 0.1f), trim);
        m.Box(new Vector3(2.6f, h / 2 - 0.4f, -d / 2 - 0.25f), new Vector3(0.2f, h - 0.8f, 0.1f), trim);
        // windows on sides
        Window(m, new Vector3(-w / 2, 2.3f, -1f), true, 1.4f, 1.1f, trim, -1);
        Window(m, new Vector3(w / 2, 2.3f, 1f), true, 1.4f, 1.1f, trim, 1);
        // ceiling light fixture + posters + shelves
        m.Box(new Vector3(0, h - 0.25f, 1f), new Vector3(2.4f, 0.12f, 0.35f), BB.Lamp(BBPal.Hex("#FFF2D0")));
        m.Box(new Vector3(-w / 2 + 0.24f, 2.4f, -2.6f), new Vector3(0.02f, 1.2f, 0.85f), BBPal.Accent);
        m.Box(new Vector3(-w / 2 + 0.24f, 2.4f, -1.4f), new Vector3(0.02f, 1.0f, 0.75f), BBPal.Mint);
        m.Box(new Vector3(w / 2 - 0.24f, 2.6f, -2.2f), new Vector3(0.02f, 1.1f, 0.8f), BBPal.Butter);
        for (int k = 0; k < 3; k++)
        {
            m.Box(new Vector3(-w / 2 + 0.7f, 0.6f + k * 0.8f, 1.8f), new Vector3(0.9f, 0.06f, 2.4f), BBPal.WoodDark);
            for (int b = 0; b < 4; b++)
                m.Box(new Vector3(-w / 2 + 0.7f, 0.82f + k * 0.8f, 0.9f + b * 0.6f), new Vector3(0.5f, 0.38f, 0.42f), Pick(new[] { BBPal.Bubblegum, BBPal.Butter, BBPal.Wood, BBPal.Mint }));
        }
        foreach (float z in new[] { 0.6f, 3.0f }) m.Box(new Vector3(-w / 2 + 0.7f, 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f), BBPal.SteelDark);
        Col(m, new Vector3(-w / 2 + 0.7f, 1.2f, 1.8f), new Vector3(1f, 2.4f, 2.6f));
        BB.Sign("PLUSH HQ", W(m, new Vector3(0, h + 1.0f, -d / 2 - 0.5f)), 0, 0.1f, BBPal.Accent, root);
        // yard: old car, trash, overgrown bushes, fence
        var save = m.M;
        m.SetFrame(save * Matrix4x4.TRS(new Vector3(-7.5f, 0, -12f), Quaternion.Euler(0, 10, 0), Vector3.one));
        Car(m, BBPal.Hex("#8E6A5A"), true);
        Col(m, new Vector3(0, 1.0f, 0), new Vector3(1.9f, 2.0f, 4.4f));
        m.SetFrame(save);
        m.Cyl(new Vector3(4f, 0, -7f), 0.33f, 1.0f, 8, BBPal.Hex("#4E6E5A"));
        m.Cyl(new Vector3(4.8f, 0, -7f), 0.33f, 1.0f, 8, BBPal.Hex("#4E6E5A"));
        Bush(m, new Vector3(-8.5f, 0, 6.5f), 1.4f, BBPal.LeafDark);
        Bush(m, new Vector3(8.5f, 0, 7f), 1.2f, BBPal.Leaf);
        Tree(m, new Vector3(10f, 0, -9f), 1.2f);
        Tree(m, new Vector3(-11f, 0, 9f), 1.0f);
        m.Box(new Vector3(0, 0.025f, -11f), new Vector3(5f, 0.03f, 10f), BBPal.Concrete);

        r.hideout = new Bounds(g + new Vector3(0, 2, 0), new Vector3(w, 6, d));
        r.spawn = g + new Vector3(0, 0.2f, -3.5f);

        var lg = new GameObject("GarageLight");
        lg.transform.SetParent(root, false);
        lg.transform.position = g + new Vector3(0, h - 0.6f, 1);
        r.garageLight = lg.AddComponent<Light>();
        r.garageLight.type = LightType.Point; r.garageLight.range = 13; r.garageLight.intensity = 2.2f; r.garageLight.color = BBPal.WindowLit;
        r.garageLight.shadows = LightShadows.None;

        // ---- interactive stations (separate objects so they can be targeted) ----
        var pm = new BBMesh();
        pm.Box(new Vector3(0, 0.15f, 0), new Vector3(2.3f, 0.3f, 1.7f), BBPal.SteelDark);
        pm.Box(new Vector3(0, 0.75f, 0), new Vector3(2.1f, 0.9f, 1.5f), BBPal.Bubblegum);
        pm.Box(new Vector3(0, 1.3f, 0.35f), new Vector3(1.7f, 0.2f, 0.7f), BBPal.Steel);
        pm.Cyl(new Vector3(0, 1.4f, 0.35f), 0.18f, 0.9f, 8, BBPal.Steel);
        pm.Box(new Vector3(0, 2.25f, 0.35f), new Vector3(1.9f, 0.35f, 0.9f), BBPal.Bubblegum);
        foreach (float x in new[] { -0.85f, 0.85f }) pm.Box(new Vector3(x, 1.75f, 0.35f), new Vector3(0.14f, 1.0f, 0.14f), BBPal.SteelDark);
        pm.Box(new Vector3(0.6f, 0.95f, -0.77f), new Vector3(0.6f, 0.4f, 0.06f), BBPal.Panel);
        pm.Box(new Vector3(0.45f, 0.98f, -0.81f), new Vector3(0.1f, 0.1f, 0.03f), BB.Lamp(BBPal.Good));
        pm.Box(new Vector3(0.75f, 0.98f, -0.81f), new Vector3(0.1f, 0.1f, 0.03f), BB.Lamp(BBPal.Bad));
        r.press = pm.Emit("Plush Press", root);
        r.press.transform.position = g + new Vector3(-4.5f, 0.1f, 4.2f);
        var pbc = r.press.AddComponent<BoxCollider>(); pbc.center = new Vector3(0, 1.2f, 0); pbc.size = new Vector3(2.3f, 2.4f, 1.7f);
        r.pressMat = BB.NewMat(BBPal.Accent);
        BB.SetEmit(r.pressMat, BBPal.Accent * 0.2f);
        r.pressLight = BB.Prim(PrimitiveType.Cube, new Vector3(-0.3f, 0.95f, -0.78f), new Vector3(0.5f, 0.2f, 0.04f), r.pressMat, r.press.transform, false, "Status");
        BB.Sign("PRESS", r.press.transform.position + new Vector3(-0.3f, 0.6f, -0.82f), 0, 0.04f, BBPal.Panel, root);

        var mm = new BBMesh();
        mm.Box(new Vector3(0, 0.95f, 0), new Vector3(2.6f, 0.1f, 1.2f), BBPal.Wood);
        foreach (float x in new[] { -1.2f, 1.2f }) foreach (float z in new[] { -0.5f, 0.5f }) mm.Box(new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), BBPal.WoodDark);
        mm.Cyl(new Vector3(-0.6f, 1.0f, 0), 0.38f, 0.22f, 10, BBPal.Mint, 0.45f);
        mm.Cyl(new Vector3(0.2f, 1.0f, 0.15f), 0.08f, 0.35f, 6, BB.Lamp(BBPal.Butter));
        mm.Cyl(new Vector3(0.45f, 1.0f, -0.15f), 0.07f, 0.42f, 6, BB.Lamp(BBPal.Accent));
        mm.Cyl(new Vector3(0.7f, 1.0f, 0.2f), 0.09f, 0.3f, 6, BB.Lamp(BBPal.Lilac));
        mm.Box(new Vector3(1.0f, 1.08f, -0.2f), new Vector3(0.3f, 0.16f, 0.3f), BBPal.Bubblegum);
        r.mixer = mm.Emit("Mixing Table", root);
        r.mixer.transform.position = g + new Vector3(0, 0.1f, 4.7f);
        var mbc = r.mixer.AddComponent<BoxCollider>(); mbc.center = new Vector3(0, 0.6f, 0); mbc.size = new Vector3(2.6f, 1.2f, 1.2f);
        BB.Sign("MIX", r.mixer.transform.position + new Vector3(0, 0.75f, -0.62f), 0, 0.04f, BBPal.Panel, root);

        var sm = new BBMesh();
        sm.Box(new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.9f, 1.3f), BBPal.Wood);
        for (float y = 0.2f; y < 0.9f; y += 0.25f) sm.Box(new Vector3(0, y, -0.66f), new Vector3(1.82f, 0.04f, 0.02f), BBPal.WoodDark);
        sm.Box(new Vector3(0, 1.3f, 0.1f), new Vector3(0.9f, 0.8f, 0.8f), BBPal.SteelDark);
        sm.Box(new Vector3(0, 1.3f, -0.31f), new Vector3(0.7f, 0.6f, 0.02f), BBPal.Steel);
        sm.CylX(new Vector3(0.15f, 1.3f, -0.34f), 0.09f, 0.04f, 8, BBPal.Butter, BBPal.Butter);
        r.stash = sm.Emit("Stash", root);
        r.stash.transform.position = g + new Vector3(4.6f, 0.1f, 4.3f);
        var sbc = r.stash.AddComponent<BoxCollider>(); sbc.center = new Vector3(0, 0.85f, 0); sbc.size = new Vector3(1.8f, 1.7f, 1.3f);

        var bm = new BBMesh();
        bm.Box(new Vector3(0, 0.2f, 0), new Vector3(2.2f, 0.4f, 3.4f), BBPal.WoodDark);
        bm.Box(new Vector3(0, 0.5f, 0), new Vector3(2.0f, 0.25f, 3.2f), BBPal.Trim);
        bm.Box(new Vector3(0, 0.66f, 1.15f), new Vector3(1.4f, 0.18f, 0.6f), BBPal.Ink);
        bm.Box(new Vector3(0, 0.64f, -0.45f), new Vector3(2.1f, 0.12f, 2.2f), BBPal.Bubblegum);
        bm.Box(new Vector3(0, 0.9f, 1.68f), new Vector3(2.2f, 0.8f, 0.12f), BBPal.WoodDark);
        bm.Box(new Vector3(-1.9f, 0.01f, 0), new Vector3(1.2f, 0.02f, 1.8f), BBPal.Lilac);
        r.bed = bm.Emit("Bed", root);
        r.bed.transform.position = g + new Vector3(5f, 0.1f, -1.2f);
        var bbc = r.bed.AddComponent<BoxCollider>(); bbc.center = new Vector3(0, 0.4f, 0); bbc.size = new Vector3(2.2f, 0.8f, 3.4f);
    }

    // ---------------- Gas-n-Glam ----------------
    static void BuildGasStation(BBWorldRefs r, BBMesh m, Vector3 c)
    {
        m.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(27, 0.02f, 27), BBPal.Concrete);
        // Store: local frame faces -Z; yaw 90 makes the front face -X (toward Zipper Rd).
        m.SetFrame(c + new Vector3(4, Curb, 0), 90);
        float w = 14f, d = 12f, h = 4.6f;
        Color wall = BBPal.Butter;
        float fz = -d / 2;
        m.Box(new Vector3(0, 0.03f, 0), new Vector3(w, 0.06f, d), BBPal.Hex("#E9E3EE"));
        for (float x = -w / 2 + 1; x < w / 2; x += 2) for (float z = -d / 2 + 1; z < d / 2; z += 2)
                if (((int)(x + z)) % 4 == 0) m.Box(new Vector3(x, 0.065f, z), new Vector3(1f, 0.01f, 1f), BBPal.Hex("#D2CADB"));
        m.Box(new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.4f), wall);
        m.Box(new Vector3(-w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall);
        m.Box(new Vector3(w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall);
        Col(m, new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.4f));
        Col(m, new Vector3(-w / 2, h / 2, 0), new Vector3(0.4f, h, d));
        Col(m, new Vector3(w / 2, h / 2, 0), new Vector3(0.4f, h, d));
        // glass front with a door gap at x -4.5..-2
        m.Box(new Vector3(-5.75f, 0.3f, fz), new Vector3(2.5f, 0.6f, 0.3f), BBPal.Dim(wall, 0.8f));
        m.Box(new Vector3(2.25f, 0.3f, fz), new Vector3(9.5f, 0.6f, 0.3f), BBPal.Dim(wall, 0.8f));
        m.Box(new Vector3(-5.75f, 1.9f, fz), new Vector3(2.5f, 2.6f, 0.1f), BB.Glow(BBPal.Glass));
        m.Box(new Vector3(2.25f, 1.9f, fz), new Vector3(9.5f, 2.6f, 0.1f), BB.Glow(BBPal.Glass));
        Col(m, new Vector3(-5.75f, h / 2, fz), new Vector3(2.5f, h, 0.3f));
        Col(m, new Vector3(2.25f, h / 2, fz), new Vector3(9.5f, h, 0.3f));
        m.Box(new Vector3(0, 3.8f, fz), new Vector3(w, 1.6f, 0.4f), wall);
        for (float x = -6.9f; x <= 6.9f; x += 2.3f) m.Box(new Vector3(x, 1.9f, fz - 0.06f), new Vector3(0.12f, 2.6f, 0.06f), BBPal.SteelDark);
        m.Box(new Vector3(0, h + 0.1f, 0), new Vector3(w + 0.4f, 0.25f, d + 0.4f), BBPal.Accent);
        m.Box(new Vector3(0, h + 0.5f, fz - 0.1f), new Vector3(w * 0.7f, 0.8f, 0.25f), BB.Lamp(BBPal.Accent));
        BB.Sign("GAS-N-GLAM", W(m, new Vector3(0, h + 0.5f, fz - 0.3f)), Yaw(m), 0.11f, BBPal.Ink, root);
        Col(m, new Vector3(0, h + 0.1f, 0), new Vector3(w, 0.3f, d));
        m.Box(new Vector3(0, h - 0.05f, 0), new Vector3(w - 0.4f, 0.1f, d - 0.4f), BBPal.Hex("#F2EEF5"));
        for (float x = -4; x <= 4; x += 4) m.Box(new Vector3(x, h - 0.12f, 0), new Vector3(0.3f, 0.05f, 6f), BB.Lamp(BBPal.Hex("#F4F8FF")));
        // interior: shelves, coolers, counter
        for (float z = -1.5f; z <= 3.5f; z += 2.5f)
        {
            m.Box(new Vector3(2.5f, 0.75f, z), new Vector3(5f, 1.5f, 0.7f), BBPal.Steel);
            for (float x = 0.4f; x < 4.8f; x += 0.45f)
                for (int k = 0; k < 3; k++)
                    m.Box(new Vector3(x, 0.35f + k * 0.45f, z - 0.38f), new Vector3(0.35f, 0.3f, 0.12f), Pick(new[] { BBPal.Accent, BBPal.Butter, BBPal.Mint, BBPal.Lilac, BBPal.Bad, BBPal.Cop }));
            Col(m, new Vector3(2.5f, 0.75f, z), new Vector3(5f, 1.5f, 0.7f));
        }
        m.Box(new Vector3(-6.4f, 1.1f, 2.5f), new Vector3(0.8f, 2.2f, 5f), BBPal.Steel);
        m.Box(new Vector3(-5.98f, 1.15f, 2.5f), new Vector3(0.04f, 1.9f, 4.7f), BB.Lamp(BBPal.Hex("#BEE6FF")));
        Col(m, new Vector3(-6.4f, 1.1f, 2.5f), new Vector3(0.8f, 2.2f, 5f));

        var cm = new BBMesh();
        cm.Box(new Vector3(0, 0.55f, 0), new Vector3(3.2f, 1.1f, 0.9f), BBPal.Wood);
        cm.Box(new Vector3(0, 1.12f, 0), new Vector3(3.3f, 0.06f, 1.0f), BBPal.WoodDark);
        cm.Box(new Vector3(0.9f, 1.3f, 0.1f), new Vector3(0.5f, 0.3f, 0.4f), BBPal.SteelDark);
        cm.Box(new Vector3(0.9f, 1.38f, -0.1f), new Vector3(0.35f, 0.15f, 0.02f), BB.Lamp(BBPal.Good));
        cm.Box(new Vector3(-0.8f, 1.25f, 0), new Vector3(0.6f, 0.2f, 0.4f), BBPal.Accent);
        r.counter = cm.Emit("Shop Counter", root);
        r.counter.transform.position = W(m, new Vector3(-2.5f, 0, -2.8f));
        r.counter.transform.rotation = Quaternion.Euler(0, 90, 0);
        var cbc = r.counter.AddComponent<BoxCollider>(); cbc.center = new Vector3(0, 0.6f, 0); cbc.size = new Vector3(3.2f, 1.2f, 0.9f);
        var clerk = new GameObject("Clerk");
        clerk.transform.SetParent(root, false);
        clerk.transform.position = W(m, new Vector3(-2.5f, 0.05f, -1.7f));
        clerk.transform.rotation = Quaternion.Euler(0, -90, 0);
        var cs = BBHumanStyle.Random(new System.Random(42)); cs.clerk = true; cs.hairStyle = 5; cs.hat = BBPal.Accent;
        BBHuman.Create(clerk.transform, cs);

        var sl = new GameObject("StoreLight");
        sl.transform.SetParent(root, false);
        sl.transform.position = W(m, new Vector3(0, h - 0.8f, 0));
        r.storeLight = sl.AddComponent<Light>();
        r.storeLight.type = LightType.Point; r.storeLight.range = 12; r.storeLight.intensity = 1.6f; r.storeLight.color = BBPal.Hex("#F4F8FF");

        // fuel canopy + pumps toward the road
        m.Box(new Vector3(0, 4.4f, -12f), new Vector3(11f, 0.5f, 7f), BBPal.Trim);
        m.Box(new Vector3(0, 4.12f, -12f), new Vector3(10.6f, 0.06f, 6.6f), BB.Lamp(BBPal.Hex("#F4F8FF")));
        m.Box(new Vector3(0, 4.75f, -15.55f), new Vector3(11f, 0.7f, 0.12f), BB.Lamp(BBPal.Accent));
        foreach (float x in new[] { -3.5f, 3.5f })
        {
            m.Box(new Vector3(x, 2.1f, -12f), new Vector3(0.4f, 4.2f, 0.4f), BBPal.Trim);
            m.Box(new Vector3(x, 0.12f, -12f), new Vector3(1.4f, 0.24f, 3f), BBPal.Concrete);
            m.Box(new Vector3(x, 0.95f, -12f), new Vector3(0.8f, 1.5f, 0.6f), BBPal.Accent);
            m.Box(new Vector3(x, 1.25f, -12.31f), new Vector3(0.5f, 0.35f, 0.02f), BB.Lamp(BBPal.Hex("#BEE6FF")));
            Col(m, new Vector3(x, 0.9f, -12f), new Vector3(1f, 1.8f, 0.8f));
            Col(m, new Vector3(x, 2.1f, -12f), new Vector3(0.4f, 4.2f, 0.4f));
        }
        // price sign
        m.Box(new Vector3(-6.5f, 3f, -15.5f), new Vector3(0.3f, 6f, 0.3f), BBPal.SteelDark);
        m.Box(new Vector3(-6.5f, 6f, -15.5f), new Vector3(2.4f, 1.8f, 0.3f), BB.Lamp(BBPal.Accent));
        m.Box(new Vector3(-6.5f, 5.8f, -15.68f), new Vector3(2f, 0.7f, 0.05f), BBPal.Panel);
        Col(m, new Vector3(-6.5f, 3f, -15.5f), new Vector3(0.3f, 6f, 0.3f));
        Tree(m, new Vector3(9f, 0, -12f), 1.1f);
        Tree(m, new Vector3(-10f, 0, 9f), 1.0f);
        var save = m.M;
        m.SetFrame(save * Matrix4x4.TRS(new Vector3(3.5f, 0, -9.6f), Quaternion.Euler(0, 90, 0), Vector3.one));
        Car(m, BBPal.CarPaint[1], false);
        Col(m, new Vector3(0, 0.9f, 0), new Vector3(1.9f, 1.8f, 4.4f));
        m.SetFrame(save);
    }

    // =====================================================================
    static void StreetFurniture()
    {
        var m = new BBMesh();
        for (int a = 0; a < N; a++)
            for (int b = 0; b < N; b++)
            {
                Vector3 c = I(a, b);
                for (int k = 0; k < 2; k++)
                {
                    float sx = (k == 0 ? 1 : -1) * (a == 0 ? 1 : a == N - 1 ? -1 : 1);
                    float sz = (k == 0 ? 1 : -1) * (b == 0 ? 1 : b == N - 1 ? -1 : 1);
                    if (k == 1 && (a == 0 || a == N - 1 || b == 0 || b == N - 1)) continue;
                    Vector3 p = c + new Vector3(sx * (RoadHalf + 0.6f), Curb, sz * (RoadHalf + 4.5f));
                    m.Cyl(p, 0.11f, 6.2f, 6, BBPal.SteelDark, 0.08f);
                    m.Box(p + new Vector3(-sx * 1.0f, 6.1f, 0), new Vector3(2.0f, 0.1f, 0.12f), BBPal.SteelDark);
                    m.Box(p + new Vector3(-sx * 1.9f, 5.95f, 0), new Vector3(0.7f, 0.2f, 0.35f), BBPal.SteelDark);
                    m.Box(p + new Vector3(-sx * 1.9f, 5.83f, 0), new Vector3(0.6f, 0.05f, 0.28f), BB.Lamp(BBPal.WindowLit));
                    BB.Solid(cols, p + Vector3.up * 2f, new Vector3(0.3f, 4f, 0.3f));
                }
                // hydrant on one corner
                if (a < N - 1 && b < N - 1 && rng.NextDouble() < 0.6)
                {
                    Vector3 hp = c + new Vector3(RoadHalf + 0.7f, Curb, RoadHalf + 2.2f);
                    m.Cyl(hp, 0.16f, 0.7f, 8, BBPal.Bad);
                    m.Blob(hp + Vector3.up * 0.7f, new Vector3(0.17f, 0.12f, 0.17f), BBPal.Bad, 6, 3);
                    m.CylX(hp + Vector3.up * 0.45f, 0.07f, 0.45f, 6, BBPal.Bad, BBPal.Butter);
                }
            }
        // power poles + wires along every x-road on the +z side
        for (int b = 0; b < N; b++)
        {
            float z = b * S + (b == N - 1 ? -(RoadHalf + 1.6f) : RoadHalf + 1.6f);
            for (int k = 0; k < N - 1; k++)
            {
                Vector3 p = new Vector3(k * S + S / 2, Curb, z);
                m.Cyl(p, 0.16f, 8.5f, 6, BBPal.WoodDark, 0.13f);
                m.Box(p + new Vector3(0, 8.1f, 0), new Vector3(2.4f, 0.14f, 0.14f), BBPal.WoodDark);
                m.Cyl(p + new Vector3(0.5f, 7.5f, 0.2f), 0.25f, 0.6f, 6, BBPal.Steel);
                BB.Solid(cols, p + Vector3.up * 2f, new Vector3(0.35f, 4f, 0.35f));
                if (k < N - 2)
                    foreach (float off in new[] { -1f, 1f })
                        m.Box(new Vector3(p.x + S / 2, 8.25f + Curb - 0.2f, z + 0), new Vector3(S, 0.035f, 0.035f), BBPal.Tire);
            }
        }
        // bus stop on Fluff Ave
        Vector3 bs = I(2, 1) + new Vector3(RoadHalf + 1.4f, Curb, 18f);
        m.Box(bs + new Vector3(0.6f, 1.25f, 0), new Vector3(0.08f, 2.5f, 3.2f), BB.Glow(BBPal.Glass));
        m.Box(bs + new Vector3(0, 2.55f, 0), new Vector3(1.6f, 0.12f, 3.4f), BBPal.SteelDark);
        m.Box(bs + new Vector3(0.3f, 0.45f, 0), new Vector3(0.5f, 0.1f, 2.6f), BBPal.Wood);
        m.Box(bs + new Vector3(-0.6f, 1.5f, 1.8f), new Vector3(0.1f, 3f, 0.1f), BBPal.SteelDark);
        m.Box(bs + new Vector3(-0.6f, 2.9f, 1.8f), new Vector3(0.05f, 0.5f, 0.5f), BBPal.Cop);
        BB.Solid(cols, bs + new Vector3(0.6f, 1.25f, 0), new Vector3(0.2f, 2.5f, 3.2f));
        m.Emit("Street Furniture", root);
    }

    static void ParkedCars()
    {
        var m = new BBMesh();
        for (int i = 0; i < N - 1; i++)
            for (int j = 0; j < N; j++)
            {
                if (rng.NextDouble() < 0.45) continue;
                float side = rng.NextDouble() < 0.5 ? -1 : 1;
                if (j == 0) side = 1; if (j == N - 1) side = -1;
                Vector3 p = new Vector3(i * S + R(11, 29), 0, j * S + side * ParkLane);
                m.SetFrame(p, side > 0 ? 90 : -90);
                Car(m, Pick(BBPal.CarPaint), rng.NextDouble() < 0.2);
                Col(m, new Vector3(0, 0.9f, 0), new Vector3(1.9f, 1.8f, 4.4f));
                if (rng.NextDouble() < 0.45) continue;
                Vector3 q = new Vector3(j * S + side * ParkLane, 0, i * S + R(11, 29));
                m.SetFrame(q, side > 0 ? 0 : 180);
                Car(m, Pick(BBPal.CarPaint), rng.NextDouble() < 0.2);
                Col(m, new Vector3(0, 0.9f, 0), new Vector3(1.9f, 1.8f, 4.4f));
            }
        m.SetFrame(Matrix4x4.identity);
        m.Emit("Parked Cars", root);
    }

    static void Scenery()
    {
        float max = Max, mid = max / 2;
        // hedge ring + invisible walls
        var m = new BBMesh();
        float edge = mid + RoadHalf + 4f;
        for (int s = 0; s < 4; s++)
        {
            bool alongX = s < 2; float sign = s % 2 == 0 ? -1 : 1;
            Vector3 c = new Vector3(mid, 1f, mid) + (alongX ? new Vector3(0, 0, sign * edge) : new Vector3(sign * edge, 0, 0));
            Vector3 size = alongX ? new Vector3(2 * edge + 2, 2, 1.6f) : new Vector3(1.6f, 2, 2 * edge + 2);
            m.Box(c, size, BBPal.LeafDark);
            BB.Solid(cols, c + Vector3.up * 3f, size + Vector3.up * 8f);
            for (float t = -edge; t < edge; t += 9f)
            {
                Vector3 tp = c + (alongX ? new Vector3(t, -1f, sign * 6f) : new Vector3(sign * 6f, -1f, t));
                Tree(m, tp, R(1.1f, 1.6f));
            }
        }
        m.Emit("Edge", root);

        // mountains and far hills
        var mt = new BBMesh();
        for (int k = 0; k < 26; k++)
        {
            float a = k / 26f * Mathf.PI * 2 + R(-0.08f, 0.08f);
            float dist = R(320, 420);
            Vector3 p = new Vector3(mid + Mathf.Cos(a) * dist, -2, mid + Mathf.Sin(a) * dist);
            float rr = R(70, 130), hh = R(50, 120);
            mt.Cyl(p, rr, hh, 7, k % 2 == 0 ? BBPal.Mountain : BBPal.MountainFar, R(0, 12), false);
        }
        for (int k = 0; k < 30; k++)
        {
            float a = k / 30f * Mathf.PI * 2;
            float dist = R(150, 220);
            mt.Blob(new Vector3(mid + Mathf.Cos(a) * dist, -6, mid + Mathf.Sin(a) * dist), new Vector3(R(30, 50), R(14, 26), R(30, 50)), BBPal.GrassDark, 7, 4);
        }
        mt.Emit("Mountains", root, null, false);

        // clouds
        var cl = new BBMesh();
        for (int k = 0; k < 16; k++)
        {
            Vector3 p = new Vector3(mid + R(-350, 350), R(110, 160), mid + R(-350, 350));
            for (int b = 0; b < 4; b++)
                cl.Blob(p + new Vector3(R(-22, 22), R(-3, 5), R(-10, 10)), new Vector3(R(14, 24), R(6, 10), R(10, 16)), Color.white, 7, 4);
        }
        var cgo = cl.Emit("Clouds", root, null, false);
        cgo.AddComponent<BBDrift>();
    }
}

public class BBDrift : MonoBehaviour
{
    void Update() { transform.position += new Vector3(1.2f, 0, 0.4f) * Time.deltaTime; if (transform.position.x > 300) transform.position -= new Vector3(600, 0, 200); }
}
