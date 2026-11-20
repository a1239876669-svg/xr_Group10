using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QuestGuidance.Review
{
    // One display-only canvas is reused as the right hand moves between visible markers.
    internal sealed class MarkerPreviewCard
    {
        private readonly RectTransform root;
        private readonly TextMeshProUGUI label;
        private readonly ReviewBillboard billboard;

        public MarkerPreviewCard(Transform parent, TMP_FontAsset font)
        {
            var card = new GameObject("Marker preview", typeof(RectTransform), typeof(Canvas), typeof(Image));
            card.layer = 2;
            root = card.GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.sizeDelta = new Vector2(540f, 210f);
            root.pivot = new Vector2(.5f, 0f);
            root.localScale = Vector3.one * .0008f;
            card.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var background = card.GetComponent<Image>();
            background.color = new Color(.035f, .08f, .1f, .96f);
            background.raycastTarget = false;

            var text = new GameObject("Marker information", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.layer = 2;
            label = text.GetComponent<TextMeshProUGUI>();
            var rect = label.rectTransform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 10f);
            rect.offsetMax = new Vector2(-18f, -10f);
            label.font = font != null ? font : TMP_Settings.defaultFontAsset;
            label.fontSize = label.fontSizeMax = 26f;
            label.fontSizeMin = 20f;
            label.enableAutoSizing = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.richText = false;
            label.raycastTarget = false;
            billboard = card.AddComponent<ReviewBillboard>();
            billboard.IsPreviewCard = true;
        }

        public void Show(ReviewRecord marker, ReviewRecord measurement)
        {
            string room = string.IsNullOrWhiteSpace(marker.roomId) ? "Unassigned" : marker.roomId;
            string heading = ApartmentLanguage.Format("review.markerheading", marker.number, ApartmentLanguage.Display(room));
            if (marker.state == FeedbackState.Draft) heading += " · " + ApartmentLanguage.Text("review.draft");
            string distance = measurement == null ? ApartmentLanguage.Text("none") :
                "#" + measurement.number + "  " + measurement.distanceMetres.ToString("F2", CultureInfo.InvariantCulture) + " m";
            label.text = heading + "\n" + ApartmentLanguage.Format("review.previewfeedback", ApartmentReviewController.MarkerFeedbackSummary(marker))
                + "\n" + ApartmentLanguage.Text("review.importanceprefix") + ApartmentLanguage.Text("review.importance." + (int)marker.importance)
                + "\n" + ApartmentLanguage.Text("review.linkprefix") + distance;
            billboard.SetAnchor(marker.point);
            root.position = marker.point + Vector3.up * .1f;
            root.gameObject.SetActive(true);
            if (Camera.main != null) billboard.PlaceForViewer(Camera.main.transform);
        }

        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
        }
    }
}
