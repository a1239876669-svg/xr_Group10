using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

namespace QuestGuidance.Review
{
    public enum ReviewMode { None, Measure, Mark }
    public enum IssueImportance { Low, Medium, High }
    public enum FeedbackState { Draft, Submitted }

    [Serializable]
    public class ReviewRecord
    {
        public string id, kind, scene, roomId, objectPath, timestampUtc;
        public Vector3 point, endPoint;
        public float distanceMetres;
        public string observation = "", everydayImpact = "";
        public IssueImportance importance = IssueImportance.Medium;
        public FeedbackState state;
        public int number;
        public string measurementId = "", updatedUtc = "";
    }

    [Serializable]
    public class ReviewSession
    {
        public int version = 2;
        public int nextNumber = 1;
        public string scenePath;
        public string sessionId = Guid.NewGuid().ToString("N");
        public List<ReviewRecord> records = new List<ReviewRecord>();
    }

    /// <summary>Menu-independent measurement and location feedback API. One Unity unit = one metre.</summary>
    public sealed class ReviewTools : MonoBehaviour
    {
        [SerializeField] private Material visualMaterial;
        [SerializeField] private ReviewMode mode;
        [SerializeField] private string currentRoomId = "Unassigned";
        [SerializeField] private bool autoSave = true;
        [SerializeField] private ReviewRoomMap roomMap;
        public UnityEvent onRecordsChanged = new UnityEvent();
        public UnityEvent onModeChanged = new UnityEvent();
        public event Action<ReviewRecord> RecordCreated;
        public event Action<ReviewRecord> MarkerSelected;
        /// <summary>Raised only after a new feedback submission is successfully written to the current session file.</summary>
        public event Action<ReviewRecord> FeedbackSaved;
        public long SuccessfulSaveRevision { get; private set; }
        public long FeedbackSaveRevision { get; private set; }
        public string LastSavedFeedbackMarkerId { get; private set; }
        public UnityEvent onStorageChanged = new UnityEvent();
        public ReviewMode Mode => mode;
        public bool HasPendingPoint => pending;
        public Vector3 PendingPoint => firstPoint;
        public IReadOnlyList<ReviewRecord> Records => recordView ?? (recordView = session.records.AsReadOnly());
        public string LastExportPath { get; private set; }
        public string LastError { get; private set; }
        public string StorageStatus { get; private set; } = "Not saved";
        public string SelectedId { get; private set; }
        public bool IsStorageBlocked => storageBlocked;
        public string SavePath => Path.Combine(Application.persistentDataPath, "ReviewSessions", ReviewStorage.SceneKey(gameObject.scene.path) + "_current.json");

        private ReviewSession session = new ReviewSession();
        private IReadOnlyList<ReviewRecord> recordView;
        private readonly List<GameObject> visuals = new List<GameObject>();
        private bool pending;
        private Vector3 firstPoint;
        private GameObject pendingVisual;
        private LineRenderer preview;
        private TextMesh previewLabel;
        private bool storageBlocked;
        // Runtime-only acknowledgement: restored records and measurement autosaves are not new feedback submissions.
        private string pendingFeedbackSaveId;

        private void Start()
        {
            session.scenePath = gameObject.scene.path;
            if (autoSave) RestoreSession();
        }

        private void Changed()
        {
            if (Application.isPlaying && autoSave && !storageBlocked) SaveSession();
            onRecordsChanged.Invoke();
        }

        public bool SaveSession()
        {
            if (storageBlocked) return false;
            try
            {
                session.scenePath = gameObject.scene.path;
                ReviewStorage.Save(SavePath, session);
            }
            catch (Exception error) { return StorageFailure(error.Message); }

            SuccessfulSaveRevision++;
            LastError = null;
            StorageStatus = "Saved locally " + DateTime.Now.ToString("HH:mm:ss");
            var savedFeedback = FindRecord(pendingFeedbackSaveId);
            pendingFeedbackSaveId = null;
            if (savedFeedback != null && savedFeedback.kind == "marker" && savedFeedback.state == FeedbackState.Submitted)
            {
                FeedbackSaveRevision++;
                LastSavedFeedbackMarkerId = savedFeedback.id;
            }
            onStorageChanged.Invoke();
            if (savedFeedback != null && savedFeedback.kind == "marker" && savedFeedback.state == FeedbackState.Submitted)
                FeedbackSaved?.Invoke(savedFeedback);
            return true;
        }

