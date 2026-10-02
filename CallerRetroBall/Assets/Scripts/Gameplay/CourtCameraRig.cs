using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Portrait court camera: integer pixel zoom for crisp art, the court anchored under the
    /// HUD at the top of the screen (thumb zone below), and smooth horizontal follow of the
    /// controlled player, clamped to the court.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CourtCameraRig : MonoBehaviour
    {
        /// <summary>Always keep at least this much court width visible (m).</summary>
        [SerializeField] private float minVisibleWidth = 11.5f;
        [SerializeField] private float followStiffness = 5f;
        /// <summary>Share of the screen height reserved for the HUD above the court.</summary>
        [SerializeField] private float hudReserve = 0.1f;

        private Camera _camera;
        private CourtGeometry _court;
        private float _minX, _maxX;
        private int _lastWidth, _lastHeight;
        private Rect _lastSafe;
        private float _targetY;

        public int Zoom { get; private set; } = 1;

        private float _shake;

        /// <summary>Brief camera shake (dunks, blocks). Ignored when Screen Shake is off in Settings.</summary>
        public void Shake(float amount)
        {
            var settings = Core.App.Career?.settings;
            if (settings != null && (!settings.screenShake || settings.reduceMotion)) return;
            _shake = Mathf.Max(_shake, amount);
        }

        public void Init(CourtGeometry court, Color background)
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = background;
            _court = court;
            _minX = -court.HalfWidth - CourtGenerator.SideMargin;
            _maxX = court.HalfWidth + CourtGenerator.SideMargin;
            Recompute();
            transform.position = new Vector3(0f, _targetY, -10f);
        }

        /// <summary>Follows a court position (usually the controlled player).</summary>
        public void Follow(Vec2 target, float dt, bool snap = false)
        {
            if (_court == null) return;
            if (Screen.width != _lastWidth || Screen.height != _lastHeight || Screen.safeArea != _lastSafe) Recompute();

            float halfW = _camera.orthographicSize * _camera.aspect;
            var current = new Vec2(transform.position.x, _targetY);
            var goal = new Vec2(target.x, _targetY);
            var next = snap ? goal : CameraMath.Follow(current, goal, dt, followStiffness);
            next = CameraMath.ClampView(next, halfW, _camera.orthographicSize,
                                        new Vec2(_minX, -1000f), new Vec2(_maxX, 1000f));
            // Snap the camera to the pixel grid of the current zoom to avoid shimmering.
            float step = 1f / (CourtSpace.PixelsPerUnit * Zoom);
            float sx = 0f, sy = 0f;
            if (_shake > 0.001f)
            {
                sx = (Random.value * 2f - 1f) * _shake;
                sy = (Random.value * 2f - 1f) * _shake;
                _shake = Mathf.MoveTowards(_shake, 0f, dt * 1.2f);
            }
            transform.position = new Vector3(Mathf.Round((next.x + sx) / step) * step, Mathf.Round((_targetY + sy) / step) * step, -10f);
        }

        private void Recompute()
        {
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastSafe = Screen.safeArea;

            Zoom = CameraMath.IntegerZoom(Screen.width, minVisibleWidth, CourtSpace.PixelsPerUnit);
            _camera.orthographicSize = CameraMath.OrthographicSize(Screen.height, Zoom, CourtSpace.PixelsPerUnit);

            // Top of the court art (crowd strip) sits just below the HUD and the notch.
            float unitsPerPixel = 1f / (CourtSpace.PixelsPerUnit * Zoom);
            float topInsetPx = (Screen.height - _lastSafe.yMax) + Screen.height * hudReserve;
            float courtTopWorld = CourtGenerator.BaselineMargin; // baseline is world y = 0
            _targetY = courtTopWorld + topInsetPx * unitsPerPixel - _camera.orthographicSize;
        }
    }
}
