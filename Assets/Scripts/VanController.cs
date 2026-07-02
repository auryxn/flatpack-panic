using UnityEngine;

namespace FlatpackPanic
{
    [RequireComponent(typeof(Rigidbody))]
    public class VanController : MonoBehaviour
    {
        [Header("Wheel Colliders")]
        public WheelCollider FrontLeftCollider;
        public WheelCollider FrontRightCollider;
        public WheelCollider RearLeftCollider;
        public WheelCollider RearRightCollider;

        [Header("Wheel Meshes")]
        public Transform FrontLeftMesh;
        public Transform FrontRightMesh;
        public Transform RearLeftMesh;
        public Transform RearRightMesh;

        [Header("Car Settings")]
        public float MotorForce = 1650f;
        public float BrakeForce = 3000f;
        public float MaxSteerAngle = 32f;
        public float MaxSpeedKmh = 70f;
        public AnimationCurve SteeringBySpeed = AnimationCurve.EaseInOut(0, 1, 70, 0.42f);

        [Header("Stability")]
        public Transform CenterOfMass;
        public float AntiRollForce = 6500f;
        public float Downforce = 42f;

        [Header("Driver camera")]
        public Vector3 DriverSeatLocal = new(-0.72f, 0.72f, 1.58f);
        public Vector3 DriverCameraLocal = new(-0.72f, 1.32f, 1.85f);
        public Vector3 DriverDoorLocal = new(-2.15f, 0.35f, 1.35f);
        public Vector3 DriverExitLocal = new(-3.15f, 0.35f, 1.15f);
        public Vector3 PassengerExitLocal = new(3.15f, 0.35f, 1.15f);
        public Vector3 RearExitLocal = new(0f, 0.35f, -4.35f);
        public Vector3 ThirdPersonCameraLocal = new(0f, 4.2f, -8.2f);
        public Vector3 ThirdPersonLookAtLocal = new(0f, 1.25f, 1.25f);
        public float LookYawLimit = 75f;
        public float LookPitchLimit = 45f;

        public FirstPersonPlayer Driver { get; private set; }
        public Vector3 DriverSeatWorld => transform.TransformPoint(DriverSeatLocal);
        public Vector3 DriverDoorWorld => transform.TransformPoint(DriverDoorLocal);
        public Vector3 DriverExitWorld => transform.TransformPoint(DriverExitLocal);
        public Vector3 PassengerExitWorld => transform.TransformPoint(PassengerExitLocal);
        public Vector3 RearExitWorld => transform.TransformPoint(RearExitLocal);
        public Vector3 DriverCameraPosition => transform.TransformPoint(DriverCameraLocal);
        public Quaternion DriverCameraRotation => transform.rotation * Quaternion.Euler(_lookPitch, _lookYaw, 0);
        public Vector3 ThirdPersonCameraPosition => transform.TransformPoint(ThirdPersonCameraLocal);
        public Quaternion ThirdPersonCameraRotation => Quaternion.LookRotation(transform.TransformPoint(ThirdPersonLookAtLocal) - ThirdPersonCameraPosition, Vector3.up);

        private Rigidbody _rb;
        private float _moveInput;
        private float _steerInput;
        private bool _brakeInput;
        private float _lookYaw;
        private float _lookPitch;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 1350f;
            _rb.drag = 0.05f;
            _rb.angularDrag = 1f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (CenterOfMass != null) _rb.centerOfMass = CenterOfMass.localPosition;
            else _rb.centerOfMass = new Vector3(0, -0.55f, -0.1f);

            SetupWheel(FrontLeftCollider);
            SetupWheel(FrontRightCollider);
            SetupWheel(RearLeftCollider);
            SetupWheel(RearRightCollider);
        }

        public void SetDriver(FirstPersonPlayer driver)
        {
            Driver = driver;
            _lookYaw = 0f;
            _lookPitch = 0f;
            _moveInput = 0f;
            _steerInput = 0f;
            _brakeInput = false;
        }

        public void SetLookInput(float yawDelta, float pitchDelta)
        {
            _lookYaw = Mathf.Clamp(_lookYaw + yawDelta, -LookYawLimit, LookYawLimit);
            _lookPitch = Mathf.Clamp(_lookPitch - pitchDelta, -LookPitchLimit, LookPitchLimit);
        }

        private void Update()
        {
            if (Driver != null)
            {
                _moveInput = Input.GetAxis("Vertical");
                _steerInput = Input.GetAxis("Horizontal");
                _brakeInput = Input.GetKey(KeyCode.Space);
            }
            else
            {
                _moveInput = 0f;
                _steerInput = 0f;
                _brakeInput = false;
            }

            UpdateWheelVisuals();
        }

        private void FixedUpdate()
        {
            Drive();
            Steer();
            Brake();
            ApplyStability();
            CargoJoltPenalty();
        }