        public bool RestoreSession()
        {
            if (!File.Exists(SavePath) && !File.Exists(SavePath + ".bak")) return false;
            try
            {
                bool recovered;
                var restored = ReviewStorage.Load(SavePath, gameObject.scene.path, out recovered);
                // Validate completely before replacing the current session or its visuals.
                CancelPending();
                foreach (var visual in visuals) RemoveVisual(visual);
                visuals.Clear();
                session = restored;
                pendingFeedbackSaveId = null;
                recordView = null;
                SelectedId = null;
                foreach (var record in session.records) visuals.Add(CreateVisual(record, record.number));
                storageBlocked = false;
                LastError = null;
                StorageStatus = recovered ? "Recovered backup; damaged file preserved" : "Restored local session";
                onRecordsChanged.Invoke();
                onStorageChanged.Invoke();
                return true;
            }
            catch (Exception error)
            {
                storageBlocked = true;
                return StorageFailure("Restore failed. Autosave paused to protect existing data: " + error.Message);
            }
        }

        private bool StorageFailure(string message)
        {
            LastError = message;
            StorageStatus = message;
            onStorageChanged.Invoke();
            Debug.LogWarning(message, this);
            return false;
        }

        public ReviewRecord FindRecord(string id) { return session.records.Find(item => item.id == id); }

        public string FindMarkerOnRay(Ray ray, float surfaceDistance, float maximumDistance = 30f)
        {
            string selected = null;
            float nearest = maximumDistance;
            Vector3 direction = ray.direction.normalized;
            foreach (var record in session.records)
            {
                if (record.kind != "marker") continue;
                float along = Vector3.Dot(record.point - ray.origin, direction);
                if (along < 0 || along >= nearest || along > surfaceDistance + 0.04f) continue;
                if (Vector3.Distance(ray.origin + direction * along, record.point) > 0.06f) continue;
                nearest = along;
                selected = record.id;
            }
            return selected;
        }

        public bool SelectMarker(string id)
        {
            var record = FindRecord(id);
            if (record == null || record.kind != "marker") return false;
            CancelPending();
            SelectedId = id;
            MarkerSelected?.Invoke(record);
            return true;
        }

        public bool DeleteRecord(string id)
        {
            int index = session.records.FindIndex(item => item.id == id);
            if (index < 0) return false;
            RemoveVisual(visuals[index]);
            visuals.RemoveAt(index);
            session.records.RemoveAt(index);
            if (pendingFeedbackSaveId == id) pendingFeedbackSaveId = null;
            foreach (var record in session.records)
                if (record.measurementId == id) record.measurementId = "";
            if (SelectedId == id) SelectedId = null;
            Changed();
            return true;
        }

        public void CancelMarker(string id)
        {
            var record = FindRecord(id);
            if (record != null && record.state == FeedbackState.Draft) DeleteRecord(id);
            if (SelectedId == id) SelectedId = null;
        }

        public void BeginMeasurement() { SetMode(ReviewMode.Measure); }
        public void BeginMarking() { SetMode(ReviewMode.Mark); }
        public void StopTool() { SetMode(ReviewMode.None); }
        public void SetRoomId(string roomId) { currentRoomId = string.IsNullOrWhiteSpace(roomId) ? "Unassigned" : roomId; }
        public void SetMode(ReviewMode value)
        {
            CancelPending();
            mode = value;
            onModeChanged.Invoke();
        }

        public void CancelPending()
        {
            pending = false;
            RemoveVisual(pendingVisual);
            pendingVisual = null;
            preview = null;
            previewLabel = null;
        }

