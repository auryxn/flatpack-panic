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

        [Header("Runtime POIs (filled after Generate)")]
        public List<Vector3> ApartmentPositions = new();
        public Vector3 IkeaPosition;
        public Vector3 DeliveryTargetPosition; // current mission target

        private const float GroundY = -0.2f;

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

            // ---- Apartment blocks and IKEA ----
            ApartmentPositions.Clear();

            // IKEA: place in the first block (col=0, row=0)
            var ikeaCellCenter = new Vector3(origin.x + cellW * 0.5f + RoadWidth * 0.5f, 0f, origin.z + cellZ * 0.5f + RoadWidth * 0.5f);
            IkeaPosition = ikeaCellCenter;
            PlaceBuilding("IKEA Warehouse", ikeaCellCenter, new Vector3(12, 6, 10), Color.blue);

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
                        var color = Color.Lerp(Color.gray, new Color(0.6f, 0.55f, 0.5f), Random.value);
                        PlaceBuilding($"Apartment {col}_{row}_{b}", center + offset, new Vector3(w, h, d), color);
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
                roof.transform.localScale = new Vector3(size.x + 0.3f, 0.15f, size.z + 0.3f);
                var roofMat = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.2f, 0.22f) };
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
