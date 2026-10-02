using System;

namespace CallerRetroBall.Logic
{
    /// <summary>The eight player-facing attribute categories.</summary>
    public enum AttributeType
    {
        Finishing = 0,
        Shooting = 1,
        Playmaking = 2,
        Defense = 3,
        Rebounding = 4,
        Speed = 5,
        Stamina = 6,
        Clutch = 7,
    }

    /// <summary>Internal 1–99 rating scale.</summary>
    public static class RatingScale
    {
        public const int Min = 1;
        public const int Max = 99;
        public const int AttributeCount = 8;

        public static bool IsValid(int rating) => rating >= Min && rating <= Max;

        public static int Clamp(int rating) => rating < Min ? Min : (rating > Max ? Max : rating);

        /// <summary>Normalises a rating to 0..1 for use in formulas.</summary>
        public static float Normalized(int rating) => (Clamp(rating) - Min) / (float)(Max - Min);
    }

    /// <summary>
    /// A full set of attribute ratings. Plain serializable fields so Unity can
    /// show and save it inside ScriptableObjects.
    /// </summary>
    [Serializable]
    public struct AttributeSet
    {
        public int finishing;
        public int shooting;
        public int playmaking;
        public int defense;
        public int rebounding;
        public int speed;
        public int stamina;
        public int clutch;

        public AttributeSet(int finishing, int shooting, int playmaking, int defense,
                            int rebounding, int speed, int stamina, int clutch)
        {
            this.finishing = finishing;
            this.shooting = shooting;
            this.playmaking = playmaking;
            this.defense = defense;
            this.rebounding = rebounding;
            this.speed = speed;
            this.stamina = stamina;
            this.clutch = clutch;
        }

        public int Get(AttributeType type)
        {
            switch (type)
            {
                case AttributeType.Finishing: return finishing;
                case AttributeType.Shooting: return shooting;
                case AttributeType.Playmaking: return playmaking;
                case AttributeType.Defense: return defense;
                case AttributeType.Rebounding: return rebounding;
                case AttributeType.Speed: return speed;
                case AttributeType.Stamina: return stamina;
                case AttributeType.Clutch: return clutch;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public AttributeSet With(AttributeType type, int value)
        {
            var copy = this;
            switch (type)
            {
                case AttributeType.Finishing: copy.finishing = value; break;
                case AttributeType.Shooting: copy.shooting = value; break;
                case AttributeType.Playmaking: copy.playmaking = value; break;
                case AttributeType.Defense: copy.defense = value; break;
                case AttributeType.Rebounding: copy.rebounding = value; break;
                case AttributeType.Speed: copy.speed = value; break;
                case AttributeType.Stamina: copy.stamina = value; break;
                case AttributeType.Clutch: copy.clutch = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
            return copy;
        }

        /// <summary>Adds a signed offset to every attribute, clamped to the rating scale.</summary>
        public AttributeSet Offset(int delta)
        {
            var result = this;
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var t = (AttributeType)i;
                result = result.With(t, RatingScale.Clamp(Get(t) + delta));
            }
            return result;
        }

        /// <summary>Simple unweighted overall used for UI display only.</summary>
        public int Overall
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < RatingScale.AttributeCount; i++) sum += Get((AttributeType)i);
                return (int)Math.Round(sum / (double)RatingScale.AttributeCount);
            }
        }

        public bool AllInRange(out AttributeType firstInvalid)
        {
            for (int i = 0; i < RatingScale.AttributeCount; i++)
            {
                var t = (AttributeType)i;
                if (!RatingScale.IsValid(Get(t)))
                {
                    firstInvalid = t;
                    return false;
                }
            }
            firstInvalid = AttributeType.Finishing;
            return true;
        }
    }
}
