using UnityEngine;

namespace FlatpackPanic
{
    /// <summary>
    /// Loads prefabs from Resources folders so the city generator
    /// works at runtime without manual inspector assignment.
    /// </summary>
    public static class CityResourceLoader
    {
        private static GameObject[] _buildingPrefabs;
        private static GameObject[] _housePrefabs;
        private static GameObject[] _roadPrefabs;
        private static GameObject _treePrefab;
        private static GameObject _streetLightPrefab;
        private static GameObject _sidewalkPrefab;

        public static GameObject[] BuildingPrefabs => Cache(ref _buildingPrefabs, "Prefabs/Buildings");
        public static GameObject[] HousePrefabs => Cache(ref _housePrefabs, "Prefabs/Buildings"); // reuse
        public static GameObject[] RoadPrefabs => Cache(ref _roadPrefabs, "Prefabs/Roads");
        public static GameObject TreePrefab => CacheSingle(ref _treePrefab, "Prefabs/Natures/Big Tree");
        public static GameObject StreetLightPrefab => CacheSingle(ref _streetLightPrefab, "Prefabs/Props/Street Light");
        public static GameObject SidewalkPrefab => CacheSingle(ref _sidewalkPrefab, "Prefabs/Roads/Sidewalk");

        private static GameObject[] Cache(ref GameObject[] cache, string path)
        {
            if (cache == null || cache.Length == 0)
                cache = Resources.LoadAll<GameObject>(path);
            return cache ?? new GameObject[0];
        }

        private static GameObject CacheSingle(ref GameObject cache, string path)
        {
            if (cache == null)
                cache = Resources.Load<GameObject>(path);
            return cache;
        }

        public static void AssignTo(CityGenerator gen)
        {
            gen.BuildingPrefabs = BuildingPrefabs;
            gen.HousePrefabs = HousePrefabs;
            gen.RoadTilePrefabs = RoadPrefabs;
            gen.TreePrefab = TreePrefab;
            gen.StreetLightPrefab = StreetLightPrefab;
            gen.SidewalkPrefab = SidewalkPrefab;
        }
    }
}
