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
        private Sprite[,] _heads;
        private SpriteRenderer _head;
        private const float BigHeadScale = 1.7f;

        /// <summary>BIG HEADS secret: draws an enlarged copy of the head above the body.</summary>
        public void EnableBigHead(Sprite[,] heads)
        {
            _heads = heads;
            _head = NewRenderer(transform, "BigHead", heads[(int)CharacterView.Back, 0]);
            _head.transform.localScale = new Vector3(BigHeadScale, BigHeadScale, 1f);
        }

        private void SyncHead(int view, int frame, bool flip)
        {
            if (_head == null) return;
            _head.sprite = _heads[view, frame];
            _head.flipX = flip;
            // The head sprite's pivot is its bottom row; the body's pivot is one pixel above its feet.
            int row = CharacterSpriteGenerator.HeadBottomRow(_state.Def.appearance, frame);
            var body = _body.transform.localPosition;
            _head.transform.localPosition = body + new Vector3(0f, (row - 1) / CourtSpace.PixelsPerUnit, 0f);
            _head.sortingOrder = _body.sortingOrder + 1;
        }

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

        /// <summary>Draws a recorded replay frame instead of live state (presentation only).</summary>
        public void SyncReplay(float dt, ReplayPlayer f, bool controlled)
        {
            transform.position = CourtSpace.ToWorldSnapped(f.Position);
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(f.Facing, out bool flip);
            int frame = f.ArmsUp || f.Jump01 > 0.05f ? CharacterSpriteGenerator.ShootFrame
                : f.Moving ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps) % CharacterSpriteGenerator.RunFrames
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            float lift = Mathf.Round(f.Jump01 * 0.7f * CourtSpace.PixelsPerUnit) / CourtSpace.PixelsPerUnit;
            _body.transform.localPosition = new Vector3(0f, lift, 0f);
            int order = CourtSpace.SortingOrder(f.Position);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = controlled;
            SyncHead((int)view, frame, flip);
        }

        /// <summary>Called by the match controller after each simulation update.</summary>
        public void Sync(float dt, bool controlled, bool shooting = false, float jump01 = 0f, FlairPose flair = default)
        {
            var motion = _state.Motion;
            transform.position = CourtSpace.ToWorldSnapped(motion.position);

            bool moving = motion.IsMoving;
            _animTime += dt;
            var view = CharacterSpriteGenerator.ViewFor(CourtSpace.Facing(motion.facing), out bool flip);
            if (flair.FlipOverride) flip = !flip;
            int frame = shooting || jump01 > 0.05f || flair.ArmsUp ? CharacterSpriteGenerator.ShootFrame
                : moving ? CharacterSpriteGenerator.IdleFrames + (int)(_animTime * RunFps) % CharacterSpriteGenerator.RunFrames
                : (int)(_animTime * IdleFps) % CharacterSpriteGenerator.IdleFrames;
            _body.sprite = _frames[(int)view, frame];
            _body.flipX = flip;
            // Jumping lifts the sprite off its shadow (0.7 m at the peak), snapped to art pixels.
            // Celebrations and dribble moves add a few art pixels on top (presentation only).
            const float px = 1f / CourtSpace.PixelsPerUnit;
            float lift = Mathf.Round(jump01 * 0.7f * CourtSpace.PixelsPerUnit) * px + flair.Lift * px;
            _body.transform.localPosition = new Vector3(flair.OffsetX * px, lift, 0f);

            int order = CourtSpace.SortingOrder(motion.position);
            _body.sortingOrder = order;
            _shadow.sortingOrder = order - 2;
            _ring.sortingOrder = order - 1;
            _ring.enabled = controlled;
            SyncHead((int)view, frame, flip);
        }
    }
}
