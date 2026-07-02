using System.Collections.Generic;
using UnityEngine;

namespace FlatpackPanic
{
    public class FlatpackGame : MonoBehaviour
    {
        public static FlatpackGame Instance { get; private set; }
        public readonly List<CargoBox> Cargo = new();
        public Transform PickupZone { get; private set; }
        public Transform DeliveryZone { get; private set; }
        public VanController Van { get; private set; }
        public FirstPersonPlayer Player { get; private set; }
        public CityGenerator City { get; private set; }

        private Material _road, _grass, _warehouse, _apartment, _cardboard, _blue, _yellow, _green, _red, _cyan, _black;
        private float _startedAt;

        public bool HelpVisible { get; set; } = true;
        public bool MissionComplete => DeliveredCount >= Cargo.Count;
        public float ElapsedSeconds => Time.time - _startedAt;
        public int LoadedCount { get { var c = 0; foreach (var box in Cargo) if (box.StoredInVan) c++; return c; } }
        public int DeliveredCount { get { var c = 0; foreach (var box in Cargo) if (IsCargoDelivered(box)) c++; return c; } }
        public float TotalDamage { get { var d = 0f; foreach (var box in Cargo) d += box.Damage; return d; } }
        public string MissionText
        {
            get
            {
                if (DeliveredCount >= Cargo.Count) return "Complete: delivery finished. Check your rank.";
                if (LoadedCount < Cargo.Count) return $"Load cargo into the van: {LoadedCount}/{Cargo.Count}";
                return $"Drive to the green delivery zone and unload: {DeliveredCount}/{Cargo.Count}";
            }
        }
        public string Rank
        {
            get
            {
                if (DeliveredCount < Cargo.Count) return "IN PROGRESS";
                var score = 100f - TotalDamage * 10f - ElapsedSeconds * .03f + DeliveredCount * 12f;
                if (score > 105) return "S — Swedish Legend";
                if (score > 85) return "A — Almost Professionals";
                if (score > 65) return "B — At least it arrived";
                if (score > 45) return "C — Client is crying";
                return "F — IKEA banned you";
            }
        }

        private void Awake()
        {
            Instance = this;
            _startedAt = Time.time;
            CreateMaterials();
            BuildCity();
            BuildVan();
            BuildFirstPersonPlayer();
            BuildCargo();
            BuildLights();
            gameObject.AddComponent<GameHud>();
        }

        public void ResetCargo()
        {
            // Spawn cargo back at IKEA pickup zone
            var points = new[] { new Vector3(-4, 1.2f, -4), new Vector3(-4, 1.2f, 0), new Vector3(-4, 1.2f, 4) };
            if (City != null)
            {
                var ikea = City.IkeaPosition;
                points[0] = ikea + new Vector3(-4, 1.2f, -4);
                points[1] = ikea + new Vector3(-4, 1.2f, 0);
                points[2] = ikea + new Vector3(-4, 1.2f, 4);
            }
            for (var i = 0; i < Cargo.Count; i++)
            {
                Cargo[i].RemoveFromVan(points[i], Quaternion.Euler(0, i * 17f, 0));
                var rb = Cargo[i].GetComponent<Rigidbody>();
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Cargo[i].ResetDamage();
            }
            _startedAt = Time.time;
        }

        // After delivery, generate a new target
        public void NewDeliveryTarget()
        {
            if (City != null)
            {
                City.PickRandomDeliveryTarget();
                UpdateDeliveryZonePosition(City.DeliveryTargetPosition);
            }
        }

        private void UpdateDeliveryZonePosition(Vector3 aptPos)
        {
            if (DeliveryZone != null && aptPos != Vector3.zero)
            {
                // Place zone on the street in front of the apartment
                var streetPos = aptPos + new Vector3(8, 0.15f, 0);
                DeliveryZone.position = streetPos;
            }
        }

        public bool IsCargoInVan(CargoBox cargo)
        {
            return cargo != null && cargo.StoredInVan;
        }

        public bool IsCargoDelivered(CargoBox cargo)
        {
            if (cargo == null || cargo.StoredInVan || DeliveryZone == null) return false;
            return Vector3.Distance(cargo.transform.position, DeliveryZone.position) < 5.8f && cargo.transform.position.y < 2.5f;
        }

