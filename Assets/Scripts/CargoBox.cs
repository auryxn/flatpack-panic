using UnityEngine;

namespace FlatpackPanic
{
    [RequireComponent(typeof(Rigidbody))]
    public class CargoBox : MonoBehaviour
    {
        public string Label = "Cursed flatpack";
        public float Damage { get; private set; }
        public FirstPersonPlayer HeldByFirstPerson;
        public bool StoredInVan { get; private set; }
        public float DefaultDrag { get; private set; }
        public float DefaultAngularDrag { get; private set; }

        private Renderer _renderer;
        private Color _baseColor;
        private float _highlightUntil;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _baseColor = _renderer.material.color;
            var rb = GetComponent<Rigidbody>();
            DefaultDrag = rb.drag;
            DefaultAngularDrag = rb.angularDrag;
        }

        private void Update()
        {
            if (_renderer == null) return;
            if (Time.time < _highlightUntil)
            {
                _renderer.material.color = Color.Lerp(_renderer.material.color, new Color(1f, .88f, .15f), Time.deltaTime * 12f);
            }
            else
            {
                var t = Mathf.Clamp01(Damage / 32f);
                _renderer.material.color = Color.Lerp(_baseColor, new Color(0.9f, 0.1f, 0.06f), t);
            }
        }

        public void HighlightForPickup()
        {
            if (StoredInVan) return;
            _highlightUntil = Time.time + 0.08f;
        }

        public void StoreInVan()
        {
            StoredInVan = true;
            HeldByFirstPerson = null;
            var rb = GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            gameObject.SetActive(false);
        }

        public void RemoveFromVan(Vector3 position, Quaternion rotation)
        {
            StoredInVan = false;
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(position, rotation);
            var rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.drag = DefaultDrag;
            rb.angularDrag = DefaultAngularDrag;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var impact = collision.relativeVelocity.magnitude;
            if (impact < 2.8f) return;
            var fragileBonus = Label.ToLowerInvariant().Contains("mirror") ? 2.2f : 1f;
            Damage = Mathf.Clamp(Damage + impact * 0.075f * fragileBonus, 0f, 100f);
            // Color is refreshed in Update so pickup highlight can temporarily override damage tint.
        }

        public void ResetDamage()
        {
            Damage = 0f;
            if (_renderer != null) _renderer.material.color = _baseColor;
        }
    }
}
