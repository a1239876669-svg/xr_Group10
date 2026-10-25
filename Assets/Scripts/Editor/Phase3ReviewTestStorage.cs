#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using QuestGuidance.Review;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Explicit, Editor-session-only storage isolation for the formal Phase 3 scene.
/// No scene asset, Player code, schema or historical data is changed.
/// </summary>
[InitializeOnLoad]
public static class Phase3ReviewTestStorage
{
    private const string FormalScene = "Assets/Scenes/Apartment_Detailed_VR.unity";
    private const string DirectoryPrefix = "Group10-Phase3Review-";
    private const string Menu = "Apartment Phase 3/Review Test Storage/";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo ConfigureStorage = typeof(ReviewTools).GetMethod(
        "ConfigureStorageForTests", PrivateInstance, null, new[] { typeof(string) }, null);
    private static readonly FieldInfo StorageBlocked = typeof(ReviewTools).GetField("storageBlocked", PrivateInstance);
    private static readonly HashSet<ReviewTools> Configured = new HashSet<ReviewTools>();

    private static string SessionKey => "Group10.Phase3.IsolatedReview." + Application.dataPath;

    static Phase3ReviewTestStorage()
    {
        RegisterSceneCallback();
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReload;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReload;
    }

    private static void BeforeAssemblyReload()
    {
        if (!Application.isPlaying) return;
        Scene scene = SceneManager.GetSceneByPath(FormalScene);
        if (!scene.IsValid() || !scene.isLoaded) return;
        var tools = new List<ReviewTools>();
        foreach (GameObject root in scene.GetRootGameObjects())
            tools.AddRange(root.GetComponentsInChildren<ReviewTools>(true));
        StopLoadedScene(tools, "Scripts are reloading during a development Play run. "
            + "The non-serialized storage override must not be lost during live reload; start a new Play after compilation");
    }

    [MenuItem(Menu + "Use New Isolated Development Folder")]
    public static void UseNewDevelopmentFolder()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Select development storage before entering Play.");
        string directory = Path.Combine(Path.GetTempPath(), DirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SessionState.SetString(SessionKey, Path.GetFullPath(directory));
        Debug.Log("Phase 3 development review storage selected for this Editor session: " + directory
            + ". Repeated Play runs reuse this folder. Historical feedback is not copied or modified.");
    }

