// Builds the town of Plushville out of palette-tinted primitives at runtime.
using System.Collections.Generic;
using UnityEngine;

public class BBWorldRefs
{
    public GameObject press, pressLight, mixer, stash, bed, counter;
    public Material windowMat, lampMat, pressMat;
    public Light garageLight;
    public Vector3 spawn;
    public Bounds hideout;
}

public static class BBWorld
{
    public const int N = 5;        // intersections per side
    public const float S = 40f;    // block pitch
    public const float RoadHalf = 4f;
    public const float Curb = 0.15f;

    public static readonly string[] XStreets = { "Vinyl St", "Blindbox Blvd", "Fluff Ave", "Zipper Rd", "Plush Pkwy" };
    public static readonly string[] ZStreets = { "1st", "2nd", "3rd", "4th", "5th" };

    public static Vector3 I(int a, int b) { return new Vector3(a * S, 0, b * S); }

    public static float GroundY(Vector3 p)
    {
        float mx = Mathf.Repeat(p.x, S), mz = Mathf.Repeat(p.z, S);
        bool inTown = p.x > 0 && p.z > 0 && p.x < S * (N - 1) && p.z < S * (N - 1);
        return inTown && mx > RoadHalf && mx < S - RoadHalf && mz > RoadHalf && mz < S - RoadHalf ? Curb : 0f;
    }

    public static BBWorldRefs Build(Transform root)
    {
        var r = new BBWorldRefs();
        var rng = new System.Random(1337);
        float max = S * (N - 1);

        r.windowMat = BB.NewMat(BBPal.Window);
        BB.SetEmit(r.windowMat, Color.black);
        r.lampMat = BB.NewMat(BBPal.WindowLit);
        BB.SetEmit(r.lampMat, Color.black);

        // Ground: asphalt town on a grass field, hedged in.
        BB.Box(new Vector3(max / 2, -0.5f, max / 2), new Vector3(max + 400, 1, max + 400), BBPal.Grass, root, true, "Field");
        BB.Box(new Vector3(max / 2, -0.49f, max / 2), new Vector3(max + 2 * RoadHalf + 4, 1, max + 2 * RoadHalf + 4), BBPal.Asphalt, root, false, "Asphalt");
        float edge = max / 2 + RoadHalf + 4;
        for (int s = 0; s < 4; s++)
        {
            bool alongX = s < 2; float sign = s % 2 == 0 ? -1 : 1;
            Vector3 c = new Vector3(max / 2, 1f, max / 2) + (alongX ? new Vector3(0, 0, sign * edge) : new Vector3(sign * edge, 0, 0));
            Vector3 size = alongX ? new Vector3(max + 2 * RoadHalf + 10, 2, 2) : new Vector3(2, 2, max + 2 * RoadHalf + 10);
            BB.Box(c, size, BBPal.Leaf, root, true, "Hedge");
        }

        // Lane dashes
        var laneMat = BB.Mat(BBPal.Lane);
        for (int k = 0; k < N; k++)
            for (int b = 0; b < N - 1; b++)
                for (float t = 7; t < S - 6; t += 6)
                {
                    BB.Prim(PrimitiveType.Cube, new Vector3(b * S + t, 0.01f, k * S), new Vector3(2.2f, 0.02f, 0.25f), laneMat, root, false);
                    BB.Prim(PrimitiveType.Cube, new Vector3(k * S, 0.01f, b * S + t), new Vector3(0.25f, 0.02f, 2.2f), laneMat, root, false);
                }

        // Street lamps
        for (int a = 0; a < N; a++)
            for (int b = 0; b < N; b++)
            {
                float sx = a < N - 1 ? 1 : -1, sz = b < N - 1 ? 1 : -1;
                Vector3 p = I(a, b) + new Vector3(sx * (RoadHalf + 0.7f), Curb, sz * (RoadHalf + 4f));
                BB.Prim(PrimitiveType.Cylinder, p + new Vector3(0, 2.5f, 0), new Vector3(0.18f, 2.5f, 0.18f), BB.Mat(BBPal.Roof), root, true, "Lamp");
                BB.Prim(PrimitiveType.Sphere, p + new Vector3(0, 5.2f, 0), Vector3.one * 0.6f, r.lampMat, root, false);
            }

        // Blocks
        for (int i = 0; i < N - 1; i++)
            for (int j = 0; j < N - 1; j++)
            {
                Vector3 c = new Vector3(i * S + S / 2, 0, j * S + S / 2);
                float bs = S - 2 * RoadHalf;
                BB.Box(c + new Vector3(0, Curb / 2, 0), new Vector3(bs, Curb, bs), BBPal.Sidewalk, root, true, "Sidewalk");
                BB.Box(c + new Vector3(0, Curb + 0.01f, 0), new Vector3(bs - 4, 0.02f, bs - 4), BBPal.Grass, root, false, "Lawn");

                if (i == 0 && j == 0) BuildHideout(r, root, c);
                else if (i == 3 && j == 3) BuildShop(r, root, c);
                else if (i == 1 && j == 2) BuildPark(root, c, rng);
                else
                {
                    for (int q = 0; q < 4; q++)
                    {
                        Vector3 lot = c + new Vector3(q % 2 == 0 ? -7.5f : 7.5f, 0, q < 2 ? -7.5f : 7.5f);
                        if (rng.NextDouble() < 0.22) Tree(root, lot + new Vector3((float)rng.NextDouble() * 4 - 2, 0, (float)rng.NextDouble() * 4 - 2), rng);
                        else Building(r, root, lot, rng);
                    }
                }
            }
        return r;
    }

