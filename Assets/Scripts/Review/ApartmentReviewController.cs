using System.Collections.Generic;
using System.Globalization;
using QuestGuidance.Review;
using UnityEngine;

// The apartment interactor remains the only owner of the right trigger and pointing ray.
[DisallowMultipleComponent]
public sealed class ApartmentReviewController : MonoBehaviour
{
    public ReviewTools tools;
    public ApartmentMenuController menu;
    [Range(1f, 30f)] public float maximumDistance = 30f;
    const string FeedbackPresetText = "反馈内容评价选择房间大小过道舒适度家具摆放采光通风太小合适宽敞太窄刚好不合理一般差好";
    public const string AllText = "开始测距停止工具取消起点已有测距上一页下一页暂无测距右手指向表面，扣动扳机选择两个点距离以米显示，记录自动保存已选起点，请选择终点放置标记继续标记现象描述日常影响点击输入重要程度低中高关联测距无保存留言取消草稿删除标记上一条下一条暂无标记标记自动保存请选择标记或放置新标记请先填写现象描述和日常影响已保存请输入留言请在头显内使用系统键盘输入存储失败请输入字符数量不能超过提交失败位置房间" + FeedbackPresetText;
    static readonly string[] ImportanceNames = { "低", "中", "高" };
    // Viewing feedback is picked from presets, never typed: the headset has no usable keyboard and a
    // realtor reads these back as short, consistent phrases rather than free prose.
    static readonly string[] AspectNames = { "房间大小", "过道舒适度", "家具摆放", "采光通风" };
    static readonly string[][] AspectRatings =
    {
        new[] { "太小", "合适", "宽敞" },
        new[] { "太窄", "刚好", "宽敞" },
        new[] { "不合理", "一般", "合理" },
        new[] { "差", "一般", "好" }
    };
    string editingId, observation = "", impact = "", measurementId = "", status = "";
    int aspect = -1, rating = -1;
    IssueImportance importance = IssueImportance.Medium;
    int measurementPage;
    int feedbackPage = 1;
    TouchScreenKeyboard keyboard;
    bool editingImpact, originalText;
    public bool IsEditingText => keyboard != null && keyboard.status == TouchScreenKeyboard.Status.Visible;

    void OnEnable()
    {
        if (tools == null) return;
        if(menu!=null)tools.DisplayFont=menu.font;
        tools.MarkerSelected += Select;
        tools.onRecordsChanged.AddListener(Refresh);
        tools.onModeChanged.AddListener(Refresh);
    }
    void OnDisable()
    {
        if (tools != null)
        {
            tools.MarkerSelected -= Select;
            tools.onRecordsChanged.RemoveListener(Refresh);
            tools.onModeChanged.RemoveListener(Refresh);
            tools.PreviewPoint(default, false);
            tools.ClearMarkerHover();
        }
        CloseKeyboard();
    }
    void Update()
    {
        if (keyboard != null && keyboard.status != TouchScreenKeyboard.Status.Visible)
        {
            if (keyboard.status == TouchScreenKeyboard.Status.Done)
            {
                if (editingImpact) impact = keyboard.text; else observation = keyboard.text;
            }
            CloseKeyboard(); Refresh();
        }
        if (tools != null && tools.HasPendingPoint && menu != null &&
            (menu.BlocksWorldInput || menu.locomotion.IsTeleportAiming ||
             (menu.tutorial != null && menu.tutorial.isActiveAndEnabled && !menu.tutorial.AllowInteraction))) tools.PreviewPoint(default, false);
    }
    void Refresh()
    {
        if (!string.IsNullOrEmpty(measurementId) && tools.FindRecord(measurementId) == null) measurementId = "";
        if (menu != null) menu.RefreshReview();
    }
    void CloseKeyboard() { if (keyboard != null) keyboard.active = false; keyboard = null; }
    void Select(ReviewRecord record)
    {
        LoadEditor(record);
        status = "";
        menu?.OpenMarkerEditor();
    }
    void LoadEditor(ReviewRecord record)
    {
        if (editingId == record.id) return;
        CloseKeyboard(); editingId = record.id; feedbackPage = 1;
        observation = record.observation; impact = record.everydayImpact;
        importance = record.importance; measurementId = record.measurementId;
        aspect = System.Array.IndexOf(AspectNames, observation);
        rating = aspect >= 0 ? System.Array.IndexOf(AspectRatings[aspect], impact) : -1;
        originalText = (!string.IsNullOrEmpty(observation) && aspect < 0)
            || (!string.IsNullOrEmpty(impact) && rating < 0);
    }
    public void ToggleTool(ReviewMode mode) => SetTool(mode, true);
    public bool Begin(ReviewMode mode) => SetTool(mode, false);

