using CallerRetroBall.Logic;
using UnityEngine;

namespace CallerRetroBall.Gameplay
{
    /// <summary>Draws the ball at its ground position lifted by its height, plus a ground shadow.</summary>
    public sealed class BallView : MonoBehaviour
    {
        private SpriteRenderer _ball;
        private SpriteRenderer _shadow;

        public static BallView Create(Transform parent, MatchArt art)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BallView>();

            var shadowGo = new GameObject("Shadow");
            shadowGo.transform.SetParent(go.transform, false);
            view._shadow = shadowGo.AddComponent<SpriteRenderer>();
            view._shadow.sprite = art.BallShadow;

            var ballGo = new GameObject("Sprite");
            ballGo.transform.SetParent(go.transform, false);
            view._ball = ballGo.AddComponent<SpriteRenderer>();
            view._ball.sprite = art.Ball;
            return view;
        }

        /// <param name="heldOffsetPx">Dribble-move offset in art pixels, applied only while the ball is held.</param>
        public void Sync(BallState ball, int holderSortingOrder, Vector2Int heldOffsetPx = default)
        {
            _shadow.transform.position = CourtSpace.ToWorldSnapped(ball.Position);
            var offset = ball.IsHeld ? new Vector3(heldOffsetPx.x, heldOffsetPx.y, 0f) / CourtSpace.PixelsPerUnit : Vector3.zero;
            _ball.transform.position = CourtSpace.ToWorldSnapped(ball.Position, ball.Height) + offset;

            int order = ball.IsHeld ? holderSortingOrder + 1
                : ball.Phase == BallPhase.Shot || ball.Height > 2f ? 31000 // in the air: above players and the hoop
                : CourtSpace.SortingOrder(ball.Position, 1);
            _ball.sortingOrder = order;
            _shadow.sortingOrder = order - 3;
            // Shadow fades as the ball rises.
            float a = Mathf.Clamp01(1f - ball.Height / 4f);
            _shadow.color = new Color(1f, 1f, 1f, a);
        }
    }
}