    [MenuItem(Menu + "Use New Isolated Development Folder", true)]
    private static bool CanChooseFolder() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(Menu + "Show Current Development Folder")]
    public static void ShowCurrentFolder()
    {
        if (TryGetDevelopmentPath(out string path, out string reason))
            Debug.Log("Phase 3 development review file: " + path);
        else
            Debug.LogWarning("Phase 3 development storage is not selected: " + reason + ". Use " + Menu
                + "Use New Isolated Development Folder before Play.");
    }

    [MenuItem(Menu + "Forget Folder Without Deleting Data")]
    public static void ForgetFolder()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play before changing development storage.");
        SessionState.EraseString(SessionKey);
        Debug.Log("Phase 3 development storage selection cleared. No file was deleted.");
    }

    [MenuItem(Menu + "Forget Folder Without Deleting Data", true)]
    private static bool CanForgetFolder() => !EditorApplication.isPlayingOrWillChangePlaymode;

    // Unity documents that this runs before Awake in Editor Play as well as Player startup.
    // This class lives under Editor and is excluded from Player builds.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeforeSceneLoad()
    {
        Configured.Clear();
        RegisterSceneCallback();
    }

    private static void RegisterSceneCallback()
    {
        // The InitializeOnLoad subscription is also present after an assembly reload.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            Configured.Clear();
            return;
        }
        if (state != PlayModeStateChange.ExitingEditMode || !FormalSceneWillRun()) return;

        bool selected = TryGetDevelopmentPath(out _, out string reason);
        if (selected && EditorSettings.enterPlayModeOptionsEnabled
            && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
        {
            StopBeforePlay("Scene Reload is disabled. This isolation adapter requires fresh scene objects. "
                + "Do not change protected ProjectSettings without approval.");
            return;
        }
        if (selected && (ConfigureStorage == null || StorageBlocked == null))
        {
            StopBeforePlay("The existing ReviewTools test-storage seam or safety field could not be resolved.");
            return;
        }
        if (!selected && HistoricalDirectoryExists())
            StopBeforePlay(reason + ". Historical ReviewSessions exists. Select " + Menu
                + "Use New Isolated Development Folder, then enter Play.");
    }

    private static bool FormalSceneWillRun()
    {
        if (EditorSceneManager.playModeStartScene != null
            && AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == FormalScene) return true;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).path == FormalScene) return true;
        return false;
    }

    // sceneLoaded is after Awake/OnEnable but before Start, where ReviewTools restores data.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Application.isPlaying || scene.path != FormalScene) return;
        var tools = new List<ReviewTools>();
        foreach (GameObject root in scene.GetRootGameObjects())
            tools.AddRange(root.GetComponentsInChildren<ReviewTools>(true));

        bool selected = TryGetDevelopmentPath(out string path, out string reason);
        if (!selected)
        {
            if (HistoricalDirectoryExists()) StopLoadedScene(tools, reason);
            return;
        }
        if (EditorSettings.enterPlayModeOptionsEnabled
            && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableSceneReload) != 0)
        {
            StopLoadedScene(tools, "Scene Reload is disabled; before-Start isolation cannot be guaranteed.");
            return;
        }
        try
        {
            if (ConfigureStorage == null || StorageBlocked == null)
                throw new MissingMethodException("ReviewTools test storage contract has changed.");
            if (tools.Count == 0)
                throw new InvalidOperationException("The formal scene has no ReviewTools component.");
            foreach (ReviewTools tool in tools)
            {
                if (Configured.Contains(tool)) continue;
                ConfigureStorage.Invoke(tool, new object[] { path });
                if (!string.Equals(Path.GetFullPath(tool.SavePath), path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("ReviewTools did not accept the isolated path.");
                Configured.Add(tool);
                Debug.Log("Phase 3 ReviewTools isolated before Start: " + path, tool);
            }
        }
        catch (Exception error)
        {
            var invocation = error as TargetInvocationException;
            StopLoadedScene(tools, (invocation?.InnerException ?? error).Message);
        }
    }

    private static bool HistoricalDirectoryExists()
    {
        return Directory.Exists(Path.Combine(Application.persistentDataPath, "ReviewSessions"));
    }

    private static bool TryGetDevelopmentPath(out string path, out string reason)
    {
        path = null;
        reason = "No isolated development folder selected in this Editor session";
        string selected = SessionState.GetString(SessionKey, "");
        if (string.IsNullOrEmpty(selected)) return false;
        try
        {
            string directory = Path.GetFullPath(selected).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetDirectoryName(directory);
            string name = Path.GetFileName(directory);
            if (!string.Equals(parent, temporaryRoot, StringComparison.OrdinalIgnoreCase)
                || !name.StartsWith(DirectoryPrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(name.Substring(DirectoryPrefix.Length), "N", out _)
                || !Directory.Exists(directory))
            {
                reason = "Selected development folder is missing or is not a private direct child of the system temp folder";
                return false;
            }
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                reason = "Development folder must not be a junction or symbolic link";
                return false;
            }
            path = Path.Combine(directory, "current.json");
            return true;
        }
        catch (Exception error)
        {
            reason = "Development storage validation failed: " + error.Message;
            return false;
        }
    }

    private static void StopBeforePlay(string reason)
    {
        Debug.LogError("Phase 3 Play cancelled to protect historical review data. " + reason);
        EditorApplication.isPlaying = false;
    }

    private static void StopLoadedScene(List<ReviewTools> tools, string reason)
    {
        // Fail closed if the formal scene is loaded later (e.g. additively) or setup fails.
        // This changes runtime objects only; no scene dirty/save or historical file access.
        foreach (ReviewTools tool in tools)
        {
            if (StorageBlocked != null) StorageBlocked.SetValue(tool, true);
            tool.enabled = false;
        }
        Debug.LogError("Phase 3 Play stopped before ReviewTools.Start to protect historical data. " + reason
            + ". Select " + Menu + "Use New Isolated Development Folder while not playing.");
        EditorApplication.isPlaying = false;
    }
}
#endif