    bool SetTool(ReviewMode mode, bool toggle)
    {
        if (tools == null || (mode != ReviewMode.Mark && mode != ReviewMode.Measure)) return false;
        CloseKeyboard(); status = "";
        if (!tools.IsStorageBlocked && mode == ReviewMode.Mark)
            foreach (var record in tools.ActiveRecords)
                if (record.kind == "marker" && record.state == FeedbackState.Draft)
                {
                    tools.StopTool();
                    tools.SelectMarker(record.id);
                    return false;
                }
        if (toggle && tools.Mode == mode)
        {
            tools.StopTool();
            return false;
        }
        if (tools.IsStorageBlocked) return false;
        tools.SetMode(mode); return true;
    }
    public bool ProcessPointer(Ray ray, bool pressed, out Vector3 end)
    {
        end = ray.GetPoint(maximumDistance);
        if (tools == null || !tools.isActiveAndEnabled || menu == null || menu.interactor == null || !isActiveAndEnabled) return false;
        if (tools.Mode == ReviewMode.None)
        {
            bool hasMarker = false;
            foreach (var record in tools.ActiveRecords) if (record.kind == "marker") { hasMarker = true; break; }
            if (!hasMarker) return false;
        }
        bool valid = menu.interactor.TryFirstHit(ray, maximumDistance, out RaycastHit hit);
        if (valid) end = hit.point;
        if (menu.interactor.RayOriginBlocked)
        {
            tools.ClearMarkerHover();
            tools.PreviewPoint(default, false);
            end = ray.origin;
            return tools.Mode != ReviewMode.None;
        }
        if (tools.PointAtPendingStart(ray.origin, ref end, pressed)) return true;
        tools.PreviewPoint(hit.point, valid);
        if (tools.PointAtMarker(ray.origin, ref end, pressed)) return true;
        if (tools.Mode == ReviewMode.None) return false;
        if (valid && pressed) tools.ConfirmPoint(hit.point, hit.transform);
        return true;
    }
    List<ReviewRecord> Records(string kind)
    {
        var result = new List<ReviewRecord>();
        foreach (var record in tools.ActiveRecords) if (record.kind == kind) result.Add(record);
        return result;
    }
    public void BuildMeasurementPage(ApartmentVRPanel panel)
    {
        panel.Button("Begin measurement", "开始测距", 330, 190, 350, 70, () => menu.StartReview(ReviewMode.Measure), true);
        panel.Button("Stop review", "停止工具", 710, 190, 350, 70, tools.StopTool);
        panel.Label("Measurement help", (tools.HasPendingPoint ? "已选起点，请选择终点" : "右手指向表面，扣动扳机选择两个点\n距离以米显示，记录自动保存") + "\n" + tools.CurrentToolReadout, 330, 272, 730, 80, 27);
        panel.Button("Cancel measurement point", "取消起点", 330, 355, 230, 55, () => { tools.CancelPending(); Refresh(); },false,23);
        panel.Button("Undo measurement display", ApartmentLanguage.Text("recovery.undo"), 580, 355, 230, 55, () => { tools.UndoLastMeasurementDisplay();Refresh(); },false,23);
        panel.Button("Hide measurements", ApartmentLanguage.Text("recovery.hideall"), 830, 355, 230, 55, tools.HideMeasurementDisplays,false,23);
        var measurements = Records("measurement");
        measurementPage = Mathf.Clamp(measurementPage, 0, Mathf.Max(0, (measurements.Count - 1) / 3));
        for (int i = 0; i < 3; i++)
        {
            int index = measurements.Count - 1 - measurementPage * 3 - i;
            if (index < 0) break;
            var record = measurements[index];
            panel.Label("Measurement " + record.id, "#" + record.number + "   " + record.distanceMetres.ToString("F2", CultureInfo.InvariantCulture) + " m   " + record.roomId, 330, 420 + i * 61, 550, 54, 28);
            panel.Button("Hide measurement " + record.id, ApartmentLanguage.Text("recovery.hide"), 910, 420 + i * 61, 150, 54, () => tools.HideRecord(record.id), false, 27);
        }
        if (measurements.Count == 0) panel.Label("No measurements", "暂无测距", 330, 440, 730, 70, 32);
        if (measurementPage > 0) panel.Button("Previous measurements", "上一页", 330, 615, 350, 55, () => { measurementPage--; Refresh(); });
        if ((measurementPage + 1) * 3 < measurements.Count) panel.Button("Next measurements", "下一页", 710, 615, 350, 55, () => { measurementPage++; Refresh(); });
    }
    public void BuildMarkerPage(ApartmentVRPanel panel)
    {
        var markers = Records("marker");
        var current = tools.IsRecordVisible(editingId) ? tools.FindRecord(editingId) : null;
        if (current == null && markers.Count > 0) { current = markers[markers.Count - 1]; LoadEditor(current); }
        if (current == null)
        {
            panel.Button("Begin marking", "放置标记", 330, 200, 730, 75, () => menu.StartReview(ReviewMode.Mark), true);
            panel.Label("Marker help", "右手指向表面，扣动扳机放置标记\n选择标记后填写现象描述和日常影响\n标记和留言自动保存", 330, 315, 730, 190, 30);
            panel.Button("Stop marking", "停止工具", 330, 590, 730, 65, tools.StopTool);
            return;
        }
        panel.Label("Marker location", "#" + current.number + "  " + current.roomId, 330, 182, 430, 44, 27);
        if (menu == null || !menu.EditingExitRecord)
        {
            panel.Button("Previous marker", "上一条", 774, 182, 164, 44, () => Adjacent(markers, current, -1), false, 22);
            panel.Button("Next marker", "下一条", 950, 182, 110, 44, () => Adjacent(markers, current, 1), false, 22);
        }
        if(originalText)
        {
            panel.Label("Original title",ApartmentLanguage.Text("recovery.original"),330,232,730,32,27);
            var body = panel.LabelRaw("User saved feedback", observation + "\n" + impact, 330, 270, 730, 92, 24);
            body.enableAutoSizing = false;
            body.alignment = TMPro.TextAlignmentOptions.TopLeft;
            body.overflowMode = TMPro.TextOverflowModes.Page;
            body.ForceMeshUpdate(true);
            int pages = Mathf.Max(1, body.textInfo.pageCount);
            feedbackPage = Mathf.Clamp(feedbackPage, 1, pages);
            body.pageToDisplay = feedbackPage;
            if (feedbackPage > 1)
                panel.Button("Previous feedback page", "上一页", 330, 368, 165, 58, () => { feedbackPage--; Refresh(); }, false, 24);
            if (feedbackPage < pages)
                panel.Button("Next feedback page", "下一页", 505, 368, 165, 58, () => { feedbackPage++; Refresh(); }, false, 24);
            panel.Button("Use preset feedback",ApartmentLanguage.Text("recovery.usepresets"),685,368,375,58,() =>
            {
                originalText = false;
                aspect = rating = -1;
                observation = impact = status = "";
                Refresh();
            },false,27);
        }
        else
        {
            panel.Label("Aspect title", ApartmentLanguage.Text("feedback.aspectQuestion"), 330, 226, 730, 50, 27);
            for (int i = 0; i < AspectNames.Length; i++)
            {
                int index = i;
                panel.Button("Aspect " + i, AspectNames[i], 330 + i * 186, 277, 172, 50, () => SelectAspect(index), aspect == i, 27);
            }
            panel.Label("Rating title", ApartmentLanguage.Text(aspect < 0 ? "feedback.chooseAspect" : "feedback.question." + aspect), 330, 328, 730, 50, 27);
            for (int i = 0; aspect >= 0 && i < AspectRatings[aspect].Length; i++)
            {
                int index = i;
                panel.Button("Rating " + i, AspectRatings[aspect][i], 330 + i * 248, 379, 234, 53, () => SelectRating(index), rating == i, 27);
            }
        }
        panel.Button("Importance", "重要程度：" + ImportanceNames[(int)importance], 330, 440, 270, 56, () => { importance = (IssueImportance)(((int)importance + 1) % 3); Refresh(); }, false, 27);
        var measurement = tools.FindRecord(measurementId);
        panel.Button("Linked measurement", "关联测距：" + (measurement == null ? "无" : "#" + measurement.number + " " + measurement.distanceMetres.ToString("F2") + " m"), 615, 440, 445, 56, CycleMeasurement, false, 26);
        panel.Label("Review status", string.IsNullOrEmpty(tools.LastError) ? status : tools.LastError, 330, 504, 730, 34, 23);
        panel.Button("Save feedback", "保存反馈", 330, 548, 350, 62, SaveFeedback, true);
        panel.Button("Delete marker", tools.CanCancelCurrentDraft(current.id) ? "取消草稿" : ApartmentLanguage.Text("recovery.hidemarker"), 710, 548, 350, 62, () => { CloseKeyboard(); if(!tools.CancelCurrentDraft(current.id))tools.HideRecord(current.id); editingId = null; status = ""; if (menu != null && menu.EditingExitRecord) menu.ReturnToExitReview(); else Refresh(); });
        if (menu != null && menu.EditingExitRecord) return;
        if (current.state == FeedbackState.Submitted)
            panel.Button("Continue marking", "继续标记", 330, 618, 350, 54, () => menu.StartReview(ReviewMode.Mark));
        panel.Button("Stop marking", "停止工具", 710, 618, 350, 54, tools.StopTool);
    }
    void Adjacent(List<ReviewRecord> markers, ReviewRecord current, int direction)
    {
        if (markers.Count > 0) tools.SelectMarker(markers[(markers.IndexOf(current) + direction + markers.Count) % markers.Count].id);
    }
    internal void CloseExitEditor() { CloseKeyboard(); editingId = null; }

