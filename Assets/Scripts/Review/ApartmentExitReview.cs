using System;
using System.Collections.Generic;
using System.Globalization;
using QuestGuidance.Review;
using UnityEngine;

// Presentation state belongs to this menu instance. The review session owns IDs and disk writes.
internal sealed class ApartmentExitReview
{
    bool history, failed;
    int index;
    string temporarilyShown;
    public bool Saved { get; private set; }
    public bool Editing { get; private set; }
    public bool Inspecting { get; private set; }

    public void Reset()
    {
        history = false;
        index = 0;
        Saved = failed = false;
    }

    public void EndInspection(ReviewTools tools)
    {
        Editing = Inspecting = false;
        if (tools != null)
        {
            tools.EndReviewHighlight();
            if (temporarilyShown != null) tools.HideRecord(temporarilyShown);
        }
        temporarilyShown = null;
    }

    internal static string Describe(ReviewRecord record, ReviewTools tools)
    {
        bool measure = record.kind == "measurement";
        string kind = Text(measure ? "measure" : record.state == FeedbackState.Draft ? "draft" : "marker");
        string room = string.IsNullOrEmpty(record.roomId) ? Text("unknown") : ApartmentLanguage.Display(record.roomId);
        string target = record.objectPath ?? "";
        target = target.Substring(target.LastIndexOf('/') + 1);
        int suffix = target.LastIndexOf('[');
        if (suffix > 0 && target.EndsWith("]")) target = target.Substring(0, suffix);
        string heading = "#" + record.number + "  " + kind + "  |  " + room;
        if (target.Length > 0) heading += " / " + target;
        if (measure) return heading + "\n" + record.distanceMetres.ToString("F2", CultureInfo.InvariantCulture) + " m";
        string observation = record.observation ?? "", impact = record.everydayImpact ?? "";
        string body;
        if (ApartmentReviewController.IsPresetPair(observation, impact))
        {
            observation = ApartmentLanguage.Display(observation);
            impact = ApartmentLanguage.Display(impact);
            body = observation + (ApartmentLanguage.IsChinese ? "：" : ": ") + impact;
        }
        else body = (observation + "\n" + impact).Trim();
        var linked = tools.FindRecord(record.measurementId);
        if (linked != null && linked.kind == "measurement")
            body += "\n" + ApartmentLanguage.Format("exit.linked", linked.number, linked.distanceMetres);
        return heading + "\n" + (body.Length == 0 ? Text("unspecified") : body);
    }

    static string Text(string key) => ApartmentLanguage.Text("exit." + key);

    public void Draw(ApartmentVRPanel panel, ReviewTools tools, Action refresh, Action enterWorld,
        Action<string> editMarker, Action cancel, Action finish)
    {
        panel.Label("Exit title", Text("title"), 40, 18, 575, 60, 38, true);
        if (Saved)
        {
            panel.Label("Save receipt", Text("saved"), 48, 205, 904, 130, 36);
            panel.Button("Confirm quit", Text("finish"), 48, 580, 425, 76, finish, true);
            return;
        }

        void SwitchScope(bool earlier) { history = earlier; index = 0; refresh(); }
        panel.Button("This visit",Text("current"),40,92,448,58,() => SwitchScope(false),!history);
        panel.Button("Earlier visits",Text("history"),512,92,448,58,() => SwitchScope(true),history);
        var records = new List<ReviewRecord>();
        int measures = 0, drafts = 0;
        if (tools != null)
            foreach (var record in tools.Records)
            {
                if (tools.WasCreatedThisRun(record.id) == history) continue;
                records.Add(record);
                if (record.kind == "measurement") measures++;
                else if (record.state == FeedbackState.Draft) drafts++;
            }
        panel.LabelRaw("Record counts", ApartmentLanguage.Format("exit.count",records.Count,measures,
            records.Count - measures - drafts,drafts), 40,162,920,54,28);
        index = Mathf.Clamp(index,0,Mathf.Max(0,records.Count - 1));
        if (records.Count > 0)
        {
            var selected = records[index];
            panel.PagedText("Record details",Describe(selected,tools),40,222,920,185,30,true,detailNavigation:true);
            if (index > 0)
                panel.Button("Previous record",Text("prevRecord"),40,410,250,56,() => { index--; refresh(); },false,26);
            panel.LabelRaw("Record index",ApartmentLanguage.Format("exit.recordIndex",index+1,records.Count),620,18,340,60,26);
            if (index+1 < records.Count)
                panel.Button("Next record",Text("nextRecord"),710,410,250,56,() => { index++; refresh(); },false,26);
            panel.Button("Inspect location",Text("view"),40,482,448,62,() =>
            {
                if (!tools.HighlightForReview(selected.id)) return;
                tools.StopTool();
                Inspecting = true;
                enterWorld();
            },false,26);
            if (selected.kind == "marker")
                panel.Button("Edit reviewed marker",Text("edit"),512,482,448,62,() =>
                {
                    Editing = true;
                    temporarilyShown = tools.IsRecordVisible(selected.id) ? null : selected.id;
                    editMarker(selected.id);
                },false,26);
        }
        else panel.Label("Empty list",Text(tools == null ? "nodata" : "empty"),40,224,920,120,32);
        if (failed) panel.Label("Save failure",Text("failed"),40,554,920,55,26);
        else if (drafts > 0) panel.Label("Draft policy",Text("draftHelp"),40,554,920,55,26);
        panel.Button("Cancel quit","取消",40,620,448,60,cancel,false,29);
        panel.Button("Save and exit",Text(failed ? "retry" : "save"),512,620,448,60,() =>
        {
            Saved = tools != null && tools.SaveSession();
            failed = !Saved;
            refresh();
        },true,29);
    }
}
