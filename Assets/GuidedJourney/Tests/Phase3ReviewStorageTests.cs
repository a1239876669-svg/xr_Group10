using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Group10.GuidedJourney.Tests
{
    public sealed class Phase3ReviewStorageTests
    {
        private const BindingFlags StaticAny = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceAny = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Type adapter;
        private string key;
        private string previousSelection;
        private readonly List<string> ownedDirectories = new List<string>();
        private GameObject toolsObject;

        [SetUp]
        public void SetUp()
        {
            adapter = Type.GetType("Phase3ReviewTestStorage, Assembly-CSharp-Editor", true);
            key = (string)adapter.GetProperty("SessionKey", StaticAny).GetValue(null);
            previousSelection = SessionState.GetString(key, "");
            SessionState.EraseString(key);
        }

        [TearDown]
        public void TearDown()
        {
            if (toolsObject != null) Object.DestroyImmediate(toolsObject);
            if (key != null) SessionState.SetString(key, previousSelection ?? "");
            foreach (string owned in ownedDirectories)
            {
                string full = Path.GetFullPath(owned).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Assert.That(string.Equals(Path.GetDirectoryName(full), temp, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Path.GetFileName(full), Does.StartWith("Group10-Phase3Review-"));
                if (Directory.Exists(full))
                {
                    Assert.That((File.GetAttributes(full) & FileAttributes.ReparsePoint) == 0, Is.True);
                    Directory.Delete(full, true);
                }
            }
            ownedDirectories.Clear();
        }

        private object Invoke(string name, params object[] args)
        {
            return adapter.GetMethod(name, StaticAny).Invoke(null, args);
        }

        private bool TryPath(out string path, out string reason)
        {
            object[] args = { null, null };
            bool result = (bool)Invoke("TryGetDevelopmentPath", args);
            path = (string)args[0];
            reason = (string)args[1];
            return result;
        }

        private string SelectNew()
        {
            Invoke("UseNewDevelopmentFolder");
            string directory = SessionState.GetString(key, "");
            ownedDirectories.Add(directory);
            return directory;
        }

        [Test]
        public void NoSelectionDoesNotYieldAStoragePath()
        {
            Assert.That(TryPath(out string path, out string reason), Is.False);
            Assert.That(path, Is.Null);
            Assert.That(reason, Does.Contain("No isolated"));
        }

        [Test]
        public void OptInCreatesPrivateTempPathAndReusesItUntilSelectionChanges()
        {
            string directory = SelectNew();
            Assert.That(TryPath(out string first, out _), Is.True);
            Assert.That(TryPath(out string second, out _), Is.True);
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo(Path.Combine(directory, "current.json")));
            Assert.That(File.Exists(first), Is.False, "Selecting storage must not create or copy feedback records.");
        }

        [Test]
        public void OutsideTempSelectionIsRejectedWithoutCreatingFiles()
        {
            string outside = Path.Combine(Application.dataPath, "Group10-Phase3Review-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(key, outside);
            Assert.That(TryPath(out string path, out _), Is.False);
            Assert.That(path, Is.Null);
            Assert.That(Directory.Exists(outside), Is.False);
        }

        [Test]
        public void MissingPrivateTempDirectoryIsRejected()
        {
            string missing = Path.Combine(Path.GetTempPath(), "Group10-Phase3Review-" + Guid.NewGuid().ToString("N"));
            SessionState.SetString(key, missing);
            Assert.That(TryPath(out _, out _), Is.False);
            Assert.That(Directory.Exists(missing), Is.False);
        }

        [Test]
        public void ForgetSelectionPreservesTheDevelopmentFile()
        {
            string directory = SelectNew();
            string path = Path.Combine(directory, "keep.txt");
            byte[] bytes = { 4, 2, 8, 1 };
            File.WriteAllBytes(path, bytes);
            Invoke("ForgetFolder");
            Assert.That(TryPath(out _, out _), Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
        }

        [Test]
        public void NewSelectionPreservesThePreviouslySelectedDataset()
        {
            string first = SelectNew();
            string marker = Path.Combine(first, "keep.txt");
            File.WriteAllText(marker, "development evidence");
            string second = SelectNew();
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.ReadAllText(marker), Is.EqualTo("development evidence"));
            Assert.That(TryPath(out string path, out _), Is.True);
            Assert.That(path, Is.EqualTo(Path.Combine(second, "current.json")));
        }

        [Test]
        public void ExistingReviewToolsSeamAcceptsPrivateTempAndRejectsProjectPath()
        {
            string directory = SelectNew();
            string isolated = Path.Combine(directory, "current.json");
            Type type = Type.GetType("QuestGuidance.Review.ReviewTools, Assembly-CSharp", true);
            MethodInfo configure = type.GetMethod("ConfigureStorageForTests", InstanceAny);
            Assert.That(configure, Is.Not.Null);
            Assert.That(type.GetField("storageBlocked", InstanceAny), Is.Not.Null);
            toolsObject = new GameObject("Phase 3 isolated review seam test") { hideFlags = HideFlags.HideAndDontSave };
            Component tools = toolsObject.AddComponent(type);
            type.GetField("autoSave", InstanceAny).SetValue(tools, false);
            configure.Invoke(tools, new object[] { isolated });
            Assert.That(type.GetProperty("SavePath").GetValue(tools), Is.EqualTo(isolated));
            string invalid = Path.Combine(Application.dataPath, "DoNotCreatePhase3History.json");
            var error = Assert.Throws<TargetInvocationException>(() => configure.Invoke(tools, new object[] { invalid }));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(type.GetProperty("SavePath").GetValue(tools), Is.EqualTo(isolated));
            Assert.That(File.Exists(invalid), Is.False);
            Assert.That(File.Exists(isolated), Is.False);
        }
    }
}
