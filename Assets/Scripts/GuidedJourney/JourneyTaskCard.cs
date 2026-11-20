using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Display and hit testing only. The apartment interactor supplies the single latched trigger press.
internal sealed class JourneyTaskCard : IDisposable
{
    internal enum Layout { Entrance, Introduction, Compact, Expanded }
    internal Layout Current { get; private set; }
    internal bool Modal => visible && (Current == Layout.Introduction || Current == Layout.Expanded || animating);
    private readonly ApartmentVRPanel panel;
    private readonly GuidancePanelPlacement placement = new GuidancePanelPlacement();
    private readonly Image background;
    private readonly Image[] edges = new Image[4];
    private readonly TextMeshProUGUI heading, details, footer;
    private readonly RectTransform confirm;
    private readonly Action rearm;
    private readonly Action dismiss;
    private bool finalTask;
    private bool visible, hover, animating, needsPlacement;
    private float started, flashUntil;
    private Vector3 fromPosition, fromScale, toPosition, toScale;
    private Quaternion fromRotation, toRotation;
    private string lastTitle, lastTask;
    private Layout? lastLayout;

    internal JourneyTaskCard(Transform parent, ApartmentMenuController menu, Action rearmInput, Action dismissCard)
    {
        rearm = rearmInput;
        dismiss = dismissCard;
        panel = new ApartmentVRPanel(parent, "Guided review task card (no collider)", menu.font, menu.panelShader, menu.textOverlayShader, 870, 320);
        background = panel.Root.GetComponent<Image>();
        Edge(0, 0, 0, 870, 5); Edge(1, 0, 315, 870, 5);
        Edge(2, 0, 5, 5, 310); Edge(3, 865, 5, 5, 310);
        heading = panel.Label("Stage", "", 24, 14, 822, 57, 35, true);
        details = panel.Label("Current task", "", 24, 78, 822, 126, 30);
        footer = panel.Label("Card controls", "", 24, 210, 822, 94, 26);
        panel.Button("Acknowledge", "知道了", 610, 235, 230, 65, null, true, 32);
        confirm = (RectTransform)panel.Root.Find("Acknowledge");
        panel.SetVisible(false);
    }

    private void Edge(int index, float x, float y, float w, float h)
    {
        string name = "Border " + index;
        panel.Box(name, x, y, w, h, Color.clear);
        edges[index] = panel.Root.Find(name).GetComponent<Image>();
    }

    internal void Introduce()
    {
        if (Current != Layout.Entrance) return;
        Current = Layout.Introduction;
        needsPlacement = true;
        rearm();
    }

    internal void Hide()
    {
        visible = hover = animating = false;
        panel.SetVisible(false);
    }

    internal void ShowFinalTask()
    {
        if (finalTask) return;
        finalTask = true;
        if (Current != Layout.Compact)
        {
            Current = Layout.Compact;
            needsPlacement = true;
            animating = false;
        }
        RefreshCopy();
    }

    internal void Flash() => flashUntil = Time.unscaledTime + .8f;

    internal void Draw(Transform head, Vector3 fallback, string title, string task, string shortTask)
    {
        bool compact = Current == Layout.Compact;
        bool intro = Current == Layout.Introduction;
        string body = compact ? shortTask : task;
        if (lastLayout != Current || title != lastTitle || body != lastTask)
        {
            panel.SetLabelText(heading, title);
            panel.SetLabelText(details, body);
            string key = intro ? "journey.card.intro" : compact ? "journey.card.expand" : "journey.card.fold";
            panel.SetLabelText(footer, Current == Layout.Entrance ? ApartmentGuidedJourney.GuideFooter : ApartmentLanguage.Text(key));
            footer.gameObject.SetActive(!finalTask);
            footer.rectTransform.sizeDelta = new Vector2(intro ? 565 : 822, 94);
            details.fontSizeMax = compact ? 38 : 30;
            bool canConfirm = intro || finalTask;
            confirm.gameObject.SetActive(canConfirm);
            panel.Root.Find("Acknowledge label").gameObject.SetActive(canConfirm);
            panel.Root.Find("Acknowledge accent").gameObject.SetActive(canConfirm);
            lastLayout = Current; lastTitle = title; lastTask = body;
        }
        var root = panel.Root;
        placement.AboveGaze = compact;
        placement.PanelScale = compact ? .00055f : .001f;
        if (!visible || needsPlacement)
        {
            placement.Place(head, root, fallback, Time.unscaledTime, true);
            toPosition = root.position; toRotation = root.rotation; toScale = root.localScale;
            needsPlacement = false;
        }
        else if (!animating) placement.Place(head, root, fallback, Time.unscaledTime);
        if (animating)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - started) / .25f);
            float blend = Mathf.SmoothStep(0, 1, t);
            root.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, blend), Quaternion.Slerp(fromRotation, toRotation, blend));
            root.localScale = Vector3.Lerp(fromScale, toScale, blend);
            animating = t < 1f;
            if (!animating && compact) rearm();
        }
        bool bright = hover || Time.unscaledTime < flashUntil;
        background.color = bright ? new Color(.065f,.25f,.29f,.98f) : new Color(.025f,.045f,.067f,.97f);
        var border = bright ? new Color(.30f,1f,.87f) : new Color(.09f,.38f,.42f);
        foreach (var edge in edges) edge.color = border;
        visible = true;
        panel.SetVisible(true);
    }

    internal void RefreshCopy() => lastLayout = null;

    internal bool Point(Ray ray, float reach, bool pressed, out Vector3 end)
    {
        end = ray.GetPoint(reach);
        if (!visible || Current == Layout.Entrance) return false;
        hover = ApartmentTutorialController.HitRectangle(panel.Root, ray, reach, out var point);
        if (hover) end = point;
        bool consumed = hover || Modal;
        if (!hover || !pressed || animating) return consumed;
        if (finalTask && ApartmentTutorialController.HitRectangle(confirm, ray, reach, out _))
        {
            Current = Layout.Compact;
            Hide();
            dismiss();
            rearm();
            return true;
        }
        if (Current == Layout.Introduction && !ApartmentTutorialController.HitRectangle(confirm, ray, reach, out _))
            return true;
        var root = panel.Root;
        fromPosition = root.position; fromRotation = root.rotation; fromScale = root.localScale;
        Current = Current == Layout.Compact ? Layout.Expanded : Layout.Compact;
        animating = needsPlacement = true; hover = false; started = Time.unscaledTime;
        rearm();
        return true;
    }

    public void Dispose() => panel.Dispose();
}
