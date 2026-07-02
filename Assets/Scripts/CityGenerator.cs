using System.Collections.Generic;
using UnityEngine;

namespace FlatpackPanic
{
    public class CityGenerator : MonoBehaviour
    {
        [Header("City layout")]
        public int BlocksX = 6;
        public int BlocksZ = 5;
        public float BlockSize = 16f;
        public float RoadWidth = 9f;

        [Header("Prefab references (assign in Inspector)")]
        public GameObject[] BuildingPrefabs;
        public GameObject[] HousePrefabs;
        public GameObject[] RoadTilePrefabs;
        public GameObject RoadLanePrefab;
        public GameObject RoadLinePrefab;
        public GameObject SidewalkPrefab;
        public GameObject TreePrefab;
        public GameObject StreetLightPrefab;

        [Header("Runtime POIs (filled after Generate)")]
        public List<Vector3> ApartmentPositions = new();
        public Vector3 IkeaPosition;
        public Vector3 VanSpawnRoadPos;
        public Vector3 DeliveryTargetPosition;

        private const float GroundY = -0.2f;

        public void Generate()
        {
            ClearGenerated();

            var cellW = BlockSize + RoadWidth;
            var cellZ = BlockSize + RoadWidth;
            var totalW = BlocksX * cellW + RoadWidth;
            var totalZ = BlocksZ * cellZ + RoadWidth;
            var origin = new Vector3(-totalW * 0.5f, GroundY, -totalZ * 0.5f);

            BuildGround(totalW, totalZ, origin);

            // ---- Roads using prefab tiles ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                for (var col = 0; col <= BlocksX; col++)
                {
                    PlaceRoadTile(origin, col, row, cellW, cellZ);
                }
            }