    internal static bool IsPresetPair(string observation, string impact)
    {
        int index = System.Array.IndexOf(AspectNames, observation);
        return index >= 0 && System.Array.IndexOf(AspectRatings[index], impact) >= 0;
    }

    internal static string MarkerFeedbackSummary(ReviewRecord record)
    {
        if (IsPresetPair(record.observation, record.everydayImpact))
            return ApartmentLanguage.Display(record.observation) + " / " + ApartmentLanguage.Display(record.everydayImpact);
        // Only the built-in preset pairs are translated. Free-form feedback stays in its original language.
        if (string.IsNullOrWhiteSpace(record.observation) && string.IsNullOrWhiteSpace(record.everydayImpact))
            return ApartmentLanguage.Text("review.pendingfeedback");
        string Excerpt(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return ApartmentLanguage.Text("review.pendingfeedback");
            string line = value.Trim().Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
            var elements = new StringInfo(line);
            return elements.LengthInTextElements <= 28 ? line : elements.SubstringByTextElements(0, 28) + "…";
        }
        return Excerpt(record.observation) + " / " + Excerpt(record.everydayImpact);
    }
    void EditText(bool everydayImpact)
    {
        if (!TouchScreenKeyboard.isSupported) { status = "请在头显内使用系统键盘输入"; Refresh(); return; }
        CloseKeyboard(); editingImpact = everydayImpact;
        keyboard = TouchScreenKeyboard.Open(everydayImpact ? impact : observation, TouchScreenKeyboardType.Default,
            false, true, false, false, "请输入留言", 2000);
    }
    void CycleMeasurement()
    {
        var measurements = Records("measurement");
        int index = measurements.FindIndex(record => record.id == measurementId);
        measurementId = index + 1 < measurements.Count ? measurements[index + 1].id : "";
        Refresh();
    }
    void SelectAspect(int index)
    {
        if (aspect == index) return;
        aspect = index;
        rating = -1;
        observation = AspectNames[aspect];
        impact = "";
        status = "";
        Refresh();
    }
    void SelectRating(int index)
    {
        rating = Mathf.Clamp(index, 0, AspectRatings[aspect].Length - 1);
        impact = AspectRatings[aspect][rating];
        status = "";
        Refresh();
    }
    void SaveFeedback()
    {
        if (string.IsNullOrWhiteSpace(observation) || string.IsNullOrWhiteSpace(impact)) status = "请先选择反馈内容和评价";
        else if (tools.SubmitFeedback(editingId, observation, impact, importance, measurementId))
        {
            status = "已保存";
            if (menu != null && menu.EditingExitRecord) { menu.ReturnToExitReview(); return; }
        }
        else status = "提交失败";
        Refresh();
    }
}