        public bool IsCargoInsideVanBay(CargoBox cargo)
        {
            if (Van == null || cargo == null || cargo.StoredInVan) return false;
            var local = Van.transform.InverseTransformPoint(cargo.transform.position);
            return Mathf.Abs(local.x) < 1.36f && local.z > -3.15f && local.z < 0.75f && local.y > -0.55f && local.y < 1.55f;
        }

        public bool IsPlayerAtVanRear()
        {
            if (Van == null || Player == null || Player.IsDriving) return false;
            var local = Van.transform.InverseTransformPoint(Player.transform.position);
            return Mathf.Abs(local.x) < 2.6f && local.z < -2.25f && local.z > -5.8f && Mathf.Abs(local.y) < 2.5f;
        }

        public CargoBox FirstStoredCargo()
        {
            foreach (var box in Cargo) if (box.StoredInVan) return box;
            return null;
        }

        public void StoreCargoInVan(CargoBox cargo)
        {
            if (cargo == null || cargo.StoredInVan) return;
            cargo.StoreInVan();
        }

        public CargoBox TakeCargoFromVan(Transform holdPoint)
        {
            var cargo = FirstStoredCargo();
            if (cargo == null || Player == null) return null;
            var spawnPos = holdPoint != null ? holdPoint.position : Player.transform.position + Player.transform.forward * 1.5f + Vector3.up * 1.2f;
            var spawnRot = Player.PlayerCamera != null ? Quaternion.LookRotation(Player.PlayerCamera.transform.forward, Vector3.up) : Player.transform.rotation;
            cargo.RemoveFromVan(spawnPos, spawnRot);
            return cargo;
        }

        private void CreateMaterials()
        {
            _road = Mat("Asphalt", new Color(.055f, .06f, .07f));
            _grass = Mat("Grass", new Color(.07f, .2f, .1f));
            _warehouse = Mat("Warehouse Blue", new Color(.02f, .16f, .68f));
            _apartment = Mat("Concrete", new Color(.55f, .57f, .62f));
            _cardboard = Mat("Cardboard", new Color(.62f, .43f, .23f));
            _blue = Mat("IKEA Blue", new Color(.02f, .12f, .55f));
            _yellow = Mat("Panic Yellow", new Color(1f, .82f, .08f));
            _green = Mat("Delivery Green", new Color(.1f, .84f, .42f));
            _red = Mat("Borya Red", new Color(.9f, .18f, .14f));
            _cyan = Mat("Sanya Cyan", new Color(.1f, .8f, .95f));
            _black = Mat("Rubber", new Color(.015f, .015f, .018f));
        }

        private static Material Mat(string name, Color color) { var m = new Material(Shader.Find("Standard")); m.name = name; m.color = color; return m; }

        private void BuildCity()
        {
            var cityGo = new GameObject("CityGenerator");
            City = cityGo.AddComponent<CityGenerator>();

            // Try to load prefabs from Resources (SimplePoly City)
            City.BuildingPrefabs = Resources.LoadAll<GameObject>("SimplePoly City - Low Poly Assets/Prefabs/Buildings");
            City.HousePrefabs = Resources.LoadAll<GameObject>("SimplePoly City - Low Poly Assets/Prefabs/Buildings");
            City.RoadTilePrefabs = Resources.LoadAll<GameObject>("SimplePoly City - Low Poly Assets/Prefabs/Roads");
            City.TreePrefab = Resources.Load<GameObject>("SimplePoly City - Low Poly Assets/Prefabs/Natures/Big Tree");
            City.StreetLightPrefab = Resources.Load<GameObject>("SimplePoly City - Low Poly Assets/Prefabs/Props/Street Light");

            City.BlocksX = 6;
            City.BlocksZ = 5;
            City.BlockSize = 16f;
            City.RoadWidth = 9f;
            City.Generate();

            // Make the IKEA pickup zone yellow
            var ikea = City.IkeaPosition;
            PickupZone = Cube("IKEA pickup zone", ikea + new Vector3(0, 0.15f, 8), new Vector3(10, 0.15f, 5), _yellow, true).transform;

            // Delivery zone at the first random target
            var apt = City.DeliveryTargetPosition;
            var dzPos = apt != Vector3.zero ? apt + new Vector3(8, 0.15f, 0) : new Vector3(20, 0.15f, 10);
            DeliveryZone = Cube("Delivery zone", dzPos, new Vector3(8, 0.15f, 8), _green, true).transform;

            // Green beacon
            Cube("Delivery beacon", dzPos + new Vector3(0, 3.2f, 0), new Vector3(.55f, 6f, .55f), _green, true);
            Cube("Delivery arrow", dzPos + new Vector3(0, 6.55f, 0), new Vector3(4.8f, .35f, 1.2f), _yellow, true);

            // Warehouse building over IKEA
            Cube("Flatpack Warehouse main", ikea + new Vector3(0, 3, 0), new Vector3(14, 6, 10), _warehouse, true);
            Cube("Warehouse sign", ikea + new Vector3(0, 7, 5.5f), new Vector3(10, 1.5f, .3f), _yellow, true);
        }

