using System.Collections.Generic;
using UnityEngine;

namespace FlatpackPanic
{
    /// <summary>
    /// Generates a procedural city layout: grid of blocks with roads,
    /// then places hand-crafted POI markers (IKEA, apartment blocks, etc.)
    /// where prefabs can be spawned later.
    /// </summary>
    public class CityGenerator : MonoBehaviour
    {
        [Header("City layout")]
        public int BlocksX = 6;
        public int BlocksZ = 5;
        public float BlockSize = 16f;
        public float RoadWidth = 9f;
        public float CurbHeight = 0.15f;

        [Header("Materials")]
        public Material RoadMat;
        public Material SidewalkMat;
        public Material GrassMat;
        public Material BuildingMat;
        public Material WindowMat;

        [Header("Runtime POIs (filled after Generate)")]
        public List<Vector3> ApartmentPositions = new();
        public Vector3 IkeaPosition;
        public Vector3 VanSpawnRoadPos;
        public Vector3 DeliveryTargetPosition; // current mission target

        private const float GroundY = -0.2f;
        private static readonly Color[] BuildingColors = new[]
        {
            new Color(0.55f, 0.57f, 0.62f), // concrete
            new Color(0.85f, 0.72f, 0.60f), // beige
            new Color(0.60f, 0.40f, 0.30f), // brick red
            new Color(0.70f, 0.75f, 0.80f), // light gray
            new Color(0.40f, 0.55f, 0.60f), // teal-ish
            new Color(0.82f, 0.65f, 0.50f), // tan
            new Color(0.92f, 0.88f, 0.78f), // cream
        };
        private static readonly Color[] RoofColors = new[]
        {
            new Color(0.25f, 0.25f, 0.28f),
            new Color(0.35f, 0.20f, 0.15f),
            new Color(0.45f, 0.45f, 0.42f),
        };

        public void Generate()
        {
            ClearGenerated();

            var roadH = RoadWidth;
            var cellW = BlockSize + RoadWidth;
            var cellZ = BlockSize + RoadWidth;
            var totalW = BlocksX * cellW + RoadWidth;
            var totalZ = BlocksZ * cellZ + RoadWidth;
            var origin = new Vector3(-totalW * 0.5f, GroundY, -totalZ * 0.5f);

            // ---- Ground plate (big grass field) ----
            BuildGround(totalW, totalZ, origin);

            // ---- Roads: horizontal strips ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ;
                BuildRoadStrip(totalW, roadH, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.05f, z + roadH * 0.5f));
            }

