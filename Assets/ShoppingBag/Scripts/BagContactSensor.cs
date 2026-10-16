using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace XRGroup10.ShoppingBag
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class BagContactSensor : MonoBehaviour
    {
        [SerializeField] private ShoppingBag bag;
        [SerializeField] private BoxCollider contactTrigger;
        [SerializeField] private LayerMask furnitureLayers = ~0;
        private Collider[] buffer = new Collider[64];
        private Dictionary<BagContactTarget, int> contacts = new Dictionary<BagContactTarget, int>();
        private Dictionary<BagContactTarget, int> scratch = new Dictionary<BagContactTarget, int>();
        private bool focused = true, paused;
        public ShoppingBag Bag => bag;
        public BoxCollider ContactTrigger => contactTrigger;
        public bool IsContactActive => isActiveAndEnabled && bag != null && bag.isActiveAndEnabled
            && bag.IsHeld && bag.HeldHand.HasValue && focused && !paused;
        public int ContactCount => contacts.Count;
        public event Action<BagContactTarget> ContactStarted;
        public event Action<BagContactTarget> ContactEnded;
        public event Action ContactsCleared;
        private void Awake()
        {
            if (contactTrigger == null) contactTrigger = GetComponent<BoxCollider>();
            if (bag == null) bag = GetComponentInParent<ShoppingBag>();
            SyncDimensions();
        }
        private void OnEnable()
        {
            if (bag == null) bag = GetComponentInParent<ShoppingBag>();
            if (contactTrigger == null) contactTrigger = GetComponent<BoxCollider>();
            if (bag != null) { bag.SizeChanged -= OnSize; bag.HoldStateChanged -= OnHold; bag.SizeChanged += OnSize; bag.HoldStateChanged += OnHold; }
            SyncDimensions();
        }
        private void OnDisable()
        {
            if (bag != null) { bag.SizeChanged -= OnSize; bag.HoldStateChanged -= OnHold; }
            ClearContacts(); if (contactTrigger != null) contactTrigger.enabled = false;
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value) ClearContacts(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) ClearContacts(); }
        private void OnSize(BagSize _) { ClearContacts(); SyncDimensions(); }
        private void OnHold(ShoppingBag _) { if (!IsContactActive) ClearContacts(); SyncDimensions(); }
        public void SyncDimensions()
        {
            if (bag == null || contactTrigger == null || bag.BodyCollider == null) return;
            contactTrigger.isTrigger = true;
            contactTrigger.center = bag.BodyCollider.center;
            contactTrigger.size = bag.BodyCollider.size;
            contactTrigger.enabled = IsContactActive;
        }
        private void FixedUpdate() => ScanNow();
        // Query the actual oriented body volume as well as owning a dedicated Trigger. This also
        // detects colliders disabled/destroyed mid-contact and bags spawned already overlapping.
        public void ScanNow()
        {
            SyncDimensions();
            if (!IsContactActive || contactTrigger == null) { ClearContacts(); return; }
            var t = contactTrigger.transform;
            var scale = t.lossyScale;
            var half = Vector3.Scale(contactTrigger.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var center = t.TransformPoint(contactTrigger.center);
            int count;
            while (true)
            {
                count = gameObject.scene.GetPhysicsScene().OverlapBox(center, half, buffer, t.rotation, furnitureLayers, QueryTriggerInteraction.Ignore);
                if (count < buffer.Length) break;
                // Do not silently drop contacts if a dense scene fills the initial query buffer.
                Array.Resize(ref buffer, buffer.Length * 2);
            }
            // Two reusable maps avoid per-physics-tick collections/garbage on Quest.
            var next = scratch; next.Clear();
            for (int i=0; i<count; i++)
            {
                var c = buffer[i]; buffer[i] = null;
                if (c == null || c.transform.IsChildOf(bag.transform) || !BagContactTarget.TryResolve(c, out var target)) continue;
                next.TryGetValue(target, out int colliders); next[target] = colliders + 1;
            }
            var previous = contacts; contacts = next; scratch = previous;
            foreach (var pair in previous)
                if (!next.ContainsKey(pair.Key)) { if (pair.Key != null) pair.Key.SetContact(this, false); ContactEnded?.Invoke(pair.Key); }
            foreach (var pair in next)
                if (!previous.ContainsKey(pair.Key)) { pair.Key.SetContact(this, true); ContactStarted?.Invoke(pair.Key); }
        }
        public int ColliderCount(BagContactTarget target) => target != null && contacts.TryGetValue(target, out int count) ? count : 0;
        public void ClearContacts()
        {
            foreach (var pair in contacts) { if (pair.Key != null) pair.Key.SetContact(this, false); ContactEnded?.Invoke(pair.Key); }
            contacts.Clear();
            ContactsCleared?.Invoke();
        }
        private void OnDrawGizmosSelected()
        {
            var box = contactTrigger != null ? contactTrigger : GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = IsContactActive ? Color.red : Color.yellow;
            var old = Gizmos.matrix; Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size); Gizmos.matrix = old;
        }
    }
}
