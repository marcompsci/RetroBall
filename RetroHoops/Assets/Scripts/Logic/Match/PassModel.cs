using System;
using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    public enum PassType { Chest = 0, Bounce = 1 }

    [Serializable]
    public class PassTuning
    {
        public float chestSpeed = 12f;
        public float bounceSpeed = 9.5f;
        /// <summary>A defender this close to the lane makes the passer choose a bounce pass.</summary>
        public float laneRiskRadius = 1.1f;
        public float chestInterceptRadius = 0.5f;
        public float bounceInterceptRadius = 0.32f;
        /// <summary>Chance a defender in the lane takes it: base + fromDefense × rating.</summary>
        public float interceptBase = 0.45f;
        public float interceptFromDefense = 0.35f;
        /// <summary>Good passers thread lanes: reduces the chance by up to this much.</summary>
        public float interceptReductionFromPlaymaking = 0.25f;
        public float catchRadius = 0.45f;
        public float idealDistance = 5f;
        public float maxDistance = 13f;
        public float opennessWeight = 1f;
        public float directionWeight = 1.2f;
        public float laneRiskWeight = 0.8f;

        // Alley-oops
        /// <summary>A teammate this close to the rim catches a lob and finishes in the air.</summary>
        public float alleyOopRange = 2.3f;
        public float alleyOopMinPassDistance = 4f;
        public int alleyOopMinFinishing = 60;
        public float alleyOopInterceptScale = 0.4f;
        public float alleyOopCatchHeight = 3.3f;
        public float alleyOopArc = 0.9f;

        public static PassTuning Default => new PassTuning();
    }

    public struct PassCandidate
    {
        public int PlayerIndex;
        public Vec2 Position;
    }

    /// <summary>Pure passing maths: target choice, lane risk, pass type, and interceptions.</summary>
    public static class PassModel
    {
        /// <summary>
        /// Distance from <paramref name="p"/> to the lane between <paramref name="from"/> and
        /// <paramref name="to"/>. Defenders behind the passer or past the receiver don't count
        /// (returns float.MaxValue).
        /// </summary>
        public static float LaneClearance(Vec2 from, Vec2 to, Vec2 p, out float along)
        {
            var d = to - from;
            float len2 = d.SqrMagnitude;
            along = 0f;
            if (len2 < 1e-6f) return float.MaxValue;
            along = Vec2.Dot(p - from, d) / len2;
            if (along < 0.08f || along > 0.95f) return float.MaxValue;
            var closest = from + d * along;
            return Vec2.Distance(p, closest);
        }

        public static float MinClearance(Vec2 from, Vec2 to, IList<Vec2> defenders, out int closestDefender)
        {
            float best = float.MaxValue;
            closestDefender = -1;
            for (int i = 0; i < defenders.Count; i++)
            {
                float c = LaneClearance(from, to, defenders[i], out _);
                if (c < best)
                {
                    best = c;
                    closestDefender = i;
                }
            }
            return best;
        }

        public static float Openness(Vec2 receiver, IList<Vec2> defenders)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < defenders.Count; i++) nearest = Math.Min(nearest, Vec2.Distance(receiver, defenders[i]));
            return nearest;
        }

        /// <summary>
        /// Scores each teammate and returns the index into <paramref name="candidates"/> of the best
        /// receiver (-1 if none). <paramref name="aim"/> is the stick direction; zero means "best open man".
        /// </summary>
        public static int SelectTarget(Vec2 passer, Vec2 aim, IList<PassCandidate> candidates, IList<Vec2> defenders, PassTuning t)
        {
            int best = -1;
            float bestScore = float.MinValue;
            bool aiming = aim.SqrMagnitude > 0.09f;
            var aimDir = aim.Normalized;
            for (int i = 0; i < candidates.Count; i++)
            {
                var to = candidates[i].Position;
                float dist = Vec2.Distance(passer, to);
                if (dist < 0.5f || dist > t.maxDistance) continue;
                float score = Score(passer, to, dist, aiming, aimDir, defenders, t);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        private static float Score(Vec2 passer, Vec2 to, float dist, bool aiming, Vec2 aimDir, IList<Vec2> defenders, PassTuning t)
        {
            float open = Math.Min(Openness(to, defenders), 4f) / 4f;
            float lane = MinClearance(passer, to, defenders, out _);
            float laneRisk = lane >= t.laneRiskRadius ? 0f : 1f - lane / t.laneRiskRadius;
            float score = open * t.opennessWeight - Math.Abs(dist - t.idealDistance) / 10f - laneRisk * t.laneRiskWeight;
            if (aiming) score += Vec2.Dot(aimDir, (to - passer) / dist) * t.directionWeight;
            return score;
        }

        public static PassType ChooseType(float laneClearance, PassTuning t) =>
            laneClearance < t.laneRiskRadius ? PassType.Bounce : PassType.Chest;

        public static float Speed(PassType type, PassTuning t) => type == PassType.Bounce ? t.bounceSpeed : t.chestSpeed;

        /// <summary>
        /// Chance that a defender at <paramref name="clearance"/> from the lane steals the pass.
        /// Zero outside the intercept radius, so a clearly open lane is never stolen.
        /// </summary>
        public static float InterceptChance(PassType type, float clearance, int defenderDefense, int passerPlaymaking, PassTuning t)
        {
            float radius = type == PassType.Bounce ? t.bounceInterceptRadius : t.chestInterceptRadius;
            if (clearance >= radius) return 0f;
            float closeness = 1f - clearance / radius;
            float chance = (t.interceptBase + t.interceptFromDefense * RatingScale.Normalized(defenderDefense)) * (0.5f + 0.5f * closeness)
                           - t.interceptReductionFromPlaymaking * RatingScale.Normalized(passerPlaymaking);
            return Math.Max(0f, Math.Min(0.95f, chance));
        }
    }
}