            // ---- Roads: vertical strips ----
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW;
                BuildRoadStrip(totalZ, roadH, new Vector3(x + roadH * 0.5f, GroundY + 0.05f, origin.z + totalZ * 0.5f), zAxis: false);
            }

            // ---- Sidewalks along roads ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ;
                BuildSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.06f, z - 0.5f));
                BuildSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.06f, z + roadH + 0.5f));
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW;
                BuildSidewalkStrip(totalZ, new Vector3(x - 0.5f, GroundY + 0.06f, origin.z + totalZ * 0.5f), zAxis: false);
                BuildSidewalkStrip(totalZ, new Vector3(x + roadH + 0.5f, GroundY + 0.06f, origin.z + totalZ * 0.5f), zAxis: false);
            }

            // ---- Add road markings (center dashes) ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ + roadH * 0.5f;
                for (var i = 0; i < 20; i++)
                {
                    var dx = i * (totalW / 20f) + 1f;
                    BuildRoadDash(new Vector3(origin.x + dx, GroundY + 0.07f, z));
                }
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW + roadH * 0.5f;
                for (var i = 0; i < 20; i++)
                {
                    var dz = i * (totalZ / 20f) + 1f;
                    BuildRoadDash(new Vector3(x, GroundY + 0.07f, origin.z + dz));
                }
            }

            // ---- Apartment blocks and IKEA ----
            ApartmentPositions.Clear();

            // IKEA: place in the first block (col=0, row=0)
            var ikeaCellCenter = new Vector3(origin.x + cellW * 0.5f + RoadWidth * 0.5f, 0f, origin.z + cellZ * 0.5f + RoadWidth * 0.5f);
            IkeaPosition = ikeaCellCenter;
            // IKEA has a road-facing side — place it slightly back from road edge
            PlaceIkeaBuilding(ikeaCellCenter);

            // Remember a road position where the van can spawn (middle of road in column 1, row 0)
            VanSpawnRoadPos = new Vector3(origin.x + 1 * cellW + roadH * 0.5f, GroundY + 0.1f, origin.z + 0 * cellZ + roadH * 0.5f);

            // Scatter apartment buildings in remaining blocks
            for (var col = 0; col < BlocksX; col++)
            {
                for (var row = 0; row < BlocksZ; row++)
                {
                    // Skip IKEA block
                    if (col == 0 && row == 0) continue;

                    var cx = origin.x + col * cellW + RoadWidth + BlockSize * 0.5f;
                    var cz = origin.z + row * cellZ + RoadWidth + BlockSize * 0.5f;
                    var center = new Vector3(cx, 0f, cz);

                    ApartmentPositions.Add(center);

                    var buildingCount = Random.Range(2, 5);
                    for (var b = 0; b < buildingCount; b++)
                    {
                        var offset = new Vector3(
                            Random.Range(-BlockSize * 0.32f, BlockSize * 0.32f),
                            0f,
                            Random.Range(-BlockSize * 0.32f, BlockSize * 0.32f)
                        );
                        var w = Random.Range(4f, 7f);
                        var d = Random.Range(4f, 7f);
                        var h = Random.Range(5f, 16f);
                        var color = BuildingColors[Random.Range(0, BuildingColors.Length)];
                        PlaceBuilding($"Apartment {col}_{row}_{b}", center + offset, new Vector3(w, h, d), color);
                        // Windows on tall buildings
                        if (h > 8f) PlaceWindows($"Windows {col}_{row}_{b}", center + offset, new Vector3(w, h, d));
                    }

                    // Parking / trees filler
                    for (var t = 0; t < Random.Range(2, 5); t++)
                    {
                        var tpos = new Vector3(
                            cx + Random.Range(-BlockSize * 0.35f, BlockSize * 0.35f),
                            1.2f,
                            cz + Random.Range(-BlockSize * 0.35f, BlockSize * 0.35f)
                        );
                        PlaceTree(tpos, Random.Range(0.8f, 1.8f));
                    }
                }
            }

            // Pick a random delivery target
            PickRandomDeliveryTarget();
        }

        public void PickRandomDeliveryTarget()
        {
            if (ApartmentPositions.Count > 0)
                DeliveryTargetPosition = ApartmentPositions[Random.Range(0, ApartmentPositions.Count)];
            else
                DeliveryTargetPosition = IkeaPosition + new Vector3(15, 0, 15);
        }

        // ---- Builder helpers ----

        private void BuildRoadDash(Vector3 center)
        {
            var mat = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.85f) };
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Road dash";
            go.transform.position = center;
            go.transform.localScale = new Vector3(0.4f, 0.02f, 1.5f);
            go.GetComponent<Renderer>().material = mat;
            go.isStatic = true;
        }

        private void PlaceIkeaBuilding(Vector3 pos)
        {
            // Main warehouse — giant blue/yellow
            var blueMat = new Material(Shader.Find("Standard")) { color = new Color(0.02f, 0.12f, 0.55f) };
            var yellowMat = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.82f, 0.08f) };

            var main = GameObject.CreatePrimitive(PrimitiveType.Cube);
            main.name = "IKEA Warehouse";
            main.transform.position = pos + new Vector3(0, 4f, 0);
            main.transform.localScale = new Vector3(14, 8, 12);
            main.GetComponent<Renderer>().material = blueMat;
            main.isStatic = true;

            // Yellow sign stripe
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "IKEA Stripe";
            stripe.transform.position = pos + new Vector3(0, 8.2f, 6.1f);
            stripe.transform.localScale = new Vector3(12, 1f, 0.3f);
            stripe.GetComponent<Renderer>().material = yellowMat;
            stripe.isStatic = true;

            // Entrance
            var entrance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            entrance.name = "IKEA Entrance";
            entrance.transform.position = pos + new Vector3(0, 1.5f, 6.1f);
            entrance.transform.localScale = new Vector3(4, 3, 0.5f);
            entrance.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.7f) };
            entrance.isStatic = true;

            // A few IKEA windows
            for (var i = -1; i <= 1; i+=2)
            {
                var winMat = new Material(Shader.Find("Standard")) { color = new Color(0.5f, 0.7f, 0.9f, 0.6f) };
                var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                win.name = $"IKEA Window {i}";
                win.transform.position = pos + new Vector3(i * 5f, 4f, 6.1f);
                win.transform.localScale = new Vector3(2f, 2.5f, 0.1f);
                win.GetComponent<Renderer>().material = winMat;
                win.isStatic = true;
            }
        }

        private void PlaceWindows(string name, Vector3 pos, Vector3 bldgSize)
        {
            var count = Random.Range(2, 5);
            var halfX = bldgSize.x * 0.5f - 0.6f;
            var halfZ = bldgSize.z * 0.5f - 0.6f;
            for (var i = 0; i < count; i++)
            {
                var wx = pos.x + Random.Range(-halfX, halfX);
                var wz = pos.z + Random.Range(-halfZ, halfZ);
                float wy = pos.y + 0.5f + Random.Range(1.2f, bldgSize.y - 1f);
                var side = Random.Range(0, 4);
                var winMat = new Material(Shader.Find("Standard")) { color = new Color(0.7f, 0.85f, 0.95f) };
                var win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                win.name = name + $"_{i}";
                switch (side)
                {
                    case 0: win.transform.position = new Vector3(wx, wy, pos.z + halfZ + 0.05f); win.transform.localScale = new Vector3(0.8f, 1.0f, 0.05f); break;
                    case 1: win.transform.position = new Vector3(wx, wy, pos.z - halfZ - 0.05f); win.transform.localScale = new Vector3(0.8f, 1.0f, 0.05f); break;
                    case 2: win.transform.position = new Vector3(pos.x + halfX + 0.05f, wy, wz); win.transform.localScale = new Vector3(0.05f, 1.0f, 0.8f); break;
                    case 3: win.transform.position = new Vector3(pos.x - halfX - 0.05f, wy, wz); win.transform.localScale = new Vector3(0.05f, 1.0f, 0.8f); break;
                }
                win.GetComponent<Renderer>().material = winMat;
                win.isStatic = true;
            }
        }
        private void BuildGround(float w, float z, Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "City Ground";
            go.transform.position = origin + new Vector3(w * 0.5f, 0.5f, z * 0.5f);
            go.transform.localScale = new Vector3(w + 40, 1f, z + 40);
            go.GetComponent<Renderer>().material = GrassMat;
            go.isStatic = true;
        }

        private void BuildRoadStrip(float length, float width, Vector3 center, bool zAxis = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = zAxis ? "Road H" : "Road V";
            go.transform.position = center + Vector3.up * 0.05f;
            go.transform.localScale = zAxis
                ? new Vector3(length, 0.1f, width)
                : new Vector3(width, 0.1f, length);
            go.GetComponent<Renderer>().material = RoadMat;
            go.isStatic = true;
        }

        private void BuildSidewalkStrip(float length, Vector3 center, bool zAxis = true)
        {
            const float sw = 1.2f;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = zAxis ? "Sidewalk H" : "Sidewalk V";
            go.transform.position = center + Vector3.up * 0.04f;
            go.transform.localScale = zAxis
                ? new Vector3(length, 0.08f, sw)
                : new Vector3(sw, 0.08f, length);
            go.GetComponent<Renderer>().material = SidewalkMat;
            go.isStatic = true;
        }

        private void PlaceBuilding(string name, Vector3 position, Vector3 size, Color color)
        {
            var mat = new Material(Shader.Find("Standard")) { color = color, name = name + "_mat" };
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position + new Vector3(0, size.y * 0.5f, 0);
            go.transform.localScale = size;
            go.GetComponent<Renderer>().material = mat;
            go.isStatic = true;

            // Simple flat roof
            if (size.y > 3f)
            {
                var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.name = name + " roof";
                roof.transform.position = position + new Vector3(0, size.y + 0.1f, 0);
                roof.transform.localScale = new Vector3(size.x + 0.5f, 0.15f, size.z + 0.5f);
                var rc = RoofColors[Random.Range(0, RoofColors.Length)];
                var roofMat = new Material(Shader.Find("Standard")) { color = rc };
                roof.GetComponent<Renderer>().material = roofMat;
                roof.isStatic = true;
            }
        }

        private void PlaceTree(Vector3 position, float height)
        {
            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Tree trunk";
            trunk.transform.position = position + new Vector3(0, height * 0.3f, 0);
            trunk.transform.localScale = new Vector3(0.2f, height * 0.3f, 0.2f);
            trunk.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.2f, 0.08f) };
            trunk.isStatic = true;

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Tree crown";
            crown.transform.position = position + new Vector3(0, height * 0.7f, 0);
            crown.transform.localScale = new Vector3(height * 0.45f, height * 0.45f, height * 0.45f);
            crown.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.5f, 0.12f) };
            crown.isStatic = true;
        }

        public void ClearGenerated()
        {
            var toDestroy = new List<GameObject>();
            for (var i = 0; i < transform.childCount; i++)
                toDestroy.Add(transform.GetChild(i).gameObject);
            foreach (var go in toDestroy)
                DestroyImmediate(go);

            // Also clean up root city objects from older generations
            var oldCity = GameObject.Find("City Ground");
            if (oldCity != null) DestroyImmediate(oldCity);
            var oldRoads = GameObject.FindObjectsOfType<GameObject>();
            foreach (var go in oldRoads)
            {
                if (go.name.StartsWith("Road ") || go.name.StartsWith("Sidewalk ") ||
                    go.name.StartsWith("Apartment ") || go.name.StartsWith("IKEA ") ||
                    go.name.StartsWith("Tree ") || go.name.StartsWith("City Ground"))
                    DestroyImmediate(go);
            }
        }
    }
}
