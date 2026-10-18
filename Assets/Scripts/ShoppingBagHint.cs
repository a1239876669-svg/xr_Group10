using UnityEngine;

// A one-off hint next to the shopping bag. It stays at the bag's starting spot instead of following
// it, and dismissing it only hides this panel — the grab and size code is never touched.
public sealed class ShoppingBagHint : MonoBehaviour
{
    public ApartmentMenuController menu;
    public string firstLine = "按住左手握把可以提起购物袋";
    public string secondLine = "按 B 键打开菜单，在「我的物品」里调节袋子大小";
    public string dismissLabel = "我知道了";

    const float PanelWidth = 660f, PanelHeight = 300f;
    const float DismissX = 220f, DismissY = 158f, DismissW = 220f, DismissH = 66f;

    ApartmentVRPanel panel;
    GameObject hitTarget;
    bool dismissed;

    public bool Dismissed => dismissed;

    void Start()
    {
        if (menu == null || menu.font == null || menu.panelShader == null || menu.textOverlayShader == null)
        {
            Debug.LogWarning("ShoppingBagHint: menu/font/shaders are not assigned, so the hint stays hidden.", this);
            enabled = false;
            return;
        }
        FaceTheViewer();
        BuildPanel();
    }

    void OnDestroy()
    {
        if (panel != null) panel.Dispose();
        panel = null;
    }

    // Fixed in the room but turned to whoever is standing at the entrance, so the text is readable
    // without the panel chasing the player around.
    void FaceTheViewer()
    {
        var eye = menu.locomotion != null && menu.locomotion.rig != null ? menu.locomotion.rig.centerEyeAnchor : null;
        Vector3 target = eye != null ? eye.position : transform.position + Vector3.back;
        Vector3 flat = target - transform.position;
        flat.y = 0f;
        // A world-space Canvas presents its readable face on -Z, so forward has to point away from the
        // viewer for the text to read the right way round.
        if (flat.sqrMagnitude > .0001f) transform.rotation = Quaternion.LookRotation(-flat);
    }

    void BuildPanel()
    {
        panel = new ApartmentVRPanel(transform, "Bag hint (session only)", menu.font, menu.panelShader,
            menu.textOverlayShader, PanelWidth, PanelHeight);
        panel.Label("Hint line one", firstLine, 30, 28, PanelWidth - 60, 46, 27, true);
        panel.Label("Hint line two", secondLine, 30, 86, PanelWidth - 60, 46, 24);
        panel.Button("Hint dismiss", dismissLabel, DismissX, DismissY, DismissW, DismissH, Dismiss, false, 28);
        panel.SetVisible(true);

        // The panel draws the button, but nothing routes panel pointers to it, so the button carries a
        // plain solid collider that the existing world-ray interaction can hit instead. No changes to
        // the interactor are needed for that.
        float cx = DismissX + DismissW * .5f - PanelWidth * .5f;
        float cy = PanelHeight * .5f - (DismissY + DismissH * .5f);
        hitTarget = new GameObject("Hint dismiss target");
        hitTarget.transform.SetParent(panel.Root, false);
        hitTarget.transform.localPosition = new Vector3(cx, cy, 0f);
        var box = hitTarget.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = new Vector3(DismissW, DismissH, 12f);
        hitTarget.AddComponent<HintDismissTarget>().owner = this;
    }

    public void Dismiss()
    {
        if (dismissed) return;
        dismissed = true;
        if (hitTarget != null) hitTarget.SetActive(false);
        if (panel != null) panel.SetVisible(false);
    }
}

// Pick target for the dismiss button. Implementing the project's existing world-interaction interface
// is what lets the normal right-hand ray click it without touching the interactor.
public sealed class HintDismissTarget : MonoBehaviour, IApartmentInteractable
{
    public ShoppingBagHint owner;

    // Reuses an existing category only so the floating prompt has something to read. Hovering never
    // writes tutorial progress, so this cannot disturb the lesson bookkeeping.
    public ApartmentInteractionKind HintCategory => ApartmentInteractionKind.Cabinet;
    public bool NeedsAttention => true;
    public bool CanInteract => owner != null && !owner.Dismissed;
    public string InteractionPrompt => "扣扳机关闭提示";
    public Transform HintAnchor => transform;
    public Renderer HighlightRenderer => null;

    public bool TryInteract(out string reason)
    {
        reason = "";
        if (owner == null || owner.Dismissed) return false;
        owner.Dismiss();
        return true;
    }
}
