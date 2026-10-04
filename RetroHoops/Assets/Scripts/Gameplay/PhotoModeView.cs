using System;
using System.Collections;
using CallerRetroBall.Core;
using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using CallerRetroBall.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Photo mode, from the pause menu: the game stays frozen, the HUD goes away, you drag to frame
    /// the shot and zoom in, pick a filter and a frame, then SNAP saves a crisp PNG and opens the
    /// share sheet (Save Image puts it in Photos). DONE puts the camera back.
    /// </summary>
    public sealed class PhotoModeView : MonoBehaviour, IDragHandler
    {
        private Camera _camera;
        private Vector3 _startPos;
        private float _startSize;
        private Canvas _canvas;
        private TextMeshProUGUI _filterLabel, _frameLabel;
        private PhotoFilter _filter = PhotoFilter.None;
        private bool _frame = true;
        private string _caption;
        private Action _onClose;
        private bool _busy;

        private static PhotoFilter _lastFilter = PhotoFilter.None;
        private static bool _lastFrame = true;

        public static PhotoModeView Open(Camera camera, string caption, Action onClose)
        {
            var canvas = UiKit.CreateScreenCanvas("PhotoModeCanvas", 45);
            // A transparent full-screen catcher behind the buttons: dragging it moves the camera.
            var catcher = UiKit.Panel(canvas.transform, new Color(0f, 0f, 0f, 0f), name: "DragArea");
            UiKit.Stretch(catcher.rectTransform);
            catcher.raycastTarget = true;
            var view = catcher.gameObject.AddComponent<PhotoModeView>();
            view._canvas = canvas;
            view._camera = camera;
            view._startPos = camera.transform.position;
            view._startSize = camera.orthographicSize;
            view._caption = caption;
            view._onClose = onClose;
            view._filter = _lastFilter;
            view._frame = _lastFrame;
            view.Build(UiKit.SafeArea(canvas.transform));
            return view;
        }

        private void Build(RectTransform safe)
        {
            var title = UiKit.ShadowLabel(safe, "PHOTO MODE", 56f, Theme.Cream, Theme.Pink, 5f);
            var trt = (RectTransform)title.transform.parent;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(900f, 80f);
            trt.anchoredPosition = new Vector2(0f, -24f);
            var hint = UiKit.Label(safe, "DRAG TO MOVE  ·  - / + TO ZOOM", 28f, Theme.Cream, TextAlignmentOptions.Center, true);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 44f));
            hint.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            hint.raycastTarget = false;

            var panel = UiKit.Panel(safe, Color.white, Theme.PanelSprite(), true, "PhotoBar");
            var prt = panel.rectTransform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(-32f, 300f);
            prt.anchoredPosition = new Vector2(0f, 16f);
            panel.raycastTarget = true;

            var column = UiKit.Column(panel.transform, 14f, new RectOffset(20, 20, 20, 20), "PhotoControls");
            UiKit.Stretch(column);

            var row1 = UiKit.Row(column, 12f, "FilterRow");
            UiKit.Size(row1, 110f);
            UiKit.Button(row1, "<", () => SetFilter(PhotoMode.Next(_filter, -1)), ButtonStyle.Secondary, 100f, 44f);
            _filterLabel = UiKit.Label(row1, "", 34f, Theme.Gold, TextAlignmentOptions.Center, true);
            UiKit.Button(row1, ">", () => SetFilter(PhotoMode.Next(_filter, 1)), ButtonStyle.Secondary, 100f, 44f);
            var frameButton = UiKit.Button(row1, "", ToggleFrame, ButtonStyle.Ghost, 100f, 30f);
            _frameLabel = frameButton.GetComponentInChildren<TextMeshProUGUI>();

            var row2 = UiKit.Row(column, 12f, "ActionRow");
            UiKit.Size(row2, 120f);
            UiKit.Button(row2, "-", () => Zoom(1.25f), ButtonStyle.Secondary, 110f, 48f);
            UiKit.Button(row2, "+", () => Zoom(0.8f), ButtonStyle.Secondary, 110f, 48f);
            UiKit.Button(row2, "SNAP", Snap, ButtonStyle.Primary, 110f, 44f);
            UiKit.Button(row2, "DONE", Close, ButtonStyle.Ghost, 110f, 36f);
            Refresh();
        }

        private void SetFilter(PhotoFilter f)
        {
            _filter = _lastFilter = f;
            Refresh();
        }

        private void ToggleFrame()
        {
            _frame = _lastFrame = !_frame;
            Refresh();
        }

        private void Refresh()
        {
            _filterLabel.text = PhotoMode.FilterNames[(int)_filter];
            if (_frameLabel != null) _frameLabel.text = _frame ? "FRAME ON" : "FRAME OFF";
        }

        private void Zoom(float factor)
        {
            _camera.orthographicSize = Mathf.Clamp(_camera.orthographicSize * factor, _startSize * 0.35f, _startSize);
        }

        public void OnDrag(PointerEventData e)
        {
            float worldPerPixel = 2f * _camera.orthographicSize / Mathf.Max(1, Screen.height);
            var p = _camera.transform.position;
            p.x -= e.delta.x * worldPerPixel;
            p.y -= e.delta.y * worldPerPixel;
            // Stay roughly over the court the game was showing.
            float range = _startSize * 1.2f;
            p.x = Mathf.Clamp(p.x, _startPos.x - range, _startPos.x + range);
            p.y = Mathf.Clamp(p.y, _startPos.y - range, _startPos.y + range);
            _camera.transform.position = p;
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (kb.escapeKey.wasPressedThisFrame) Close();
                else if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) Snap();
                else if (kb.leftArrowKey.wasPressedThisFrame) SetFilter(PhotoMode.Next(_filter, -1));
                else if (kb.rightArrowKey.wasPressedThisFrame) SetFilter(PhotoMode.Next(_filter, 1));
            }
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                if (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) Close();
                else if (pad.buttonSouth.wasPressedThisFrame) Snap();
                else if (pad.leftShoulder.wasPressedThisFrame) SetFilter(PhotoMode.Next(_filter, -1));
                else if (pad.rightShoulder.wasPressedThisFrame) SetFilter(PhotoMode.Next(_filter, 1));
                var stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f)
                {
                    var p = _camera.transform.position;
                    p += new Vector3(stick.x, stick.y, 0f) * (_camera.orthographicSize * Time.unscaledDeltaTime);
                    _camera.transform.position = p;
                }
                float trig = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
                if (Mathf.Abs(trig) > 0.1f) Zoom(1f - trig * Time.unscaledDeltaTime);
            }
