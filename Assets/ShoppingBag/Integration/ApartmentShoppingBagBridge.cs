using System;
using UnityEngine;
using XRGroup10.ShoppingBag;

// Assembly-CSharp adapter; the reusable bag assembly never references apartment scripts.
[DisallowMultipleComponent]
public sealed class ApartmentShoppingBagBridge : MonoBehaviour, IBagInteractionContext
{
    public ShoppingBag bag;
    public BagInteractionGate gate;
    public ApartmentMenuController menu;
    public ApartmentTutorialController tutorial;
    public ApartmentVRLocomotion locomotion;
    [Tooltip("Optional final-integration context, for example review text editing.")]
    public MonoBehaviour additionalContext;
    public const string PageText = "Shopping BagSmallMediumLargeCurrent sizeHeldRelease firstNot configured";
    public bool IsConfigured => isActiveAndEnabled && bag != null && gate != null && menu != null &&
        bag.isActiveAndEnabled && gate.isActiveAndEnabled && menu.isActiveAndEnabled &&
        menu.shoppingBag == this && gate.GetComponent<ShoppingBag>() == bag;
    public event Action<BagSize> SizeSelected;
    ShoppingBag subscribedBag;
    bool dirty;
    string status = "";
    public BagInteractionBlockers Blockers
    {
        get
        {
            if (!IsConfigured) return BagInteractionBlockers.ContextUnavailable;
            var blockers = BagInteractionBlockers.None;
            if (menu.BlocksWorldInput) blockers |= BagInteractionBlockers.Menu;
            if (!ReferenceEquals(tutorial, null) && (tutorial == null || !tutorial.isActiveAndEnabled || !tutorial.AllowInteraction)) blockers |= BagInteractionBlockers.Tutorial;
            if (!ReferenceEquals(locomotion, null) && (locomotion == null || !locomotion.isActiveAndEnabled || locomotion.IsTeleportAiming))
                blockers |= locomotion != null && locomotion.isActiveAndEnabled ? BagInteractionBlockers.Teleport : BagInteractionBlockers.ContextUnavailable;
            if (!ReferenceEquals(additionalContext, null))
            {
                if (additionalContext == null || !additionalContext.isActiveAndEnabled ||
                    !(additionalContext is IBagInteractionContext provider) || ReferenceEquals(provider, this))
                    blockers |= BagInteractionBlockers.ContextUnavailable;
                else
                {
                    try { blockers |= provider.Blockers; }
                    catch (Exception) { blockers |= BagInteractionBlockers.ContextUnavailable; }
                }
            }
            return blockers;
        }
    }
    public void Configure(ApartmentMenuController owner, ShoppingBag instance)
    {
        if (owner == null || instance == null) throw new ArgumentNullException("Menu and bag are required.");
        if (owner.shoppingBag != null && owner.shoppingBag != this) throw new InvalidOperationException("Menu already has a bag bridge.");
        Unsubscribe(); menu = owner; bag = instance; gate = bag.GetComponent<BagInteractionGate>();
        tutorial = menu.tutorial; locomotion = menu.locomotion; menu.shoppingBag = this;
        if (gate == null) throw new InvalidOperationException("The bag requires its existing C1 gate.");
        if (isActiveAndEnabled) { Subscribe(); gate.BindContext(this); }
        dirty = true;
    }
    void OnEnable() { Subscribe(); if (gate != null) gate.BindContext(this); dirty = true; }
    void OnDisable() { Unsubscribe(); if (menu != null) menu.RefreshMyItems(); }
    void OnDestroy() { Unsubscribe(); }
    void Subscribe()
    {
        if (subscribedBag == bag) return;
        Unsubscribe(); subscribedBag = bag;
        if (subscribedBag == null) return;
        subscribedBag.SizeChanged += OnSizeChanged; subscribedBag.HoldStateChanged += OnHoldChanged;
    }
    void Unsubscribe()
    {
        if (subscribedBag != null) { subscribedBag.SizeChanged -= OnSizeChanged; subscribedBag.HoldStateChanged -= OnHoldChanged; }
        subscribedBag = null;
    }
    void OnSizeChanged(BagSize _) { status = ""; dirty = true; }
    void OnHoldChanged(ShoppingBag _) { status = ""; dirty = true; }
    void LateUpdate() { if (!dirty) return; dirty = false; if (menu != null) menu.RefreshMyItems(); }
    public bool TrySelectSize(BagSize size)
    {
        // Menu blocks Grip, not sizing. Old callbacks must recheck held state and page ownership.
        if (!IsConfigured || !menu.IsOpen || menu.Page != 3 || !menu.Visible || bag.IsHeld) return false;
        bool changed = bag.TrySetSize(size); status = changed ? "" : "Not configured"; dirty = true;
        if (changed) SizeSelected?.Invoke(size);
        return changed;
    }
    public void BuildMyItemsPage(ApartmentVRPanel panel)
    {
        if (panel == null) return;
        if (!IsConfigured) { panel.Label("Bag unavailable", "Shopping Bag: Not configured", 330, 220, 730, 100, 30); return; }
        panel.Label("Bag current size", "Current size: " + bag.CurrentSize, 330, 190, 730, 70, 33, true);
        for (int i = 0; i < 3; i++)
        {
            var size = (BagSize)i; bool selected = bag.CurrentSize == size;
            string title = (selected ? "●  " : "○  ") + size;
            if (bag.IsHeld) panel.Label("Bag size " + size + " locked", title, 350, 278 + i * 96, 690, 72, 33);
            else panel.Button("Bag size " + size, title, 330, 278 + i * 96, 730, 78, () => TrySelectSize(size), selected, 33);
        }
        panel.Label("Bag status", bag.IsHeld ? "Held - release first" : status, 330, 590, 730, 70, 29);
    }
}