        public void PreviewPoint(Vector3 point, bool valid)
        {
            if (!pending || preview == null) return;
            preview.enabled = valid;
            previewLabel.gameObject.SetActive(valid);
            if (!valid) return;
            preview.SetPosition(0, firstPoint);
            preview.SetPosition(1, point);
            previewLabel.transform.position = (firstPoint + point) * 0.5f + Vector3.up * 0.08f;
            previewLabel.text = Vector3.Distance(firstPoint, point).ToString("F2") + " m (preview)";
        }

        public bool ConfirmPoint(Vector3 point, Transform target)
        {
            if (mode == ReviewMode.None || !Finite(point)) return false;
            if (mode == ReviewMode.Mark && session.records.Exists(item => item.kind == "marker" && item.state == FeedbackState.Draft))
            {
                SelectMarker(session.records.Find(item => item.kind == "marker" && item.state == FeedbackState.Draft).id);
                return false;
            }
            if (mode == ReviewMode.Measure && !pending)
            {
                firstPoint = point;
                pending = true;
                pendingVisual = NewRoot("Pending measurement");
                Dot(pendingVisual.transform, point, Color.cyan);
                preview = pendingVisual.AddComponent<LineRenderer>();
                preview.sharedMaterial = visualMaterial;
                preview.startColor = preview.endColor = Color.cyan;
                preview.startWidth = preview.endWidth = 0.005f;
                preview.positionCount = 2;
                preview.enabled = false;
                var label = new GameObject("Preview label");
                label.transform.SetParent(pendingVisual.transform, false);
                previewLabel = label.AddComponent<TextMesh>();
                previewLabel.characterSize = 0.018f;
                previewLabel.fontSize = 64;
                previewLabel.anchor = TextAnchor.MiddleCenter;
                label.AddComponent<ReviewBillboard>();
                label.SetActive(false);
                return true;
            }
            if (mode == ReviewMode.Measure && Vector3.Distance(firstPoint, point) < 0.001f) return false;
            var record = new ReviewRecord
            {
                id = Guid.NewGuid().ToString("N"),
                kind = mode == ReviewMode.Measure ? "measurement" : "marker",
                scene = gameObject.scene.path,
                roomId = roomMap != null ? roomMap.Resolve(point) : currentRoomId,
                objectPath = HierarchyPath(target),
                timestampUtc = DateTime.UtcNow.ToString("o"),
                point = mode == ReviewMode.Measure ? firstPoint : point,
                endPoint = point,
                distanceMetres = mode == ReviewMode.Measure ? Vector3.Distance(firstPoint, point) : 0f,
                number = session.nextNumber++,
                state = mode == ReviewMode.Measure ? FeedbackState.Submitted : FeedbackState.Draft
            };
            CancelPending();
            session.records.Add(record);
            visuals.Add(CreateVisual(record, record.number));
            Changed();
            RecordCreated?.Invoke(record);
            if (record.kind == "marker") SelectMarker(record.id);
            return true;
        }

        public bool UpdateFeedback(string id, string observation, string impact, IssueImportance importance)
        {
            return SubmitFeedback(id, observation, impact, importance, FindRecord(id)?.measurementId);
        }

        public bool SubmitFeedback(string id, string observation, string impact, IssueImportance importance, string measurementId = "")
        {
            LastError = null;
            var record = session.records.Find(item => item.id == id && item.kind == "marker");
            if (record == null || !Enum.IsDefined(typeof(IssueImportance), importance))
            { LastError = "Invalid marker or importance."; return false; }
            if (string.IsNullOrWhiteSpace(observation) || string.IsNullOrWhiteSpace(impact))
            { LastError = "Please enter both an observation and an everyday impact."; return false; }
            if (observation.Length > 2000 || impact.Length > 2000)
            { LastError = "Each field must be at most 2000 characters."; return false; }
            if (!string.IsNullOrEmpty(measurementId) && !session.records.Exists(item => item.id == measurementId && item.kind == "measurement"))
            { LastError = "The linked measurement no longer exists."; return false; }
            record.observation = observation.Trim();
            record.everydayImpact = impact.Trim();
            record.importance = importance;
            record.measurementId = measurementId ?? "";
            record.state = FeedbackState.Submitted;
            record.updatedUtc = DateTime.UtcNow.ToString("o");
            int index = session.records.IndexOf(record);
            RemoveVisual(visuals[index]);
            visuals[index] = CreateVisual(record, record.number);
            pendingFeedbackSaveId = record.id;
            // "Save feedback" must report the actual disk result, even when autosave is disabled.
            // Keep the submitted data and selection on failure so the user can retry without re-entering it.
            bool saved = SaveSession();
            if (saved) SelectedId = null;
            onRecordsChanged.Invoke();
            return saved;
        }

