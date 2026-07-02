using System.Collections.Generic;
using UnityEngine;

namespace FlatpackPanic
{
    /// <summary>
    /// Generates a realistic layered city using SimplePoly City prefabs.
    /// Layer order from road center outward:
    ///   1. Road surface
    ///   2. Curb
    ///   3. Street furniture zone (lights, signs, trees)
    ///   4. Pedestrian sidewalk
    ///   5. Building setback / front yard
    ///   6. Building facade
    /// </summary>
    public class CityGeneratorV2 : MonoBehaviour
    {
        [Header("City layout")]
        public int BlocksX = 6;
        public int BlocksZ = 5;
        public float BlockSize = 24f; // deep block interior
        public float RoadWidth = 7f; // 2 lanes
        public float CurbWidth = 0.2f;
        public float FurnitureZoneWidth = 1f;
        public float SidewalkWidth = 2f;
        public float BuildingSetbackResidential = 4f;
        public float BuildingSetbackCommercial = 1f;
        public float BuildingSetbackCenter = 0.5f;
        public float LampPostSpacing = 20f;
        public float TreeSpacing = 10f;

        [Header("Prefab references (auto-loaded or assign in Inspector)")]
        public GameObject[] BuildingPrefabs;
        public GameObject[] HousePrefabs;
        public GameObject[] RoadTilePrefabs;
        public GameObject TreePrefab;
        public GameObject SidewalkPrefab;
        public GameObject StreetLightPrefab;

        [Header("Runtime POIs")]
        public List<Vector3> BuildingPositions = new();
        public Vector3 IkeaPosition;
        public Vector3 VanSpawnRoadPos;
        public Vector3 DeliveryTargetPosition;

        private const float GroundY = -0.2f;

        public void Generate()
        {
            ClearGenerated();
            LoadPrefabs();

            var cellW = BlockSize + RoadWidth;
            var cellZ = BlockSize + RoadWidth;
            var totalW = BlocksX * cellW + RoadWidth;
            var totalZ = BlocksZ * cellZ + RoadWidth;
            var origin = new Vector3(-totalW * 0.5f, GroundY, -totalZ * 0.5f);

            // Ground
            BuildGround(totalW, totalZ, origin);

            // Generate road network
            for (var row = 0; row <= BlocksZ; row++)
            {
                for (var col = 0; col <= BlocksX; col++)
                    PlaceRoadTile(origin, col, row, cellW, cellZ, totalW, totalZ);
            }

            // Sidewalks along every road
            BuildSidewalks(origin, cellW, cellZ, totalW, totalZ);

            // Street furniture: lamps + trees
            BuildStreetFurniture(origin, cellW, cellZ, totalW, totalZ);

            // City blocks
            BuildingPositions.Clear();

            // IKEA block (0,0)
            var ikeaCenter = new Vector3(origin.x + cellW * 0.5f + RoadWidth * 0.5f, 0f, origin.z + cellZ * 0.5f + RoadWidth * 0.5f);
            IkeaPosition = ikeaCenter;
            PlaceIkea(ikeaCenter);

            // Van spawn: road center at column 1 row 0
            VanSpawnRoadPos = new Vector3(origin.x + 1 * cellW + RoadWidth * 0.5f, GroundY + 0.1f, origin.z + RoadWidth * 0.5f);

            // Populate remaining blocks
            for (var col = 0; col < BlocksX; col++)
            {
                for (var row = 0; row < BlocksZ; row++)
                {
                    if (col == 0 && row == 0) continue;

                    var cx = origin.x + col * cellW + RoadWidth + BlockSize * 0.5f;
                    var cz = origin.z + row * cellZ + RoadWidth + BlockSize * 0.5f;
                    var center = new Vector3(cx, GroundY + 0.05f, cz);

                    // Place 2-3 buildings around the block perimeter, facades facing the road
                    PlaceBlockBuildings(center, col, row, cellW, cellZ, origin);

                    // Interior trees/greenery
                    for (var t = 0; t < Random.Range(1, 3); t++)
                    {
                        var tpos = new Vector3(
                            cx + Random.Range(-BlockSize * 0.3f, BlockSize * 0.3f),
                            GroundY + 0.05f,
                            cz + Random.Range(-BlockSize * 0.3f, BlockSize * 0.3f));
                        if (TreePrefab != null)
                            Instantiate(TreePrefab, tpos, Quaternion.Euler(0, Random.Range(0, 360f), 0));
                    }
                }
            }

            // Street lamps along roads in furniture zone
            PlaceStreetLamps(origin, cellW, cellZ, totalW, totalZ);

            PickRandomDeliveryTarget();
        }