    static void Building(BBWorldRefs r, Transform root, Vector3 lot, System.Random rng)
    {
        float w = 9 + (float)rng.NextDouble() * 3, d = 9 + (float)rng.NextDouble() * 3;
        float h = 5 + (float)rng.NextDouble() * 12;
        Color wall = BBPal.Walls[rng.Next(BBPal.Walls.Length)];
        Vector3 b = lot + new Vector3(0, Curb, 0);
        var g = BB.Box(b + new Vector3(0, h / 2, 0), new Vector3(w, h, d), wall, root, true, "Building");
        BB.Box(b + new Vector3(0, h + 0.25f, 0), new Vector3(w + 0.6f, 0.5f, d + 0.6f), BBPal.Roof, g.transform.parent, false);
        int floors = Mathf.FloorToInt((h - 1.5f) / 3f);
        for (int f = 0; f < floors; f++)
        {
            float y = b.y + 2.2f + f * 3f;
            BB.Prim(PrimitiveType.Cube, new Vector3(b.x, y, b.z - d / 2 - 0.05f), new Vector3(w * 0.78f, 1.1f, 0.12f), r.windowMat, root, false);
            BB.Prim(PrimitiveType.Cube, new Vector3(b.x, y, b.z + d / 2 + 0.05f), new Vector3(w * 0.78f, 1.1f, 0.12f), r.windowMat, root, false);
            BB.Prim(PrimitiveType.Cube, new Vector3(b.x - w / 2 - 0.05f, y, b.z), new Vector3(0.12f, 1.1f, d * 0.78f), r.windowMat, root, false);
            BB.Prim(PrimitiveType.Cube, new Vector3(b.x + w / 2 + 0.05f, y, b.z), new Vector3(0.12f, 1.1f, d * 0.78f), r.windowMat, root, false);
        }
        BB.Box(new Vector3(b.x, b.y + 1.1f, b.z - d / 2 - 0.06f), new Vector3(1.4f, 2.2f, 0.12f), BBPal.Roof, root, false, "Door");
    }

    static void Tree(Transform root, Vector3 p, System.Random rng)
    {
        float s = 0.8f + (float)rng.NextDouble() * 0.6f;
        p.y = Curb;
        BB.Prim(PrimitiveType.Cylinder, p + new Vector3(0, 1.3f * s, 0), new Vector3(0.45f, 1.3f, 0.45f) * s, BB.Mat(BBPal.Trunk), root, true, "Tree");
        BB.Prim(PrimitiveType.Sphere, p + new Vector3(0, 3.6f * s, 0), new Vector3(3.4f, 3f, 3.4f) * s, BB.Mat(BBPal.Leaf), root, false);
        BB.Prim(PrimitiveType.Sphere, p + new Vector3(0.6f, 4.6f * s, 0.3f), new Vector3(2.2f, 2f, 2.2f) * s, BB.Mat(BBPal.Leaf), root, false);
    }

