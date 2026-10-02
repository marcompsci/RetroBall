using CallerRetroBall.Logic;
using CallerRetroBall.Logic.PixelArt;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>
    /// Draws one player from simulation state: body sprite (view, frame, flip), drop shadow,
    /// and the controlled-player ring. Reads state only; never changes it.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        private const float RunFps = 10f;
        private const float IdleFps = 2f;

        private PlayerRuntimeState _state;
        private Sprite[,] _frames;
        private SpriteRenderer _body;
        private SpriteRenderer _shadow;
        private SpriteRenderer _ring;
        private float _animTime;

        public static PlayerView Create(Transform parent, PlayerRuntimeState state, Sprite[,] frames, MatchArt art, Color ringColor)
        {
            var go = new GameObject("Player " + state.Index + " " + state.Def.DisplayName);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<PlayerView>();
            view._state = state;
            view._frames = frames;

            view._shadow = NewRenderer(go.transform, "Shadow", art.Shadow);
            view._ring = NewRenderer(go.transform, "Ring", art.Ring);
            view._ring.color = ringColor;
            view._ring.enabled = false;
            view._body = NewRenderer(go.transform, "Body", frames[(int)CharacterView.Back, 0]);
            view.Sync(0f, false, false);
            return view;
        }

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return sr;
        }

        /// <summary>Called by the match controller after each simulation update.</summary>
        public void Sync(float dt, bool controlled, bool shooting = false, float jump01 = 0f)
        {
            var motion = _state.Motion;
            transform.position = CourtSpace.ToWorldSnapped(motion.position);

            bool moving = motion.IsMoving;
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(motion.facing, out bool flip);
            int frame = shooting || jump01 > 0.05f ? CharacterSpriteGenerator.ShootFrame
                : moving ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps) % CharacterSpriteGenerator.RunFrames
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            // Jumping lifts the sprite off its shadow (0.7 m at the peak), snapped to art pixels.
            float lift = Mathf.Round(jump01 * 0.7f * CourtSpace.PixelsPerUnit) / CourtSpace.PixelsPerUnit;
            _body.transform.localPosition = new Vector3(0f, lift, 0f);

            int order = CourtSpace.SortingOrder(motion.position);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = controlled;
        }
    }
}
