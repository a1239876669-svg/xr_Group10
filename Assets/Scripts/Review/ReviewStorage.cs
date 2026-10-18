using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace QuestGuidance.Review
{
    public static class ReviewStorage
    {
        public static string SceneKey(string scene)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(scene ?? ""))).Replace("-", "").Substring(0, 16);
        }

        public static void Save(string path, ReviewSession session)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(session, true), Encoding.UTF8);
            // Replacement keeps the last complete file as backup. Failure leaves the original intact.
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }

        public static ReviewSession Load(string path, string scene, out bool recovered)
        {
            recovered = false;
            try { return Parse(File.ReadAllText(path), scene); }
            catch (Exception)
            {
                if (!File.Exists(path + ".bak")) throw;
                var session = Parse(File.ReadAllText(path + ".bak"), scene);
                if (File.Exists(path)) File.Move(path, path + ".damaged-" + Guid.NewGuid().ToString("N"));
                recovered = true;
                return session;
            }
        }

        public static ReviewSession Parse(string json, string scene)
        {
            var session = JsonUtility.FromJson<ReviewSession>(json);
            if (session == null || session.version != 2 || string.IsNullOrEmpty(session.sessionId)
                || session.scenePath != scene || session.records == null)
                throw new InvalidDataException("Unsupported or mismatched review session.");
            var ids = new HashSet<string>();
            var numbers = new HashSet<int>();
            int highest = 0;
            foreach (var record in session.records)
            {
                if (record == null || string.IsNullOrEmpty(record.id) || !ids.Add(record.id)
                    || record.number < 1 || !numbers.Add(record.number) || record.scene != scene
                    || (record.kind != "marker" && record.kind != "measurement")
                    || !Finite(record.point) || !Finite(record.endPoint)
                    || !Enum.IsDefined(typeof(FeedbackState), record.state)
                    || !Enum.IsDefined(typeof(IssueImportance), record.importance))
                    throw new InvalidDataException("Invalid review record.");
                if (record.kind == "measurement")
                {
                    float distance = Vector3.Distance(record.point, record.endPoint);
                    if (float.IsNaN(record.distanceMetres) || float.IsInfinity(record.distanceMetres)
                        || distance < 0.001f || Mathf.Abs(record.distanceMetres - distance) > 0.001f)
                        throw new InvalidDataException("Invalid measurement.");
                }
                else if (record.state == FeedbackState.Submitted &&
                    (string.IsNullOrWhiteSpace(record.observation) || string.IsNullOrWhiteSpace(record.everydayImpact)))
                    throw new InvalidDataException("Submitted feedback is empty.");
                highest = Math.Max(highest, record.number);
            }
            foreach (var record in session.records)
                if (!string.IsNullOrEmpty(record.measurementId) && !session.records.Exists(
                    item => item.id == record.measurementId && item.kind == "measurement"))
                    throw new InvalidDataException("Missing linked measurement.");
            session.nextNumber = Math.Max(session.nextNumber, highest + 1);
            return session;
        }

        private static bool Finite(Vector3 point)
        {
            return !float.IsNaN(point.x) && !float.IsInfinity(point.x)
                && !float.IsNaN(point.y) && !float.IsInfinity(point.y)
                && !float.IsNaN(point.z) && !float.IsInfinity(point.z);
        }
    }
}