    static void BuildPark(Transform root, Vector3 c, System.Random rng)
    {
        BB.Prim(PrimitiveType.Cylinder, c + new Vector3(0, Curb + 0.4f, 0), new Vector3(7, 0.4f, 7), BB.Mat(BBPal.Concrete), root, true, "Fountain");
        BB.Prim(PrimitiveType.Cylinder, c + new Vector3(0, Curb + 0.82f, 0), new Vector3(6.2f, 0.04f, 6.2f), BB.Mat(BBPal.Water, 0.3f), root, false);
        BB.Prim(PrimitiveType.Cylinder, c + new Vector3(0, Curb + 1.8f, 0), new Vector3(0.8f, 1.2f, 0.8f), BB.Mat(BBPal.Concrete), root, true);
        for (int k = 0; k < 7; k++)
        {
            float a = k / 7f * Mathf.PI * 2;
            Tree(root, c + new Vector3(Mathf.Cos(a) * 11, 0, Mathf.Sin(a) * 11), rng);
        }
        for (int k = 0; k < 4; k++)
        {
            float a = (k / 4f + 0.125f) * Mathf.PI * 2;
            var bench = BB.Box(c + new Vector3(Mathf.Cos(a) * 6.5f, Curb + 0.45f, Mathf.Sin(a) * 6.5f), new Vector3(2.4f, 0.2f, 0.7f), BBPal.Wood, root, true, "Bench");
            bench.transform.rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90, 0);
        }
    }

    static void BuildHideout(BBWorldRefs r, Transform root, Vector3 c)
    {
        // Garage: open door faces -Z toward 1st street.
        Vector3 g = c + new Vector3(0, Curb, 2);
        float w = 14, d = 12, h = 4.2f;
        var wall = BBPal.Lilac;
        BB.Box(g + new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), BBPal.Concrete, root, true, "GarageFloor");
        BB.Box(g + new Vector3(0, h / 2, d / 2), new Vector3(w, h, 0.4f), wall, root, true, "Wall");
        BB.Box(g + new Vector3(-w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall, root, true, "Wall");
        BB.Box(g + new Vector3(w / 2, h / 2, 0), new Vector3(0.4f, h, d), wall, root, true, "Wall");
        BB.Box(g + new Vector3(-4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f), wall, root, true, "Wall");
        BB.Box(g + new Vector3(4.75f, h / 2, -d / 2), new Vector3(4.5f, h, 0.4f), wall, root, true, "Wall");
        BB.Box(g + new Vector3(0, h - 0.5f, -d / 2), new Vector3(5f, 1f, 0.4f), wall, root, true, "Lintel");
        BB.Box(g + new Vector3(0, h + 0.2f, 0), new Vector3(w + 0.8f, 0.4f, d + 0.8f), BBPal.Roof, root, true, "Roof");
        BB.Box(c + new Vector3(0, Curb + 0.03f, -10), new Vector3(5, 0.04f, 12), BBPal.Concrete, root, false, "Driveway");
        BB.Sign("PLUSH HQ", g + new Vector3(0, h + 1.1f, -d / 2 - 0.3f), 0, 0.12f, BBPal.Accent, root);
        r.hideout = new Bounds(g + new Vector3(0, 2, 0), new Vector3(w, 6, d));
        r.spawn = g + new Vector3(0, 0.2f, -3.5f);

        var lg = new GameObject("GarageLight");
        lg.transform.SetParent(root, false);
        lg.transform.position = g + new Vector3(0, h - 0.6f, 1);
        r.garageLight = lg.AddComponent<Light>();
        r.garageLight.type = LightType.Point; r.garageLight.range = 14; r.garageLight.intensity = 2.5f; r.garageLight.color = BBPal.WindowLit;
        BB.Prim(PrimitiveType.Cube, lg.transform.position + new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.1f, 0.3f), BB.Mat(BBPal.WindowLit, 2f), root, false);

        // Plush Press
        Vector3 pp = g + new Vector3(-4.5f, 0.1f, 4.2f);
        r.press = BB.Box(pp + new Vector3(0, 0.7f, 0), new Vector3(2.2f, 1.4f, 1.6f), BBPal.Bubblegum, root, true, "Plush Press");
        BB.Box(pp + new Vector3(0, 1.75f, 0.4f), new Vector3(1.6f, 0.7f, 0.5f), BBPal.Steel, r.press.transform.parent, false);
        r.pressMat = BB.NewMat(BBPal.Accent);
        BB.SetEmit(r.pressMat, BBPal.Accent * 0.2f);
        r.pressLight = BB.Prim(PrimitiveType.Cube, pp + new Vector3(0, 1.42f, -0.3f), new Vector3(1.4f, 0.06f, 0.7f), r.pressMat, root, false);
        BB.Sign("PRESS", pp + new Vector3(0, 0.75f, -0.82f), 0, 0.05f, BBPal.Panel, root);

        // Mixing table
        Vector3 mp = g + new Vector3(0, 0.1f, 4.7f);
        r.mixer = BB.Box(mp + new Vector3(0, 0.5f, 0), new Vector3(2.6f, 1f, 1.2f), BBPal.Wood, root, true, "Mixing Table");
        BB.Prim(PrimitiveType.Cylinder, mp + new Vector3(-0.5f, 1.15f, 0), new Vector3(0.7f, 0.15f, 0.7f), BB.Mat(BBPal.Mint), root, false);
        BB.Prim(PrimitiveType.Sphere, mp + new Vector3(0.6f, 1.15f, 0.1f), Vector3.one * 0.3f, BB.Mat(BBPal.Butter, 0.6f), root, false);
        BB.Prim(PrimitiveType.Sphere, mp + new Vector3(0.85f, 1.12f, -0.2f), Vector3.one * 0.22f, BB.Mat(BBPal.Accent, 0.6f), root, false);
        BB.Sign("MIX", mp + new Vector3(0, 0.55f, -0.62f), 0, 0.05f, BBPal.Panel, root);

        // Stash crate
        Vector3 sp = g + new Vector3(4.6f, 0.1f, 4.3f);
        r.stash = BB.Box(sp + new Vector3(0, 0.55f, 0), new Vector3(1.8f, 1.1f, 1.4f), BBPal.Trunk, root, true, "Stash");
        BB.Box(sp + new Vector3(0, 1.12f, 0), new Vector3(1.9f, 0.08f, 1.5f), BBPal.Wood, root, false);
        BB.Sign("STASH", sp + new Vector3(0, 0.6f, -0.72f), 0, 0.05f, BBPal.Ink, root);

        // Bed
        Vector3 bp = g + new Vector3(5f, 0.1f, -1.2f);
        r.bed = BB.Box(bp + new Vector3(0, 0.3f, 0), new Vector3(2.2f, 0.6f, 3.4f), BBPal.Mint, root, true, "Bed");
        BB.Box(bp + new Vector3(0, 0.7f, 1.2f), new Vector3(1.6f, 0.25f, 0.7f), BBPal.Ink, root, false);
        BB.Box(bp + new Vector3(0, 0.62f, -0.4f), new Vector3(2.25f, 0.1f, 2.4f), BBPal.Bubblegum, root, false);
    }

    static void BuildShop(BBWorldRefs r, Transform root, Vector3 c)
    {
        // Gas-n-Glam convenience store, counter faces -X toward Plush Pkwy... well, Zipper Rd.
        Vector3 b = c + new Vector3(3, Curb, 0);
        BB.Box(b + new Vector3(0, 2.75f, 0), new Vector3(12, 5.5f, 16), BBPal.Butter, root, true, "Gas-n-Glam");
        BB.Box(b + new Vector3(0, 5.75f, 0), new Vector3(12.6f, 0.5f, 16.6f), BBPal.Accent, root, false);
        BB.Box(b + new Vector3(-7.5f, 3.4f, 0), new Vector3(3.2f, 0.2f, 7), BBPal.Accent, root, false, "Awning");
        BB.Prim(PrimitiveType.Cube, b + new Vector3(-6.05f, 2.6f, -5), new Vector3(0.12f, 2f, 4f), r.windowMat, root, false);
        BB.Prim(PrimitiveType.Cube, b + new Vector3(-6.05f, 2.6f, 5), new Vector3(0.12f, 2f, 4f), r.windowMat, root, false);
        BB.Sign("GAS-N-GLAM", b + new Vector3(-6.2f, 4.6f, 0), 90, 0.16f, BBPal.Accent, root);
        r.counter = BB.Box(b + new Vector3(-8.2f, 0.6f, 0), new Vector3(1f, 1.2f, 4.5f), BBPal.Wood, root, true, "Shop Counter");

        // Clerk
        var clerk = new GameObject("Clerk"); clerk.transform.SetParent(root, false);
        clerk.transform.position = b + new Vector3(-6.9f, 0, 0);
        clerk.transform.rotation = Quaternion.Euler(0, -90, 0);
        BBNpcLook.Build(clerk.transform, BBPal.Skin[1], BBPal.Accent, BBPal.Butter, false);

        // Gas pumps for flavour
        for (int k = -1; k <= 1; k += 2)
        {
            BB.Box(c + new Vector3(-11, Curb + 0.9f, k * 6), new Vector3(0.8f, 1.8f, 0.6f), BBPal.Accent, root, true, "Pump");
            BB.Box(c + new Vector3(-11, Curb + 1.5f, k * 6 - 0.31f), new Vector3(0.5f, 0.35f, 0.02f), BBPal.Window, root, false);
        }
    }
}

