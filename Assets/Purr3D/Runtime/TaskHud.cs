using Purr3D.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Purr3D
{
    /// <summary>
    /// The panel at the bottom of the screen that reads out the task you're standing on: ref, title, state and
    /// points. Built in code (its own overlay Canvas, under <see cref="ScreenFade"/>'s), like the fade; the art pass
    /// can restyle it.
    /// </summary>
    public sealed class TaskHud : MonoBehaviour
    {
        private const float Width = 760f;
        private const float Height = 170f;
        private const float Pad = 22f;
        private const float Strip = 12f;

        private CanvasGroup _group;
        private Image _strip;
        private TextMeshProUGUI _ref;
        private TextMeshProUGUI _title;
        private Image _chip;
        private TextMeshProUGUI _state;
        private TextMeshProUGUI _points;
        private float _target;

        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.12f;

        /// <summary>The ref on show, or null when hidden.</summary>
        public string Showing { get; private set; }

        public static TaskHud Create(Transform parent)
        {
            var go = new GameObject("Task HUD");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900; // under the fade

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = go.AddComponent<TaskHud>();
            hud.Build(go.transform);
            return hud;
        }

        public void Show(TaskCard card, Color projectColour)
        {
            if (card == null) { Hide(); return; }

            Showing = card.Ref;
            _strip.color = projectColour;
            _ref.text = card.Ref;
            _title.text = card.Title;
            _chip.color = StateColours.For(card.StateGroup, 0.6f);
            _state.text = card.StateName;
            _points.text = card.PointsText;
            _target = 1f;
        }

        public void Hide()
        {
            Showing = null;
            _target = 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_group.alpha, _target)) return;
            _group.alpha = Mathf.MoveTowards(_group.alpha, _target, Time.unscaledDeltaTime / fadeSeconds);
        }

        private void Build(Transform root)
        {
            var panel = Rect("Panel", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            panel.sizeDelta = new Vector2(Width, Height);
            panel.anchoredPosition = new Vector2(0f, 40f);
            panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.09f, 0.88f);

            _group = panel.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            // Down the left edge, in the project's (door's) colour.
            var strip = Rect("Project colour", panel, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f));
            strip.sizeDelta = new Vector2(Strip, 0f);
            _strip = strip.gameObject.AddComponent<Image>();

            var left = Strip + Pad;
            _ref = Text("Ref", panel, 40f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            Place(_ref.rectTransform, left, Pad, 260f, 48f);

            _points = Text("Points", panel, 24f, FontStyles.Normal, TextAlignmentOptions.TopRight);
            _points.color = new Color(1f, 1f, 1f, 0.75f);
            Place(_points.rectTransform, Width - Pad - 220f, Pad + 10f, 220f, 32f);

            _title = Text("Title", panel, 26f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _title.textWrappingMode = TextWrappingModes.Normal;
            _title.overflowMode = TextOverflowModes.Truncate;
            _title.maxVisibleLines = 2;
            Place(_title.rectTransform, left, Pad + 52f, Width - left - Pad, 70f);

            // The state as a little chip in its zone's colour, top right beside the points.
            var chip = Rect("State chip", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            Place(chip, Width - Pad - 220f - 16f - 200f, Pad + 4f, 200f, 40f);
            _chip = chip.gameObject.AddComponent<Image>();
            _state = Text("State", chip, 22f, FontStyles.Bold, TextAlignmentOptions.Center);
            Fill(_state.rectTransform);
            _state.overflowMode = TextOverflowModes.Truncate;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            return rect;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var rect = Rect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Positions a top-left-anchored rect <paramref name="x"/> across and <paramref name="y"/> down.</summary>
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Fill(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