            // ---- Sidewalks (primitive strips around roads) ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ;
                BuildSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.04f, z - 0.5f));
                BuildSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.04f, z + RoadWidth + 0.5f));
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW;
                BuildSidewalkStrip(totalZ, new Vector3(x - 0.5f, GroundY + 0.04f, origin.z + totalZ * 0.5f), zAxis: false);
                BuildSidewalkStrip(totalZ, new Vector3(x + RoadWidth + 0.5f, GroundY + 0.04f, origin.z + totalZ * 0.5f), zAxis: false);
            }

            // ---- Road center dashes ----
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ + RoadWidth * 0.5f;
                for (var i = 0; i < 20; i++)
                    PlaceLine(new Vector3(origin.x + i * (totalW / 20f) + 1f, GroundY + 0.07f, z));
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW + RoadWidth * 0.5f;
                for (var i = 0; i < 20; i++)
                    PlaceLine(new Vector3(x, GroundY + 0.07f, origin.z + i * (totalZ / 20f) + 1f));
            }

            // ---- City blocks ----
            ApartmentPositions.Clear();

            // IKEA in first block
            var ikeaCenter = new Vector3(origin.x + cellW * 0.5f + RoadWidth * 0.5f, 0f, origin.z + cellZ * 0.5f + RoadWidth * 0.5f);
            IkeaPosition = ikeaCenter;
            PlaceIkeaBuilding(ikeaCenter);

            // Van spawn: middle of vertical road at column 1
            VanSpawnRoadPos = new Vector3(origin.x + 1 * cellW + RoadWidth * 0.5f, GroundY + 0.1f, origin.z + RoadWidth * 0.5f);

            // Populate remaining blocks with prefabs
            for (var col = 0; col < BlocksX; col++)
            {
                for (var row = 0; row < BlocksZ; row++)
                {
                    if (col == 0 && row == 0) continue;

                    var cx = origin.x + col * cellW + RoadWidth + BlockSize * 0.5f;
                    var cz = origin.z + row * cellZ + RoadWidth + BlockSize * 0.5f;
                    var center = new Vector3(cx, GroundY + 0.05f, cz);
                    ApartmentPositions.Add(center);

                    // Place 2-4 prefabs per block
                    var count = Random.Range(2, 5);
                    for (var b = 0; b < count; b++)
                    {
                        var off = new Vector3(
                            Random.Range(-BlockSize * 0.32f, BlockSize * 0.32f), 0f,
                            Random.Range(-BlockSize * 0.32f, BlockSize * 0.32f));
                        var prefab = PickRandomBuildingPrefab();
                        if (prefab != null)
                            Instantiate(prefab, center + off, Quaternion.Euler(0, Random.Range(0, 4) * 90f, 0));
                    }

                    // Trees
                    for (var t = 0; t < Random.Range(1, 4); t++)
                    {
                        var tpos = new Vector3(
                            cx + Random.Range(-BlockSize * 0.35f, BlockSize * 0.35f), GroundY + 0.05f,
                            cz + Random.Range(-BlockSize * 0.35f, BlockSize * 0.35f));
                        if (TreePrefab != null)
                            Instantiate(TreePrefab, tpos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                    }
                }
            }

            // Street lights along road
            for (var row = 0; row <= BlocksZ; row += 2)
            {
                for (var col = 0; col < BlocksX; col++)
                {
                    var x1 = origin.x + col * cellW + RoadWidth + BlockSize * 0.5f;
                    var z = origin.z + row * cellZ + RoadWidth * 0.5f;
                    if (StreetLightPrefab != null)
                        Instantiate(StreetLightPrefab, new Vector3(x1, GroundY + 0.05f, z), Quaternion.Euler(0, 180, 0));
                }
            }

            PickRandomDeliveryTarget();
        }

        public void PickRandomDeliveryTarget()
        {
            if (ApartmentPositions.Count > 0)
                DeliveryTargetPosition = ApartmentPositions[Random.Range(0, ApartmentPositions.Count)];
            else
                DeliveryTargetPosition = IkeaPosition + new Vector3(15, 0, 15);
        }

        // ---- Prefab helpers ----

        private GameObject PickRandomBuildingPrefab()
        {
            var all = new List<GameObject>();
            if (HousePrefabs != null) all.AddRange(HousePrefabs);
            if (BuildingPrefabs != null) all.AddRange(BuildingPrefabs);
            if (all.Count == 0) return null;
            return all[Random.Range(0, all.Count)];
        }

        private void PlaceRoadTile(Vector3 origin, int col, int row, float cellW, float cellZ)
        {
            if (RoadTilePrefabs == null || RoadTilePrefabs.Length == 0) return;

            GameObject prefab = null;
            if ((col == 0 && row == 0) || (col == BlocksX && row == BlocksZ))
                prefab = FindRoadPrefab("Intersection");
            else if (col == 0 || col == BlocksX || row == 0 || row == BlocksZ)
                prefab = FindRoadPrefab("T_Intersection");
            else
                prefab = FindRoadPrefab("Lane");

            if (prefab == null) prefab = RoadTilePrefabs[0];

            var x = origin.x + col * cellW + RoadWidth * 0.5f;
            var z = origin.z + row * cellZ + RoadWidth * 0.5f;
            var rot = Quaternion.identity;
            if (col == 0 && row == BlocksZ) rot = Quaternion.Euler(0, 270, 0);
            else if (col == BlocksX && row == 0) rot = Quaternion.Euler(0, 90, 0);
            else if (col == BlocksX && row == BlocksZ) rot = Quaternion.Euler(0, 180, 0);

            var go = Instantiate(prefab, new Vector3(x, GroundY + 0.02f, z), rot);
            var s = RoadWidth / 9f;
            go.transform.localScale = new Vector3(s, 1, s);
        }

        private GameObject FindRoadPrefab(string namePart)
        {
            if (RoadTilePrefabs == null) return null;
            foreach (var p in RoadTilePrefabs)
                if (p != null && p.name.Contains(namePart)) return p;
            return null;
        }

        // ---- Primitive builders (used when prefabs aren't assigned) ----

        private void BuildGround(float w, float z, Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "City Ground";
            go.transform.position = origin + new Vector3(w * 0.5f, -0.1f, z * 0.5f);
            go.transform.localScale = new Vector3(w + 40, 0.2f, z + 40);
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.25f, 0.14f) };
            go.isStatic = true;
        }

        private void BuildSidewalkStrip(float length, Vector3 center, bool zAxis = true)
        {
            const float sw = 1.2f;
            if (SidewalkPrefab != null)
            {
                var go = Instantiate(SidewalkPrefab, center, zAxis ? Quaternion.identity : Quaternion.Euler(0, 90, 0));
                go.transform.localScale = new Vector3(length / 3f, 1, 1);
                return;
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = zAxis ? "Sidewalk H" : "Sidewalk V";
            cube.transform.position = center;
            cube.transform.localScale = zAxis ? new Vector3(length, 0.08f, sw) : new Vector3(sw, 0.08f, length);
            cube.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.65f, 0.65f, 0.62f) };
            cube.isStatic = true;
        }

        private void PlaceLine(Vector3 center)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Road dash";
            go.transform.position = center;
            go.transform.localScale = new Vector3(0.4f, 0.02f, 1.2f);
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.85f) };
            go.isStatic = true;
        }

        private void PlaceIkeaBuilding(Vector3 pos)
        {
            // Try to spawn a building prefab as IKEA
            if (BuildingPrefabs != null && BuildingPrefabs.Length > 0)
            {
                var prefab = FindRoadPrefab("Super Market");
                if (prefab == null) prefab = BuildingPrefabs[0];
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.transform.localScale = new Vector3(1.5f, 1f, 1.2f);
                return;
            }

            // Fallback: box IKEA
            var blue = new Material(Shader.Find("Standard")) { color = new Color(0.02f, 0.12f, 0.55f) };
            var yellow = new Material(Shader.Find("Standard")) { color = new Color(1f, 0.82f, 0.08f) };
            var main = GameObject.CreatePrimitive(PrimitiveType.Cube);
            main.name = "IKEA Warehouse";
            main.transform.position = pos + new Vector3(0, 3f, 0);
            main.transform.localScale = new Vector3(12, 6, 10);
            main.GetComponent<Renderer>().material = blue;
            main.isStatic = true;
            var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sign.name = "IKEA Sign";
            sign.transform.position = pos + new Vector3(0, 6.5f, 5.2f);
            sign.transform.localScale = new Vector3(8, 1, 0.3f);
            sign.GetComponent<Renderer>().material = yellow;
            sign.isStatic = true;
        }

        public void ClearGenerated()
        {
            var destroyList = new List<GameObject>();
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid()) continue;
                if (go.name.StartsWith("Building_") || go.name.StartsWith("Natures_") ||
                    go.name.StartsWith("Props_") || go.name.StartsWith("Vehicle_") ||
                    go.name.StartsWith("Road ") || go.name.StartsWith("Road_") ||
                    go.name.StartsWith("Sidewalk ") || go.name.StartsWith("City Ground") ||
                    go.name.StartsWith("Road dash") || go.name.StartsWith("IKEA ") ||
                    go.name.EndsWith("(Clone)"))
                    destroyList.Add(go);
            }
            foreach (var go in destroyList)
                Object.DestroyImmediate(go);
        }
    }
}