// Shared look for every person in town: chunky capsule body, big head, dot eyes, optional cop hat.
public static class BBNpcLook
{
    public static void Build(Transform t, Color skin, Color shirt, Color hat, bool cop)
    {
        BB.Prim(PrimitiveType.Capsule, new Vector3(0, 0.75f, 0), new Vector3(0.75f, 0.6f, 0.6f), BB.Mat(cop ? BBPal.Cop : BBPal.Roof), t, false);
        BB.Prim(PrimitiveType.Capsule, new Vector3(0, 1.2f, 0), new Vector3(0.8f, 0.45f, 0.65f), BB.Mat(shirt), t, false);
        BB.Prim(PrimitiveType.Sphere, new Vector3(0, 1.85f, 0), Vector3.one * 0.62f, BB.Mat(skin), t, false);
        BB.Prim(PrimitiveType.Sphere, new Vector3(-0.12f, 1.9f, 0.27f), Vector3.one * 0.09f, BB.Mat(BBPal.Panel), t, false);
        BB.Prim(PrimitiveType.Sphere, new Vector3(0.12f, 1.9f, 0.27f), Vector3.one * 0.09f, BB.Mat(BBPal.Panel), t, false);
        if (cop)
        {
            BB.Prim(PrimitiveType.Cylinder, new Vector3(0, 2.17f, 0), new Vector3(0.6f, 0.1f, 0.6f), BB.Mat(BBPal.Cop), t, false);
            BB.Prim(PrimitiveType.Cube, new Vector3(0, 2.1f, 0.25f), new Vector3(0.5f, 0.04f, 0.25f), BB.Mat(BBPal.Panel), t, false);
            BB.Prim(PrimitiveType.Cube, new Vector3(0.18f, 1.35f, 0.31f), new Vector3(0.14f, 0.16f, 0.03f), BB.Mat(BBPal.Warn, 0.8f), t, false);
        }
        else
        {
            BB.Prim(PrimitiveType.Sphere, new Vector3(0, 2.07f, -0.04f), new Vector3(0.66f, 0.3f, 0.66f), BB.Mat(hat), t, false);
        }
    }
}