#endif
        }

        private void Snap()
        {
            if (_busy) return;
            StartCoroutine(Capture());
        }

        private IEnumerator Capture()
        {
            _busy = true;
            _canvas.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D shot = null;
            string path = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                var pixels = shot.GetPixels32();
                // Screen pixels per art pixel at the current zoom; sample one per art pixel so the photo stays crisp.
                float ratio = Screen.height / (2f * _camera.orthographicSize * CourtSpace.PixelsPerUnit);
                ratio = Mathf.Max(1f, ratio);
                int w = Mathf.Max(1, Mathf.FloorToInt(shot.width / ratio));
                int h = Mathf.Max(1, Mathf.FloorToInt(shot.height / ratio));
                var art = new PixelCanvas(w, h);
                for (int y = 0; y < h; y++)
                {
                    int sy = Mathf.Min(shot.height - 1, (int)((y + 0.5f) * ratio));
                    for (int x = 0; x < w; x++)
                    {
                        int sx = Mathf.Min(shot.width - 1, (int)((x + 0.5f) * ratio));
                        var p = pixels[sy * shot.width + sx];
                        art.Pixels[y * w + x] = new RgbColor(p.r, p.g, p.b);
                    }
                }
                var photo = PhotoMode.Compose(art, _filter, _frame, _caption, PhotoMode.ScaleFor(w + (_frame ? PhotoMode.Border * 2 : 0)));
                var tex = new Texture2D(photo.Width, photo.Height, TextureFormat.RGB24, false);
                var outPixels = new Color32[photo.Pixels.Length];
                for (int i = 0; i < outPixels.Length; i++)
                {
                    var c = photo.Pixels[i];
                    outPixels[i] = new Color32(c.r, c.g, c.b, 255);
                }
                tex.SetPixels32(outPixels);
                tex.Apply(false);
                var png = tex.EncodeToPNG();
                Destroy(tex);
                string folder = System.IO.Path.Combine(Application.persistentDataPath, "Photos");
                System.IO.Directory.CreateDirectory(folder);
                path = System.IO.Path.Combine(folder, PhotoMode.FileName(DateTime.Now));
                System.IO.File.WriteAllBytes(path, png);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Retro Hoops] Photo failed: " + ex.Message);
                path = null;
            }
            finally
            {
                if (shot != null) Destroy(shot);
                _canvas.enabled = true;
                _busy = false;
            }
            if (path == null) yield break;
            if (App.Career != null)
            {
                App.Career.photosTaken++;
                App.SaveCareer();
            }
            Haptics.Success();
            Share.File(path, "Retro Hoops photo mode");
        }

        private void Close()
        {
            if (_busy) return;
            _camera.transform.position = _startPos;
            _camera.orthographicSize = _startSize;
            var done = _onClose;
            _onClose = null;
            Destroy(_canvas.gameObject);
            done?.Invoke();
        }
    }
}
