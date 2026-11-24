using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Purr3D
{
    /// <summary>
    /// A full-screen fade to black and back, built in code (its own overlay Canvas), for hiding a teleport.
    /// <c>await fade.Out()</c>, move things, <c>await fade.In()</c>.
    /// </summary>
    public sealed class ScreenFade : MonoBehaviour
    {
        private CanvasGroup _group;

        public static ScreenFade Create(Transform parent)
        {
            var go = new GameObject("Screen fade");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var fade = go.AddComponent<ScreenFade>();
            fade._group = go.AddComponent<CanvasGroup>();
            fade._group.alpha = 0f;
            fade._group.blocksRaycasts = false;
            fade._group.interactable = false;

            var black = new GameObject("Black");
            black.transform.SetParent(go.transform, false);
            var rect = black.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            black.AddComponent<Image>().color = Color.black;
            return fade;
        }

        public Task Out(float seconds, CancellationToken ct = default) => To(1f, seconds, ct);
        public Task In(float seconds, CancellationToken ct = default) => To(0f, seconds, ct);

        private async Task To(float target, float seconds, CancellationToken ct)
        {
            var start = _group.alpha;
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                await Awaitable.NextFrameAsync(ct);
                elapsed += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / seconds));
            }
            _group.alpha = target;
        }
    }
}