        private void LoadPrefabs()
        {
            if (BuildingPrefabs == null || BuildingPrefabs.Length == 0)
                BuildingPrefabs = Resources.LoadAll<GameObject>("Prefabs/Buildings");
            if (HousePrefabs == null || HousePrefabs.Length == 0)
                HousePrefabs = Resources.LoadAll<GameObject>("Prefabs/Buildings");
            if (RoadTilePrefabs == null || RoadTilePrefabs.Length == 0)
                RoadTilePrefabs = Resources.LoadAll<GameObject>("Prefabs/Roads");
            if (TreePrefab == null)
                TreePrefab = Resources.Load<GameObject>("Prefabs/Natures/Natures_Big Tree");
            if (StreetLightPrefab == null)
                StreetLightPrefab = Resources.Load<GameObject>("Prefabs/Props/Props_Street Light");
        }

        private void PlaceRoadTile(Vector3 origin, int col, int row, float cellW, float cellZ, float totalW, float totalZ)
        {
            if (RoadTilePrefabs == null || RoadTilePrefabs.Length == 0)
            {
                // Fallback: simple cube road
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Road {col}_{row}";
                var x = origin.x + col * cellW + RoadWidth * 0.5f;
                var z = origin.z + row * cellZ + RoadWidth * 0.5f;
                var isHorizontal = (col == 0 || col == BlocksX) && row < BlocksZ;
                var isVertical = (row == 0 || row == BlocksZ) && col < BlocksX;
                var isIntersection = col > 0 && col < BlocksX && row > 0 && row < BlocksZ;
                if (isHorizontal) { go.transform.position = new Vector3(x, GroundY + 0.05f, z); go.transform.localScale = new Vector3(RoadWidth, 0.1f, totalZ); }
                else if (isVertical) { go.transform.position = new Vector3(x, GroundY + 0.05f, z); go.transform.localScale = new Vector3(totalW, 0.1f, RoadWidth); }
                else if (isIntersection) { go.transform.position = new Vector3(x, GroundY + 0.05f, z); go.transform.localScale = new Vector3(RoadWidth, 0.1f, RoadWidth); }
                else go.transform.localScale = Vector3.zero;
                go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.25f, 0.27f, 0.30f) };
                go.isStatic = true;
                return;
            }

            var prefab = RoadTilePrefabs[Random.Range(0, RoadTilePrefabs.Length)];
            var rx = origin.x + col * cellW + RoadWidth * 0.5f;
            var rz = origin.z + row * cellZ + RoadWidth * 0.5f;
            var rot = Quaternion.identity;
            // Corner rotation
            if (col == 0 && row == 0) rot = Quaternion.identity;
            else if (col == BlocksX && row == 0) rot = Quaternion.Euler(0, 90, 0);
            else if (col == BlocksX && row == BlocksZ) rot = Quaternion.Euler(0, 180, 0);
            else if (col == 0 && row == BlocksZ) rot = Quaternion.Euler(0, 270, 0);
            else if (col == BlocksX) rot = Quaternion.Euler(0, 90, 0);
            else if (row == BlocksZ) rot = Quaternion.Euler(0, 180, 0);
            else if (col == 0) rot = Quaternion.identity;
            else if (row == 0) rot = Quaternion.identity;

