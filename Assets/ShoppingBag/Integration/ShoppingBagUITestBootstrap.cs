using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor integration fixture only. Loads the unchanged UI scene once, then binds an exact path.
[DisallowMultipleComponent]
public sealed class ShoppingBagUITestBootstrap : MonoBehaviour
{
    public ApartmentShoppingBagBridge bridge;
    public Transform fixtureRoot;
    public string uiScenePath = "Assets/Scenes/Apartment_Detailed_VR.unity";
    public string menuPath = "VRPlayer/ApartmentMenu";
    void Start()
    {
        try
        {
            if (bridge == null || fixtureRoot == null || bridge.bag == null)
                throw new InvalidOperationException("Integration fixture references are incomplete.");
            var scene = SceneManager.GetSceneByPath(uiScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
#if UNITY_EDITOR
                scene = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(uiScenePath,
                    new LoadSceneParameters(LoadSceneMode.Additive));
#else
                throw new InvalidOperationException("Editor test fixture: final Player scene wiring is pending.");
#endif
            }
            var segments = menuPath.Split('/'); Transform target = null;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == segments[0])
                {
                    if (target != null) throw new InvalidOperationException("Duplicate UI root: " + segments[0]);
                    target = root.transform;
                }
            for (int i = 1; target != null && i < segments.Length; i++) target = target.Find(segments[i]);
            var owner = target != null ? target.GetComponent<ApartmentMenuController>() : null;
            if (owner == null || owner.locomotion == null) throw new InvalidOperationException("UI menu path is not configured: " + menuPath);
            // Content only: no additional Camera, Rig or input owner is instantiated here.
            fixtureRoot.SetPositionAndRotation(owner.locomotion.transform.position,
                Quaternion.Euler(0, owner.locomotion.transform.eulerAngles.y, 0));
            bridge.Configure(owner, bridge.bag);
            SceneManager.SetActiveScene(scene);
        }
        catch (Exception e)
        {
            Debug.LogError("ShoppingBag UI fixture stopped safely: " + e.Message, this);
            enabled = false; // The already-bound Gate continues failing closed through the incomplete Bridge.
        }
    }
}