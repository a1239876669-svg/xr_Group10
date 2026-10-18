using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace XRGroup10.ShoppingBag
{
    // Only these explicitly listed solid colliders can participate in shopping-bag warnings.
    [DisallowMultipleComponent]
    public sealed class BagContactTarget : MonoBehaviour
    {
        [SerializeField] private Collider[] furnitureColliders = new Collider[0];
        [SerializeField] private Renderer[] furnitureRenderers = new Renderer[0];
        [SerializeField] private Material warningMaterial;
        private static readonly Dictionary<Collider, BagContactTarget> Registry = new Dictionary<Collider, BagContactTarget>();
        private readonly HashSet<BagContactSensor> owners = new HashSet<BagContactSensor>();
        private readonly List<Renderer> overlays = new List<Renderer>();
        private readonly List<Renderer> sources = new List<Renderer>();
        public bool WarningActive => owners.Count > 0;
        public int WarningSourceCount => owners.Count;
        public IReadOnlyList<Renderer> OverlayRenderers => overlays;

        public void Configure(Collider[] colliders, Renderer[] renderers, Material material)
        {
            Unregister(); ClearWarnings(); DestroyOverlays();
            furnitureColliders = colliders ?? new Collider[0];
            furnitureRenderers = renderers ?? new Renderer[0]; warningMaterial = material;
            if (isActiveAndEnabled) Register();
        }
        private void OnEnable() => Register();
        private void OnDisable() { Unregister(); ClearWarnings(); DestroyOverlays(); }
        private void OnDestroy() { Unregister(); DestroyOverlays(); }
        private void Register()
        {
            foreach (var collider in furnitureColliders)
            {
                if (collider == null || collider.isTrigger || !collider.transform.IsChildOf(transform)) continue;
                if (Registry.TryGetValue(collider, out var previous) && previous != this && previous != null)
                { Debug.LogError("BagContactTarget: collider already registered by another furniture target.", collider); continue; }
                Registry[collider] = this;
            }
        }
        private void Unregister()
        {
            foreach (var collider in furnitureColliders)
                if (collider != null && Registry.TryGetValue(collider, out var target) && target == this) Registry.Remove(collider);
        }
        public static bool TryResolve(Collider collider, out BagContactTarget target)
        {
            target = null;
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
                && Registry.TryGetValue(collider, out target) && target != null && target.isActiveAndEnabled;
        }
        public void SetContact(BagContactSensor source, bool active)
        {
            if (source == null) return;
            if (active && isActiveAndEnabled) owners.Add(source); else owners.Remove(source);
            if (WarningActive && overlays.Count == 0) CreateOverlays();
            RefreshOverlays();
        }
        private void ClearWarnings() { owners.Clear(); RefreshOverlays(); }
        private void LateUpdate()
        {
            owners.RemoveWhere(x => x == null || !x.isActiveAndEnabled || !x.IsContactActive);
            RefreshOverlays();
        }
        private void CreateOverlays()
        {
            if (warningMaterial == null) { Debug.LogError("BagContactTarget: assign the shopping-bag warning material.", this); return; }
            var unique = new HashSet<Renderer>();
            foreach (var source in furnitureRenderers)
            {
                if (source == null || !source.transform.IsChildOf(transform) || !unique.Add(source)) continue;
                Mesh mesh = null;
                if (source is SkinnedMeshRenderer skin) mesh = skin.sharedMesh;
                else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null) continue;
                var child = new GameObject("Bag Contact Red Overlay") { hideFlags = HideFlags.DontSave, layer = 2 };
                child.transform.SetParent(source.transform, false);
                Renderer overlay;
                if (source is SkinnedMeshRenderer skinned)
                {
                    var copy = child.AddComponent<SkinnedMeshRenderer>(); copy.sharedMesh = mesh;
                    copy.bones = skinned.bones; copy.rootBone = skinned.rootBone; copy.localBounds = skinned.localBounds;
                    overlay = copy;
                }
                else { child.AddComponent<MeshFilter>().sharedMesh = mesh; overlay = child.AddComponent<MeshRenderer>(); }
                var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                for (int i=0; i<materials.Length; i++) materials[i] = warningMaterial;
                overlay.sharedMaterials = materials;
                overlay.shadowCastingMode = ShadowCastingMode.Off; overlay.receiveShadows = false;
                overlay.lightProbeUsage = LightProbeUsage.Off; overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
                overlays.Add(overlay); sources.Add(source);
            }
        }
        private void RefreshOverlays()
        {
            for (int i=0; i<overlays.Count; i++)
                if (overlays[i] != null) overlays[i].enabled = WarningActive && sources[i] != null
                    && sources[i].enabled && sources[i].gameObject.activeInHierarchy && !sources[i].forceRenderingOff;
        }
        private void DestroyOverlays()
        {
            foreach (var overlay in overlays)
                if (overlay != null) { if (Application.isPlaying) Destroy(overlay.gameObject); else DestroyImmediate(overlay.gameObject); }
            overlays.Clear(); sources.Clear();
        }
    }
}