        public void UndoLast()
        {
            if (pending) { CancelPending(); return; }
            if (session.records.Count == 0) return;
            DeleteRecord(session.records[session.records.Count - 1].id);
        }

        public void ClearAll()
        {
            CancelPending();
            foreach (var visual in visuals) RemoveVisual(visual);
            visuals.Clear();
            session.records.Clear();
            pendingFeedbackSaveId = null;
            SelectedId = null;
            Changed();
        }

        public string ToJson() { return JsonUtility.ToJson(session, true); }

        public void ExportSession()
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "ReviewSessions");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, session.sessionId + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff") + ".json");
                var exported = JsonUtility.FromJson<ReviewSession>(ToJson());
                exported.records.RemoveAll(item => item.kind == "marker" && item.state == FeedbackState.Draft);
                ReviewStorage.Save(path, exported);
                LastExportPath = path;
                LastError = null;
                StorageStatus = "Exported: " + path;
                onStorageChanged.Invoke();
                Debug.Log("Review session exported: " + path, this);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError("Review export failed: " + LastError, this);
            }
        }

        private GameObject CreateVisual(ReviewRecord record, int number)
        {
            var root = NewRoot(record.kind + " " + number);
            Color color = record.kind == "measurement" ? Color.cyan : new Color(1f, 0.65f, 0.12f);
            if (record.kind == "marker" && record.state == FeedbackState.Draft) color = Color.gray;
            Dot(root.transform, record.point, color);
            Vector3 labelPosition = record.point;
            string label = "#" + number + (record.state == FeedbackState.Draft ? " Draft" : " " + record.importance);
            if (record.kind == "measurement")
            {
                Dot(root.transform, record.endPoint, color);
                var line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = visualMaterial;
                line.startColor = line.endColor = color;
                line.startWidth = line.endWidth = 0.008f;
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPositions(new[] { record.point, record.endPoint });
                labelPosition = (record.point + record.endPoint) * 0.5f;
                label = record.distanceMetres.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " m";
            }
            var labelObject = new GameObject("Label");
            labelObject.layer = 2;
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.position = labelPosition + Vector3.up * 0.08f;
            var text = labelObject.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = 64;
            text.characterSize = 0.018f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = color;
            labelObject.AddComponent<ReviewBillboard>();
            return root;
        }

        private GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            root.layer = 2;
            root.transform.SetParent(transform, false);
            return root;
        }

        private void Dot(Transform parent, Vector3 position, Color color)
        {
            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Point";
            dot.layer = 2;
            dot.transform.SetParent(parent, false);
            dot.transform.position = position;
            dot.transform.localScale = Vector3.one * 0.025f;
            var collider = dot.GetComponent<Collider>();
            collider.enabled = false;
            var renderer = dot.GetComponent<Renderer>();
            renderer.sharedMaterial = visualMaterial;
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static string HierarchyPath(Transform target)
        {
            string path = "";
            while (target != null)
            {
                path = target.name + "[" + target.GetSiblingIndex() + "]" + (path.Length == 0 ? "" : "/" + path);
                target = target.parent;
            }
            return path;
        }

        private static void RemoveVisual(GameObject visual)
        {
            if (visual == null) return;
            visual.SetActive(false);
            if (Application.isPlaying) Destroy(visual); else DestroyImmediate(visual);
        }

        private void OnDisable() { CancelPending(); }
    }
}