        private void BuildVan()
        {
            var root = new GameObject("First Person Delivery Van");
            root.transform.SetPositionAndRotation(new Vector3(-17, 1.05f, -2), Quaternion.Euler(0, 90, 0));

            // Position van on the road, not inside a building
            if (City != null)
            {
                var spawnPos = City.VanSpawnRoadPos + new Vector3(0, 1.05f, 0);
                root.transform.position = spawnPos;
                root.transform.rotation = Quaternion.identity;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.drag = 0.05f;
            rb.angularDrag = 1f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            AddVanCollider(root, new Vector3(0, -0.55f, 0f), new Vector3(3.35f, 0.32f, 6.45f));
            AddVanCollider(root, new Vector3(0, 0.15f, 2.25f), new Vector3(3.25f, 1.9f, 2.1f));
            AddVanCollider(root, new Vector3(-1.68f, 0.25f, -1.25f), new Vector3(0.18f, 1.35f, 3.35f));
            AddVanCollider(root, new Vector3(1.68f, 0.25f, -1.25f), new Vector3(0.18f, 1.35f, 3.35f));

            var centerOfMass = new GameObject("CenterOfMass").transform;
            centerOfMass.SetParent(root.transform, false);
            centerOfMass.localPosition = new Vector3(0, -0.55f, -0.1f);
            rb.centerOfMass = centerOfMass.localPosition;

            var body = Cube("Van body", Vector3.zero, new Vector3(3.2f, 2.0f, 6), _blue, false); body.transform.SetParent(root.transform, false); body.GetComponent<Collider>().enabled = false;
            var cabin = Cube("Van cabin", new Vector3(0, .5f, 2.2f), new Vector3(3.05f, 1.9f, 2.1f), _yellow, false); cabin.transform.SetParent(root.transform, false); cabin.GetComponent<Collider>().enabled = false;
            var windshield = Cube("Windshield", new Vector3(0, 1.15f, 3.28f), new Vector3(2.2f, .65f, .08f), _black, false); windshield.transform.SetParent(root.transform, false); windshield.GetComponent<Collider>().enabled = false;
            var cargoBay = Cube("Open cargo bay", new Vector3(0, .1f, -1.1f), new Vector3(2.8f, 1.35f, 3.4f), _road, false); cargoBay.transform.SetParent(root.transform, false); cargoBay.GetComponent<Collider>().enabled = false;

            var frontLeft = CreateWheel(root.transform, "FL", new Vector3(-1.72f, -0.72f, 2.05f));
            var frontRight = CreateWheel(root.transform, "FR", new Vector3(1.72f, -0.72f, 2.05f));
            var rearLeft = CreateWheel(root.transform, "RL", new Vector3(-1.72f, -0.72f, -2.05f));
            var rearRight = CreateWheel(root.transform, "RR", new Vector3(1.72f, -0.72f, -2.05f));

            Van = root.AddComponent<VanController>();
            Van.CenterOfMass = centerOfMass;
            Van.FrontLeftCollider = frontLeft.collider;
            Van.FrontRightCollider = frontRight.collider;
            Van.RearLeftCollider = rearLeft.collider;
            Van.RearRightCollider = rearRight.collider;
            Van.FrontLeftMesh = frontLeft.mesh;
            Van.FrontRightMesh = frontRight.mesh;
            Van.RearLeftMesh = rearLeft.mesh;
            Van.RearRightMesh = rearRight.mesh;
        }

        private static BoxCollider AddVanCollider(GameObject root, Vector3 center, Vector3 size)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
            return col;
        }