            var go2 = Instantiate(prefab, new Vector3(rx, GroundY + 0.02f, rz), rot);
            var s = RoadWidth / 9f;
            go2.transform.localScale = new Vector3(s, 1, s);
        }

        private void BuildSidewalks(Vector3 origin, float cellW, float cellZ, float totalW, float totalZ)
        {
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ;
                // Top sidewalk (north side of road)
                PlaceSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.04f, z - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.5f), false);
                // Bottom sidewalk (south side)
                PlaceSidewalkStrip(totalW, new Vector3(origin.x + totalW * 0.5f, GroundY + 0.04f, z + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.5f), false);
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW;
                PlaceSidewalkStrip(totalZ, new Vector3(x - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.5f, GroundY + 0.04f, origin.z + totalZ * 0.5f), true);
                PlaceSidewalkStrip(totalZ, new Vector3(x + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.5f, GroundY + 0.04f, origin.z + totalZ * 0.5f), true);
            }

            // Connect sidewalks at corners (intersections)
            // Small fill-in squares at each intersection corner
            for (var row = 0; row <= BlocksZ; row++)
            {
                for (var col = 0; col <= BlocksX; col++)
                {
                    var cx = origin.x + col * cellW;
                    var cz = origin.z + row * cellZ;

                    // Four corners of each intersection
                    Vector3[] corners = {
                        new(cx - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.0f, GroundY + 0.04f, cz - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.0f),
                        new(cx + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.0f, GroundY + 0.04f, cz - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.0f),
                        new(cx - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth - SidewalkWidth * 0.0f, GroundY + 0.04f, cz + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.0f),
                        new(cx + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.0f, GroundY + 0.04f, cz + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth + SidewalkWidth * 0.0f)
                    };
                    foreach (var corner in corners)
                    {
                        var fill = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        fill.name = "Sidewalk corner";
                        fill.transform.position = corner;
                        fill.transform.localScale = new Vector3(SidewalkWidth, 0.08f, SidewalkWidth);
                        fill.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.65f, 0.65f, 0.62f) };
                        fill.isStatic = true;
                    }
                }
            }
        }

        private void PlaceSidewalkStrip(float length, Vector3 center, bool isVertical)
        {
            if (SidewalkPrefab != null)
            {
                var go = Instantiate(SidewalkPrefab, center, isVertical ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
                go.transform.localScale = new Vector3(length / 3f, 1, 1);
                return;
            }
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Sidewalk";
            cube.transform.position = center;
            cube.transform.localScale = isVertical
                ? new Vector3(SidewalkWidth, 0.08f, length)
                : new Vector3(length, 0.08f, SidewalkWidth);
            cube.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.65f, 0.65f, 0.62f) };
            cube.isStatic = true;
        }

        private void BuildStreetFurniture(Vector3 origin, float cellW, float cellZ, float totalW, float totalZ)
        {
            // Center road dashes
            var dashMat = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.85f) };
            for (var row = 0; row <= BlocksZ; row++)
            {
                var z = origin.z + row * cellZ;
                for (var i = 0; i < totalW / 3f; i++)
                {
                    var dx = origin.x + i * 3f + 1.5f;
                    if (dx > origin.x + totalW) break;
                    var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    d.name = "Road dash";
                    d.transform.position = new Vector3(dx, GroundY + 0.07f, z);
                    d.transform.localScale = new Vector3(0.3f, 0.02f, 1.2f);
                    d.GetComponent<Renderer>().material = dashMat;
                    d.isStatic = true;
                }
            }
            for (var col = 0; col <= BlocksX; col++)
            {
                var x = origin.x + col * cellW;
                for (var i = 0; i < totalZ / 3f; i++)
                {
                    var dz = origin.z + i * 3f + 1.5f;
                    if (dz > origin.z + totalZ) break;
                    var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    d.name = "Road dash";
                    d.transform.position = new Vector3(x, GroundY + 0.07f, dz);
                    d.transform.localScale = new Vector3(0.3f, 0.02f, 1.2f);
                    d.GetComponent<Renderer>().material = dashMat;
                    d.isStatic = true;
                }
            }
        }

        private void PlaceBlockBuildings(Vector3 center, int col, int row, float cellW, float cellZ, Vector3 origin)
        {
            var setback = col == 0 || row == 0 ? BuildingSetbackCommercial : BuildingSetbackResidential;
            var halfBlock = BlockSize * 0.5f - setback;
            BuildingPositions.Add(center);

            // Place 2-4 buildings along the block perimeter, facades facing outward
            var count = Random.Range(2, 5);
            for (var b = 0; b < count; b++)
            {
                var prefab = PickRandomPrefab();
                if (prefab == null) continue;

                var angle = 0;
                // Position on one of 4 sides of the block
                var side = Random.Range(0, 4);
                float px, pz;
                switch (side)
                {
                    case 0: // North side (facing south)
                        px = center.x + Random.Range(-halfBlock + 2f, halfBlock - 2f);
                        pz = center.z - halfBlock + 1f;
                        angle = 0;
                        break;
                    case 1: // East side (facing west)
                        px = center.x + halfBlock - 1f;
                        pz = center.z + Random.Range(-halfBlock + 2f, halfBlock - 2f);
                        angle = 90;
                        break;
                    case 2: // South side (facing north)
                        px = center.x + Random.Range(-halfBlock + 2f, halfBlock - 2f);
                        pz = center.z + halfBlock - 1f;
                        angle = 180;
                        break;
                    default: // West side (facing east)
                        px = center.x - halfBlock + 1f;
                        pz = center.z + Random.Range(-halfBlock + 2f, halfBlock - 2f);
                        angle = 270;
                        break;
                }

                var pos = new Vector3(px, GroundY + 0.05f, pz);
                Instantiate(prefab, pos, Quaternion.Euler(0, angle, 0));
            }
        }

        private void PlaceStreetLamps(Vector3 origin, float cellW, float cellZ, float totalW, float totalZ)
        {
            if (StreetLightPrefab == null) return;

            // Along horizontal roads
            for (var row = 0; row <= BlocksZ; row++)
            {
                var zLamp = origin.z + row * cellZ - RoadWidth * 0.5f - CurbWidth - FurnitureZoneWidth * 0.5f;
                var zLamp2 = origin.z + row * cellZ + RoadWidth * 0.5f + CurbWidth + FurnitureZoneWidth * 0.5f;
                for (var x = origin.x + LampPostSpacing; x < origin.x + totalW; x += LampPostSpacing)
                {
                    if (StreetLightPrefab != null)
                    {
                        Instantiate(StreetLightPrefab, new Vector3(x, GroundY + 0.05f, zLamp), Quaternion.Euler(0, 180, 0));
                        Instantiate(StreetLightPrefab, new Vector3(x, GroundY + 0.05f, zLamp2), Quaternion.Euler(0, 0, 0));
                    }
                }
            }
        }

        private void PlaceIkea(Vector3 pos)
        {
            // Place as large building in center of block
            var prefab = FindPrefabByName("Super Market");
            if (prefab == null) prefab = PickRandomPrefab();
            if (prefab != null)
            {
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.transform.localScale = new Vector3(1.5f, 1f, 1.2f);
            }
            else
            {
                // Fallback
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
        }

        public void PickRandomDeliveryTarget()
        {
            if (BuildingPositions.Count > 0)
                DeliveryTargetPosition = BuildingPositions[Random.Range(0, BuildingPositions.Count)];
            else
                DeliveryTargetPosition = IkeaPosition + new Vector3(15, 0, 15);
        }

        private GameObject PickRandomPrefab()
        {
            var all = new List<GameObject>();
            if (HousePrefabs != null) all.AddRange(HousePrefabs);
            if (BuildingPrefabs != null) all.AddRange(BuildingPrefabs);
            if (all.Count == 0) return null;
            return all[Random.Range(0, all.Count)];
        }

        private GameObject FindPrefabByName(string name)
        {
            foreach (var p in BuildingPrefabs ?? new GameObject[0])
                if (p != null && p.name.Contains(name)) return p;
            return null;
        }

        private void BuildGround(float w, float z, Vector3 origin)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "City Ground";
            go.transform.position = origin + new Vector3(w * 0.5f, -0.1f, z * 0.5f);
            go.transform.localScale = new Vector3(w + 40, 0.2f, z + 40);
            go.GetComponent<Renderer>().material = new Material(Shader.Find("Standard")) { color = new Color(0.12f, 0.25f, 0.14f) };
            go.isStatic = true;
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
                    go.name.StartsWith("Sidewalk corner") ||
                    go.name.EndsWith("(Clone)"))
                    destroyList.Add(go);
            }
            foreach (var go in destroyList)
                Object.DestroyImmediate(go);
        }
    }
}
