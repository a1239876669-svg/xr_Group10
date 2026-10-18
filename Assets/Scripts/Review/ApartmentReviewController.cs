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
    int aspect, rating = 1;
    IssueImportance importance = IssueImportance.Medium;
    int measurementPage;
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
        CloseKeyboard(); editingId = record.id;
        observation = record.observation; impact = record.everydayImpact;
        importance = record.importance; measurementId = record.measurementId;
        // Preset feedback reuses the two text fields the record already stores, so a record written by
        // an older build (or edited by hand) that matches nothing simply falls back to the first preset
        // and the middle rating rather than showing an empty selection.
        int found = System.Array.IndexOf(AspectNames, observation);
        aspect = found < 0 ? 0 : found;
        found = System.Array.IndexOf(AspectRatings[aspect], impact);
        rating = found < 0 ? 1 : found;
        originalText = !string.IsNullOrEmpty(record.observation) && System.Array.IndexOf(AspectNames,record.observation)<0;
        originalText |= !string.IsNullOrEmpty(record.everydayImpact) && System.Array.IndexOf(AspectRatings[aspect],record.everydayImpact)<0;
        if(!originalText) { observation = AspectNames[aspect]; impact = AspectRatings[aspect][rating]; }
    }
    public bool Begin(ReviewMode mode)
    {
        if (tools == null || tools.IsStorageBlocked || (mode != ReviewMode.Mark && mode != ReviewMode.Measure)) return false;
        CloseKeyboard(); status = "";
        if (mode == ReviewMode.Mark)
            foreach (var record in tools.ActiveRecords)
                if (record.kind == "marker" && record.state == FeedbackState.Draft)
                { tools.SelectMarker(record.id); return false; }
        tools.SetMode(mode); return true;
    }
    public bool ProcessPointer(Ray ray, bool pressed, out Vector3 end)
    {
        end = ray.GetPoint(maximumDistance);
        if (tools == null || menu == null || menu.interactor == null || !isActiveAndEnabled) return false;
        if (tools.Mode == ReviewMode.None)
        {
            bool hasMarker = false;
            foreach (var record in tools.ActiveRecords) if (record.kind == "marker") { hasMarker = true; break; }
            if (!hasMarker) return false;
        }
        bool valid = menu.interactor.TryFirstHit(ray, maximumDistance, out RaycastHit hit);
        if (valid) end = hit.point;
        tools.PreviewPoint(hit.point, valid);
        if (menu.interactor.RayOriginBlocked) { end = ray.origin; return tools.Mode != ReviewMode.None; }
        string marker = tools.Mode != ReviewMode.Measure ? tools.FindMarkerOnRay(ray, valid ? hit.distance : maximumDistance, maximumDistance) : null;
        if (marker != null)
        {
            if (pressed) tools.SelectMarker(marker);
            return true;
        }
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
        panel.Label("Marker location", "#" + current.number + "  " + current.roomId, 330, 182, 490, 44, 27);
        panel.Button("Previous marker", "上一条", 830, 182, 110, 44, () => Adjacent(markers, current, -1), false, 22);
        panel.Button("Next marker", "下一条", 950, 182, 110, 44, () => Adjacent(markers, current, 1), false, 22);
        if(originalText)
        {
            panel.Label("Original title",ApartmentLanguage.Text("recovery.original"),330,232,730,32,27);
            panel.LabelRaw("User saved feedback",Preview(observation)+" / "+Preview(impact),330,272,730,82,27);
            panel.Button("Use preset feedback",ApartmentLanguage.Text("recovery.usepresets"),330,368,730,58,()=>{originalText=false;SelectAspect(aspect);},false,27);
        }
        else
        {
        panel.Label("Aspect title", "反馈内容", 330, 232, 730, 32, 27);
        for (int i = 0; i < AspectNames.Length; i++)
        {
            int index = i;
            panel.Button("Aspect " + i, AspectNames[i], 330 + i * 186, 266, 172, 58, () => SelectAspect(index), aspect == i, 27);
        }
        panel.Label("Rating title", "评价", 330, 334, 730, 32, 27);
        for (int i = 0; i < AspectRatings[aspect].Length; i++)
        {
            int index = i;
            panel.Button("Rating " + i, AspectRatings[aspect][i], 330 + i * 248, 368, 234, 58, () => SelectRating(index), rating == i, 27);
        }
        }
        panel.Button("Importance", "重要程度：" + ImportanceNames[(int)importance], 330, 440, 270, 56, () => { importance = (IssueImportance)(((int)importance + 1) % 3); Refresh(); }, false, 27);
        var measurement = tools.FindRecord(measurementId);
        panel.Button("Linked measurement", "关联测距：" + (measurement == null ? "无" : "#" + measurement.number + " " + measurement.distanceMetres.ToString("F2") + " m"), 615, 440, 445, 56, CycleMeasurement, false, 26);
        panel.Label("Review status", string.IsNullOrEmpty(tools.LastError) ? status : tools.LastError, 330, 504, 730, 34, 23);
        panel.Button("Save feedback", "保存反馈", 330, 548, 350, 62, SaveFeedback, true);
        panel.Button("Delete marker", tools.CanCancelCurrentDraft(current.id) ? "取消草稿" : ApartmentLanguage.Text("recovery.hidemarker"), 710, 548, 350, 62, () => { CloseKeyboard(); if(!tools.CancelCurrentDraft(current.id))tools.HideRecord(current.id); editingId = null; status = ""; Refresh(); });
        if (current.state == FeedbackState.Submitted)
            panel.Button("Continue marking", "继续标记", 330, 618, 350, 54, () => menu.StartReview(ReviewMode.Mark));
        panel.Button("Stop marking", "停止工具", 710, 618, 350, 54, tools.StopTool);
    }
    void Adjacent(List<ReviewRecord> markers, ReviewRecord current, int direction)
    {
        if (markers.Count > 0) tools.SelectMarker(markers[(markers.IndexOf(current) + direction + markers.Count) % markers.Count].id);
    }
    static string Preview(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "点击输入";
        string line = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        if (line.Length <= 36) return line;
        int length = char.IsHighSurrogate(line[35]) ? 35 : 36;
        return line.Substring(0, length) + "…";
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
        aspect = Mathf.Clamp(index, 0, AspectNames.Length - 1);
        if (rating >= AspectRatings[aspect].Length) rating = AspectRatings[aspect].Length - 1;
        observation = AspectNames[aspect];
        impact = AspectRatings[aspect][rating];
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
        else if (tools.SubmitFeedback(editingId, observation, impact, importance, measurementId)) status = "已保存";
        else status = "提交失败";
        Refresh();
    }
}