        private (WheelCollider collider, Transform mesh) CreateWheel(Transform parent, string id, Vector3 localPosition)
        {
            var colliderObj = new GameObject("WheelCollider_" + id);
            colliderObj.transform.SetParent(parent, false);
            colliderObj.transform.localPosition = localPosition;
            var wheelCollider = colliderObj.AddComponent<WheelCollider>();
            wheelCollider.radius = 0.38f;
            wheelCollider.suspensionDistance = 0.23f;

            var meshObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            meshObj.name = "WheelMesh_" + id;
            meshObj.transform.SetParent(parent, false);
            meshObj.transform.localPosition = localPosition;
            meshObj.transform.localRotation = Quaternion.Euler(0, 0, 90);
            meshObj.transform.localScale = new Vector3(0.76f, 0.28f, 0.76f);
            meshObj.GetComponent<Renderer>().material = _black;
            Destroy(meshObj.GetComponent<Collider>());

            return (wheelCollider, meshObj.transform);
        }

        private void BuildFirstPersonPlayer()
        {
            var player = new GameObject("First Person Player — Borya Barrel POV");
            // Spawn on the sidewalk near the van spawn, not inside a building
            var spawnPos = City != null ? City.VanSpawnRoadPos + new Vector3(2, 1.2f, 0) : new Vector3(-24, 1.2f, -13);
            player.transform.position = spawnPos;
            var rb = player.AddComponent<Rigidbody>(); rb.mass = 95f; rb.freezeRotation = true;
            var capsule = player.AddComponent<CapsuleCollider>(); capsule.height = 1.65f; capsule.radius = .48f; capsule.center = new Vector3(0, .82f, 0);
            Player = player.AddComponent<FirstPersonPlayer>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Borya visible belly"; body.transform.SetParent(player.transform, false);
            body.transform.localPosition = new Vector3(0, .72f, 0); body.transform.localScale = new Vector3(1.05f, .55f, 1.05f); body.GetComponent<Renderer>().material = _red; Destroy(body.GetComponent<Collider>());

            var sanya = new GameObject("Slender Sanya NPC placeholder"); sanya.transform.position = player.transform.position + new Vector3(2, 0, 0);
            var sbody = GameObject.CreatePrimitive(PrimitiveType.Capsule); sbody.name = "Slender Sanya tall body"; sbody.transform.SetParent(sanya.transform, false); sbody.transform.localPosition = new Vector3(0, 1.35f, 0); sbody.transform.localScale = new Vector3(.55f, 1.35f, .55f); sbody.GetComponent<Renderer>().material = _cyan; Destroy(sbody.GetComponent<Collider>());
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name = "long confused head"; head.transform.SetParent(sanya.transform, false); head.transform.localPosition = new Vector3(0, 2.85f, 0); head.transform.localScale = new Vector3(.5f, .7f, .5f); head.GetComponent<Renderer>().material = _yellow; Destroy(head.GetComponent<Collider>());
        }

        private void BuildCargo()
        {
            // Spawn cargo on the sidewalk in front of IKEA entrance
            var basePos = City != null ? City.IkeaPosition + new Vector3(-2, 1.2f, 6) : new Vector3(-29, 1.3f, -9);
            CreateCargo("PAX cursed wardrobe", basePos, new Vector3(1.2f, 1, 5.5f), 52f);
            CreateCargo("Mirror DO NOT DROP", basePos + new Vector3(2, -0.15f, 3.5f), new Vector3(.45f, 1.6f, 4), 25f);
            CreateCargo("Mystery screws box", basePos + new Vector3(4, -0.2f, 1.5f), new Vector3(2, 1.5f, 1.8f), 34f);
        }

        private void CreateCargo(string label, Vector3 pos, Vector3 scale, float mass)
        {
            var box = Cube(label, pos, scale, _cardboard, false);
            var rb = box.AddComponent<Rigidbody>(); rb.mass = mass; rb.drag = .18f; rb.angularDrag = .12f; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var cargo = box.AddComponent<CargoBox>(); cargo.Label = label; Cargo.Add(cargo);
            var stripe = Cube(label + " blue tape", Vector3.zero, new Vector3(1.02f, .035f, .22f), _blue, false); stripe.transform.SetParent(box.transform, false); stripe.transform.localPosition = new Vector3(0, .53f, 0); stripe.GetComponent<Collider>().enabled = false;
        }

        private void BuildLights()
        {
            var light = new GameObject("Sun"); var l = light.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.25f; light.transform.rotation = Quaternion.Euler(48, -35, 0);
        }

        private static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, bool isStatic)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().material = mat; go.isStatic = isStatic; return go;
        }
    }
}
