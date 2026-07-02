using UnityEngine;

namespace FlatpackPanic
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class FirstPersonPlayer : MonoBehaviour
    {
        public Camera PlayerCamera;
        public float WalkSpeed = 4.2f;
        public float SprintSpeed = 6.2f;
        public float MouseSensitivity = 3.8f;
        public float JumpImpulse = 4.5f;
        public float InteractDistance = 4.2f;
        public float InteractRadius = 0.8f;
        public Transform HoldPoint;

        public CargoBox HeldCargo { get; private set; }
        public bool IsDriving { get; private set; }
        public string InteractionPrompt { get; private set; }

        private Rigidbody _rb;
        private CapsuleCollider _collider;
        private VanController _currentVan;
        private float _pitch;
        private bool _grounded;
        private Vector3 _savedVelocity;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<CapsuleCollider>();
            _rb.freezeRotation = true;
            _rb.mass = 85f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (PlayerCamera == null)
            {
                var camObj = new GameObject("First Person Camera");
                camObj.transform.SetParent(transform, false);
                camObj.transform.localPosition = new Vector3(0, 1.65f, 0);
                PlayerCamera = camObj.AddComponent<Camera>();
            }

            PlayerCamera.tag = "MainCamera";
            PlayerCamera.fieldOfView = 75f;

            if (HoldPoint == null)
            {
                var hold = new GameObject("Cargo Hold Point");
                hold.transform.SetParent(PlayerCamera.transform, false);
                hold.transform.localPosition = new Vector3(0, -0.28f, 2.25f);
                HoldPoint = hold.transform;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Look();
            UpdateInteractionPrompt();

            if (Input.GetKeyDown(KeyCode.E)) Interact();
            if (Input.GetKeyDown(KeyCode.F)) ToggleDrive();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void FixedUpdate()
        {
            if (IsDriving)
            {
                transform.position = _currentVan.DriverSeatWorld;
                _rb.velocity = Vector3.zero;
                return;
            }

            Move();
            HoldCargoPhysics();
        }

        private void Look()
        {
            var mx = Input.GetAxis("Mouse X") * MouseSensitivity;
            var my = Input.GetAxis("Mouse Y") * MouseSensitivity;

            if (IsDriving && _currentVan != null)
            {
                // Pass look input to van for third-person camera orbit
                _currentVan.SetLookInput(mx, my);
                // Keep player transform aligned with van
                transform.rotation = Quaternion.Euler(0, _currentVan.transform.eulerAngles.y, 0);
                // Smooth chase camera
                PlayerCamera.transform.position = Vector3.Lerp(PlayerCamera.transform.position, _currentVan.ThirdPersonCameraPosition, Time.deltaTime * 10f);
                PlayerCamera.transform.rotation = Quaternion.Slerp(PlayerCamera.transform.rotation, _currentVan.ThirdPersonCameraRotation, Time.deltaTime * 10f);
                return;
            }

            transform.Rotate(Vector3.up * mx);
            _pitch = Mathf.Clamp(_pitch - my, -82f, 82f);
            PlayerCamera.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        private void Move()
        {
            var x = Input.GetAxisRaw("Horizontal");
            var z = Input.GetAxisRaw("Vertical");
            var input = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
            var speed = Input.GetKey(KeyCode.LeftShift) ? SprintSpeed : WalkSpeed;

            var target = input * speed;
            var velocity = _rb.velocity;
            var change = new Vector3(target.x - velocity.x, 0, target.z - velocity.z);
            _rb.AddForce(change * 9f, ForceMode.Acceleration);

            _grounded = Physics.Raycast(transform.position + Vector3.up * 0.15f, Vector3.down, _collider.height * 0.55f + 0.25f);
            if (_grounded && Input.GetKey(KeyCode.Space))
            {
                _rb.AddForce(Vector3.up * JumpImpulse, ForceMode.Impulse);
            }
        }

        private void Interact()
        {
            if (IsDriving) return;

            if (HeldCargo != null)
            {
                if (FlatpackGame.Instance != null && FlatpackGame.Instance.IsCargoInsideVanBay(HeldCargo))
                {
                    LoadHeldCargoIntoVan();
                    return;
                }

                DropCargo(false);
                return;
            }

            if (FlatpackGame.Instance != null && FlatpackGame.Instance.IsPlayerAtVanRear() && FlatpackGame.Instance.LoadedCount > 0)
            {
                TakeCargoFromVan();
                return;
            }

            var cargo = FindBestCargoCandidate();
            if (cargo != null) GrabCargo(cargo);
        }

        private void UpdateInteractionPrompt()
        {
            InteractionPrompt = string.Empty;
            if (IsDriving)
            {
                InteractionPrompt = "Press F to exit van";
                return;
            }

            if (HeldCargo != null)
            {
                if (FlatpackGame.Instance != null && FlatpackGame.Instance.IsCargoInsideVanBay(HeldCargo))
                    InteractionPrompt = $"Press E to load {HeldCargo.Label} into van";
                else
                    InteractionPrompt = $"Press E to drop {HeldCargo.Label}";
                return;
            }

            if (FlatpackGame.Instance != null && FlatpackGame.Instance.IsPlayerAtVanRear() && FlatpackGame.Instance.LoadedCount > 0)
            {
                var stored = FlatpackGame.Instance.FirstStoredCargo();
                InteractionPrompt = $"Press E to take {stored.Label} from van";
                return;
            }

            var cargo = FindBestCargoCandidate();
            if (cargo != null)
            {
                cargo.HighlightForPickup();
                InteractionPrompt = $"Press E to pick up {cargo.Label}";
                return;
            }

            var doorVan = FindNearestVanDoor();
            if (doorVan != null) InteractionPrompt = "Press F to enter van";
        }

        private CargoBox FindBestCargoCandidate()
        {
            if (PlayerCamera == null) return null;

            var origin = PlayerCamera.transform.position;
            var direction = PlayerCamera.transform.forward;

            // Forgiving aim: the player no longer needs to hit the exact cube collider center.
            if (Physics.SphereCast(origin, InteractRadius, direction, out var hit, InteractDistance))
            {
                var aimedCargo = hit.collider.GetComponentInParent<CargoBox>();
                if (aimedCargo != null && !aimedCargo.StoredInVan) return aimedCargo;
            }

            // Fallback: nearest cargo roughly in front of the player.
            CargoBox best = null;
            var bestScore = float.NegativeInfinity;
            var nearby = Physics.OverlapSphere(origin + direction * (InteractDistance * 0.55f), InteractDistance * 0.65f);
            foreach (var col in nearby)
            {
                var cargo = col.GetComponentInParent<CargoBox>();
                if (cargo == null || cargo.HeldByFirstPerson != null || cargo.StoredInVan) continue;

                var toCargo = cargo.transform.position - origin;
                var distance = toCargo.magnitude;
                if (distance > InteractDistance + 1f) continue;

                var alignment = Vector3.Dot(direction, toCargo.normalized);
                if (alignment < 0.25f) continue;

                var score = alignment * 2.2f - distance * 0.18f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = cargo;
                }
            }

            return best;
        }

        private void GrabCargo(CargoBox cargo)
        {
            if (cargo == null || cargo.StoredInVan) return;
            HeldCargo = cargo;
            cargo.HeldByFirstPerson = this;
            var rb = cargo.GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.drag = 4.2f;
            rb.angularDrag = 6f;
            rb.maxAngularVelocity = 3.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void DropCargo(bool throwIt)
        {
            if (HeldCargo == null) return;
            var rb = HeldCargo.GetComponent<Rigidbody>();
            rb.drag = HeldCargo.DefaultDrag;
            rb.angularDrag = HeldCargo.DefaultAngularDrag;
            rb.maxAngularVelocity = 7f;
            if (throwIt) rb.AddForce(PlayerCamera.transform.forward * 4f, ForceMode.Impulse);
            HeldCargo.HeldByFirstPerson = null;
            HeldCargo = null;
        }

        private void LoadHeldCargoIntoVan()
        {
            if (HeldCargo == null || FlatpackGame.Instance == null) return;
            var cargo = HeldCargo;
            cargo.HeldByFirstPerson = null;
            HeldCargo = null;
            FlatpackGame.Instance.StoreCargoInVan(cargo);
        }

        private void TakeCargoFromVan()
        {
            if (FlatpackGame.Instance == null) return;
            var cargo = FlatpackGame.Instance.TakeCargoFromVan(HoldPoint);
            if (cargo != null) GrabCargo(cargo);
        }

        private void HoldCargoPhysics()
        {
            if (HeldCargo == null) return;
            var rb = HeldCargo.GetComponent<Rigidbody>();
            var targetPosition = HoldPoint.position;
            if (Physics.Raycast(PlayerCamera.transform.position, PlayerCamera.transform.forward, out var wallHit, 1.55f))
            {
                if (wallHit.collider.GetComponentInParent<CargoBox>() == null)
                    targetPosition = PlayerCamera.transform.position + PlayerCamera.transform.forward * Mathf.Max(0.85f, wallHit.distance - 0.35f);
            }

            var toTarget = targetPosition - HeldCargo.transform.position;
            var massFactor = Mathf.Clamp(rb.mass / 30f, 0.8f, 2.2f);
            var desiredVelocity = Vector3.ClampMagnitude(toTarget * (11f / massFactor), 8.5f);
            rb.velocity = Vector3.Lerp(rb.velocity, desiredVelocity, Time.fixedDeltaTime * 13f);
            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 8f);

            var targetRot = Quaternion.LookRotation(PlayerCamera.transform.forward, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, Time.fixedDeltaTime * 8f));

            if (Vector3.Distance(transform.position, HeldCargo.transform.position) > 4.2f)
            {
                DropCargo(false);
            }
        }

        private void ToggleDrive()
        {
            if (IsDriving)
            {
                ExitVan();
                return;
            }

            var van = FindNearestVanDoor();
            if (van == null) return;
            EnterVan(van);
        }

        private VanController FindNearestVanDoor()
        {
            var vans = FindObjectsOfType<VanController>();
            VanController best = null;
            var bestDist = 999f;
            foreach (var v in vans)
            {
                var d = Vector3.Distance(transform.position, v.DriverDoorWorld);
                if (d < bestDist && d < 2.25f)
                {
                    best = v;
                    bestDist = d;
                }
            }
            return best;
        }

        private void EnterVan(VanController van)
        {
            if (HeldCargo != null) DropCargo(false);
            IsDriving = true;
            _currentVan = van;
            _savedVelocity = _rb.velocity;
            _rb.isKinematic = true;
            _collider.enabled = false;
            PlayerCamera.transform.SetParent(null, true);
            van.SetDriver(this);
        }

        private void ExitVan()
        {
            if (_currentVan == null) return;
            var exitPos = FindSafeExitPosition(_currentVan);
            _currentVan.SetDriver(null);
            transform.position = exitPos;
            transform.rotation = Quaternion.Euler(0, _currentVan.transform.eulerAngles.y - 90f, 0);
            PlayerCamera.transform.SetParent(transform, false);
            PlayerCamera.transform.localPosition = new Vector3(0, 1.65f, 0);
            PlayerCamera.transform.localRotation = Quaternion.identity;
            _rb.isKinematic = false;
            _collider.enabled = true;
            _rb.velocity = _savedVelocity * 0.2f;
            IsDriving = false;
            _currentVan = null;
        }

        private Vector3 FindSafeExitPosition(VanController van)
        {
            var candidates = new[] { van.DriverExitWorld, van.PassengerExitWorld, van.RearExitWorld };
            foreach (var candidate in candidates)
            {
                var pos = SnapToGround(candidate + Vector3.up * 1.8f);
                if (IsExitClear(pos)) return pos;
            }

            return SnapToGround(van.transform.position - van.transform.right * 3.2f + Vector3.up * 2f);
        }

        private Vector3 SnapToGround(Vector3 probe)
        {
            if (Physics.Raycast(probe, Vector3.down, out var hit, 6f))
                return hit.point + Vector3.up * (_collider.height * 0.5f + 0.08f);
            return probe;
        }

        private bool IsExitClear(Vector3 pos)
        {
            var bottom = pos + Vector3.up * (_collider.radius + 0.05f);
            var top = pos + Vector3.up * (_collider.height - _collider.radius);
            var hits = Physics.OverlapCapsule(bottom, top, _collider.radius * 0.92f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform == transform || hit.GetComponentInParent<FirstPersonPlayer>() == this) continue;
                if (hit.GetComponentInParent<VanController>() != null) continue;
                return false;
            }
            return true;
        }
    }
}