        private void Drive()
        {
            var speedKmh = _rb.velocity.magnitude * 3.6f;
            var wantsForward = _moveInput > 0.05f;
            var wantsReverse = _moveInput < -0.05f;
            var forwardSpeed = Vector3.Dot(_rb.velocity, transform.forward);

            var torque = _moveInput * MotorForce;
            if (speedKmh > MaxSpeedKmh && wantsForward) torque = 0f;
            if (forwardSpeed < -8f && wantsReverse) torque = 0f;

            // Rear-wheel drive: stable, simple, good for a delivery van.
            RearLeftCollider.motorTorque = torque;
            RearRightCollider.motorTorque = torque;

            FrontLeftCollider.motorTorque = 0f;
            FrontRightCollider.motorTorque = 0f;
        }

        private void Steer()
        {
            var speedKmh = _rb.velocity.magnitude * 3.6f;
            var speedSteerFactor = SteeringBySpeed.Evaluate(speedKmh);
            var angle = _steerInput * MaxSteerAngle * speedSteerFactor;

            FrontLeftCollider.steerAngle = angle;
            FrontRightCollider.steerAngle = angle;
        }

        private void Brake()
        {
            var brakingBecauseReverse = Mathf.Abs(_moveInput) > 0.05f && Mathf.Sign(_moveInput) != Mathf.Sign(Vector3.Dot(_rb.velocity, transform.forward)) && _rb.velocity.magnitude > 2f;
            var brake = _brakeInput ? BrakeForce : brakingBecauseReverse ? BrakeForce : 0f;

            FrontLeftCollider.brakeTorque = brake;
            FrontRightCollider.brakeTorque = brake;
            RearLeftCollider.brakeTorque = brake;
            RearRightCollider.brakeTorque = brake;
        }

        private void ApplyStability()
        {
            _rb.AddForce(-transform.up * (_rb.velocity.magnitude * Downforce), ForceMode.Force);
            ApplyAntiRoll(FrontLeftCollider, FrontRightCollider);
            ApplyAntiRoll(RearLeftCollider, RearRightCollider);
        }

        private void ApplyAntiRoll(WheelCollider left, WheelCollider right)
        {
            var groundedLeft = left.GetGroundHit(out var hitLeft);
            var groundedRight = right.GetGroundHit(out var hitRight);
            var travelLeft = 1f;
            var travelRight = 1f;

            if (groundedLeft)
                travelLeft = (-left.transform.InverseTransformPoint(hitLeft.point).y - left.radius) / left.suspensionDistance;
            if (groundedRight)
                travelRight = (-right.transform.InverseTransformPoint(hitRight.point).y - right.radius) / right.suspensionDistance;

            var antiRoll = (travelLeft - travelRight) * AntiRollForce;
            if (groundedLeft) _rb.AddForceAtPosition(left.transform.up * -antiRoll, left.transform.position);
            if (groundedRight) _rb.AddForceAtPosition(right.transform.up * antiRoll, right.transform.position);
        }

        private void UpdateWheelVisuals()
        {
            UpdateWheel(FrontLeftCollider, FrontLeftMesh);
            UpdateWheel(FrontRightCollider, FrontRightMesh);
            UpdateWheel(RearLeftCollider, RearLeftMesh);
            UpdateWheel(RearRightCollider, RearRightMesh);
        }

        private static void UpdateWheel(WheelCollider collider, Transform mesh)
        {
            if (collider == null || mesh == null) return;
            collider.GetWorldPose(out var position, out var rotation);
            mesh.position = position;
            mesh.rotation = rotation;
        }

        private static void SetupWheel(WheelCollider wheel)
        {
            if (wheel == null) return;

            wheel.mass = 20f;
            wheel.radius = 0.38f;
            wheel.wheelDampingRate = 0.35f;
            wheel.suspensionDistance = 0.23f;
            wheel.forceAppPointDistance = 0f;

            var spring = wheel.suspensionSpring;
            spring.spring = 35000f;
            spring.damper = 4500f;
            spring.targetPosition = 0.5f;
            wheel.suspensionSpring = spring;

            var forward = wheel.forwardFriction;
            forward.extremumSlip = 0.38f;
            forward.extremumValue = 1.15f;
            forward.asymptoteSlip = 0.8f;
            forward.asymptoteValue = 0.75f;
            forward.stiffness = 1.2f;
            wheel.forwardFriction = forward;

            var sideways = wheel.sidewaysFriction;
            sideways.extremumSlip = 0.28f;
            sideways.extremumValue = 1.05f;
            sideways.asymptoteSlip = 0.65f;
            sideways.asymptoteValue = 0.72f;
            sideways.stiffness = 1.25f;
            wheel.sidewaysFriction = sideways;
        }

        private void CargoJoltPenalty()
        {
            if (FlatpackGame.Instance == null || Driver == null) return;
            if (_rb.velocity.magnitude < 7f) return;

            var harsh = Mathf.Abs(_steerInput) > 0.72f || (_brakeInput && _rb.velocity.magnitude > 9f) || Mathf.Abs(_moveInput) > 0.92f;
            if (!harsh) return;

            foreach (var cargo in FlatpackGame.Instance.Cargo)
            {
                if (Vector3.Distance(cargo.transform.position, transform.position) > 5.5f) continue;
                var rb = cargo.GetComponent<Rigidbody>();
                var jolt = transform.right * _steerInput * 1.5f + transform.forward * -_moveInput * 0.7f;
                rb.AddForce(jolt, ForceMode.Impulse);
            }
        }
    }
}
