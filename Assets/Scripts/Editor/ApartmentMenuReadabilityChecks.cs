using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ApartmentMenuReadabilityChecks
{
    const string Output = "Logs/WelcomeMenuQA/";
    public static void Verify()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch Editor.");
        EditorSceneManager.OpenScene("Assets/Scenes/Apartment_Detailed_VR.unity");
        var tutorial = Object.FindFirstObjectByType<ApartmentTutorialController>();
        var menu = tutorial.menu;
        Directory.CreateDirectory(Output);
        typeof(ApartmentTutorialController).GetProperty("Session").SetValue(tutorial, new ApartmentTutorialProgress(true));
        typeof(ApartmentTutorialController).GetProperty("InputReady").SetValue(tutorial, true);
        // Regular MonoBehaviour lifecycle does not run when opening an EditMode scene.
        tutorial.SendMessage("OnEnable");
        var welcome = tutorial.BuildWelcomePreview();
        foreach (bool chinese in new[] { false, true, false })
        {
            tutorial.ProcessPointer(Aim(welcome, chinese ? "Welcome Chinese" : "Welcome English"), 2, true, out _);
            Require(tutorial.Session.Welcome && ApartmentLanguage.IsChinese == chinese, "Language choice advanced the tutorial or did not switch.");
            Require(welcome.Find("Welcome title").GetComponent<TMP_Text>().text ==
                ApartmentLanguage.Display(ApartmentTutorialController.WelcomeTitle), "Stale welcome title");
            Capture(welcome, chinese ? "Welcome-ZH" : "Welcome-EN");
        }
        tutorial.ProcessPointer(Aim(welcome, "Start tutorial"), 2, true, out _);
        Require(tutorial.Session.Step == -1, "Start did not enter tutorial.");
        typeof(ApartmentMenuController).GetProperty("InputReady").SetValue(menu, true);
        foreach (bool chinese in new[] { false, true })
        {
            ApartmentLanguage.SetLanguage(chinese ? ApartmentDisplayLanguage.SimplifiedChinese : ApartmentDisplayLanguage.English);
            menu.BuildPreview(2);
            var root = menu.PanelRect;
            string prefix = chinese ? "Controls-ZH-" : "Controls-EN-";
            Capture(root, prefix + "1");
            var body = root.Find("Controls").GetComponent<TMP_Text>();
            Require(body.textInfo.pageCount >= 2 && root.Find("Controls next"), "Missing page controls");
            int pages = body.textInfo.pageCount;
            for (int p = 2; p <= pages; p++)
            {
                menu.ProcessPointer(Aim(root, "Controls next"), 2, true, out _);
                Require(body.pageToDisplay == p, "Next page failed.");
                Capture(root, prefix + p);
            }
            menu.ProcessPointer(Aim(root, "Controls previous"), 2, true, out _);
            Require(body.pageToDisplay == pages - 1, "Previous page failed.");
            menu.ProcessPointer(Aim(root, "Review guide"), 2, true, out _);
            menu.ProcessPointer(Aim(root, "Recovery help"), 2, true, out _);
            Capture(root, chinese ? "Recovery-ZH" : "Recovery-EN");
            Require(root.Find("Help text"), "Recovery text missing.");
            menu.ProcessPointer(Aim(root, "Back guide"), 2, true, out _);
            menu.ProcessPointer(Aim(root, "Back controls"), 2, true, out _);
        }
        Debug.Log("Verified bilingual welcome selection, tutorial start and two-way controls paging.");
    }

    static Ray Aim(RectTransform root, string button)
    {
        var rect = (RectTransform)root.Find(button);
        var point = rect.TransformPoint(rect.rect.center);
        return new Ray(point - rect.forward, rect.forward);
    }

    static void Capture(RectTransform root, string name)
    {
        root.SetPositionAndRotation(new Vector3(1000,1000,1002), Quaternion.identity);
        foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
        var camera = new GameObject("Menu inspection camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(1000,1000,1000);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(root.sizeDelta.y,root.sizeDelta.x / 1.5f) * .00054f;
        camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.10f,.13f,.17f);
        var canvas = root.GetComponent<Canvas>();
        canvas.worldCamera = camera;
        var rt = new RenderTexture(1350,900,24);
        var image = new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Require(!label.isTextTruncated, name + " truncated: " + label.name);
                var bounds = label.rectTransform.rect;
                for (int i=0; i<label.textInfo.characterCount; i++)
                {
                    var c = label.textInfo.characterInfo[i];
                    if (!c.isVisible || (label.overflowMode == TextOverflowModes.Page && c.pageNumber+1 != label.pageToDisplay)) continue;
                    Require(c.bottomLeft.x >= bounds.xMin-2 && c.topRight.x <= bounds.xMax+2,
                        name + " glyph beyond row: " + label.name);
                }
            }
            camera.Render();
            RenderTexture.active = rt;
            image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);
            image.Apply();
            File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
    static void Require(bool ok,string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
