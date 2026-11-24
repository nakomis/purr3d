using System;
using System.Threading.Tasks;
using Purr3D.Core;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Purr3D
{
    /// <summary>
    /// "Connect to Purr": the screen that asks for an API key when there isn't one, or when Purr refuses the one in
    /// use (PURR3D-14). Built in code, like the HUD; <see cref="PurrService"/> makes one.
    ///
    /// Connect checks the key with <c>GET /me</c> before anything is saved: a bad key gets an inline error, a good
    /// one is saved (see <see cref="PurrSettings"/>) and the corridor loads. F10 opens it again in the corridor, to
    /// switch environment or forget the saved key. While it's up, the cursor is free and the player's controls are
    /// paused, so typing doesn't make Dante jump.
    /// </summary>
    public sealed class ApiKeyScreen : MonoBehaviour
    {
        private static readonly Color Panel = new Color(0.08f, 0.09f, 0.11f, 0.97f);
        private static readonly Color Selected = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color Unselected = new Color(0.22f, 0.24f, 0.28f);
        private static readonly Color Good = new Color(0.45f, 0.85f, 0.5f);
        private static readonly Color Bad = new Color(1f, 0.5f, 0.45f);

        private PurrService _service;
        private GameObject _root;
        private TextMeshProUGUI _reason;
        private TMP_InputField _key;
        private TextMeshProUGUI _revealLabel;
        private Button _sandbox, _production, _connect, _forget, _cancel;
        private TextMeshProUGUI _status;
        private string _environment = PurrEnvironments.Sandbox;
        private bool _busy;

        public bool IsOpen => _root != null && _root.activeSelf;

        public static ApiKeyScreen Create(Transform parent, PurrService service)
        {
            var go = new GameObject("API key screen");
            go.transform.SetParent(parent, false);
            var screen = go.AddComponent<ApiKeyScreen>();
            screen._service = service;
            screen.Build();
            return screen;
        }

        private void Start()
        {
            _service.KeyNeeded += Open;
            if (_service.KeyProblem != null) Open(_service.KeyProblem);
        }

        private void OnDestroy()
        {
            if (_service != null) _service.KeyNeeded -= Open;
        }

        private void Update()
        {
            // F10: change or forget the key, from the corridor only (a room's crates belong to the current corridor).
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f10Key.wasPressedThisFrame || IsOpen) return;
            var travel = FindAnyObjectByType<RoomTravel>();
            if (travel != null && travel.CurrentDoor != null) return;
            Open(null);
        }

        /// <summary>Shows the screen, explaining <paramref name="reason"/> (null: opened by hand).</summary>
        public void Open(string reason)
        {
            EnsureEventSystem();
            _root.SetActive(true);
            _reason.text = reason ?? "Change the key Purr3D uses, or switch between sandbox and production.";
            SetEnvironment(_service.Settings?.Environment ?? PurrEnvironments.Sandbox);
            _key.text = "";
            Reveal(false);
            Status("", Color.white);
            _cancel.gameObject.SetActive(reason == null && _service.KeyProblem == null);
            PlayerControls(false);
            _key.Select();
            _key.ActivateInputField();
        }

        private void Close()
        {
            _root.SetActive(false);
            PlayerControls(true);
        }

        private async void Connect()
        {
            if (_busy) return;
            var key = (_key.text ?? "").Trim();
            if (key.Length == 0)
            {
                Status("Paste a key first.", Bad);
                return;
            }

            var candidate = PurrSecretsFile.For(_environment, key);
            _busy = true;
            SetInteractable(false);
            Status($"Checking with {candidate.BaseUrl}…", Color.white);
            try
            {
                var actor = await _service.CheckKeyAsync(candidate);
                Status($"Connected as {actor}.", Good);
                await Task.Delay(700);
                _service.UseKey(candidate);
                Close();
            }
            catch (PurrApiException e)
            {
                Status(Explain(e), Bad);
            }
            catch (OperationCanceledException)
            {
                // Play mode stopped.
            }
            finally
            {
                _busy = false;
                if (_root != null) SetInteractable(true);
            }
        }

        private static string Explain(PurrApiException e)
        {
            switch (e.Status)
            {
                case 401:
                case 403: return "Purr didn't accept that key. Check it's the purr3d key for this environment.";
                case 0: return "Couldn't reach Purr. Check your connection.";
                default: return $"Purr answered {e.Status}: {e.Code ?? e.Message}";
            }
        }

        private void Forget()
        {
            Status(_service.ForgetSavedKey()
                ? "Saved key forgotten. It stays in use until you quit or enter another."
                : "There was no saved key to forget.", Color.white);
        }

        private void SetEnvironment(string environment)
        {
            _environment = PurrEnvironments.UrlFor(environment) != null ? environment.ToLowerInvariant() : PurrEnvironments.Sandbox;
            Tint(_sandbox, _environment == PurrEnvironments.Sandbox ? Selected : Unselected);
            Tint(_production, _environment == PurrEnvironments.Production ? Selected : Unselected);
        }

        private void Reveal(bool reveal)
        {
            _key.contentType = reveal ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            _key.ForceLabelUpdate();
            _revealLabel.text = reveal ? "Hide" : "Show";
        }

        private void Status(string text, Color colour)
        {
            _status.text = text;
            _status.color = colour;
        }

        private void SetInteractable(bool on)
        {
            foreach (var b in new[] { _sandbox, _production, _connect, _forget, _cancel }) b.interactable = on;
            _key.interactable = on;
        }

        /// <summary>Frees the cursor and pauses the player's controls while the screen is up, then gives them back.</summary>
        private static void PlayerControls(bool on)
        {
            var player = GameObject.FindWithTag(PlayerTrigger.PlayerTag);
            var inputs = player != null ? player.GetComponent<StarterAssetsInputs>() : null;
            if (inputs != null)
            {
                inputs.cursorLocked = on;
                inputs.cursorInputForLook = on;
                inputs.move = Vector2.zero;
                inputs.look = Vector2.zero;
                inputs.jump = false;
                inputs.sprint = false;
            }
            var playerInput = player != null ? player.GetComponent<PlayerInput>() : null;
            if (playerInput != null)
            {
                if (on) playerInput.ActivateInput();
                else playerInput.DeactivateInput();
            }
            Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !on;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem (API key screen)", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        // ---- building the panel -----------------------------------------------------------------------------------

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 980; // over the HUD, under the fade
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Backdrop", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            Stretch((RectTransform)_root.transform);
            _root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

            var panel = Box(_root.transform, "Panel", new Vector2(760f, 470f), Vector2.zero, Panel);
            var resources = new TMP_DefaultControls.Resources();

            Label(panel, "Title", "Connect to Purr", 40f, FontStyles.Bold, new Vector2(0f, 190f), new Vector2(680f, 50f));
            _reason = Label(panel, "Reason", "", 22f, FontStyles.Normal, new Vector2(0f, 135f), new Vector2(680f, 60f));
            _reason.color = new Color(1f, 1f, 1f, 0.8f);

            _sandbox = MakeButton(panel, resources, "Sandbox", new Vector2(-95f, 75f), new Vector2(180f, 44f), () => SetEnvironment(PurrEnvironments.Sandbox));
            _production = MakeButton(panel, resources, "Production", new Vector2(95f, 75f), new Vector2(180f, 44f), () => SetEnvironment(PurrEnvironments.Production));

            var field = TMP_DefaultControls.CreateInputField(resources);
            field.name = "API key";
            field.transform.SetParent(panel, false);
            Place((RectTransform)field.transform, new Vector2(-45f, 10f), new Vector2(590f, 52f));
            _key = field.GetComponent<TMP_InputField>();
            _key.contentType = TMP_InputField.ContentType.Password;
            _key.characterLimit = 256;
            _key.pointSize = 24f;
            if (_key.placeholder is TMP_Text placeholder)
            {
                placeholder.text = "Paste the purr3d API key";
                placeholder.fontSize = 22f;
            }
            _key.onSubmit.AddListener(_ => Connect());

            var reveal = MakeButton(panel, resources, "Show", new Vector2(310f, 10f), new Vector2(90f, 52f), () => Reveal(_key.contentType == TMP_InputField.ContentType.Password));
            _revealLabel = reveal.GetComponentInChildren<TextMeshProUGUI>();

            _connect = MakeButton(panel, resources, "Connect", new Vector2(0f, -60f), new Vector2(260f, 56f), Connect);
            Tint(_connect, Selected);

            _status = Label(panel, "Status", "", 22f, FontStyles.Normal, new Vector2(0f, -120f), new Vector2(680f, 50f));

            _forget = MakeButton(panel, resources, "Forget saved key", new Vector2(-150f, -185f), new Vector2(240f, 44f), Forget);
            _cancel = MakeButton(panel, resources, "Cancel", new Vector2(150f, -185f), new Vector2(240f, 44f), Close);

            _root.SetActive(false);
        }

        private static RectTransform Box(Transform parent, string name, Vector2 size, Vector2 position, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, position, size);
            go.AddComponent<Image>().color = colour;
            return rect;
        }

        private static TextMeshProUGUI Label(Transform parent, string name, string text, float size, FontStyles style, Vector2 position, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, box);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;
            return label;
        }

        private static Button MakeButton(Transform parent, TMP_DefaultControls.Resources resources, string text, Vector2 position, Vector2 size, Action onClick)
        {
            var go = TMP_DefaultControls.CreateButton(resources);
            go.name = text;
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, position, size);
            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 22f;
            label.color = Color.white;
            Tint(go.GetComponent<Button>(), Unselected);
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            return button;
        }

        private static void Tint(Button button, Color colour)
        {
            var image = button.GetComponent<Image>();
            if (image != null) image.color = colour;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
