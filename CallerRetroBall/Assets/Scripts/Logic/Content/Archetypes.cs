using System.Collections.Generic;

namespace CallerRetroBall.Logic
{
    /// <summary>The 12 original player archetypes: ratings baselines, copy, and AI tendencies.</summary>
    public static class Archetypes
    {
        public static string IdFor(Archetype a) => "archetype." + a.ToString().ToLowerInvariant();

        public static List<ArchetypeDef> CreateAll()
        {
            var list = new List<ArchetypeDef>
            {
                Make(Archetype.FloorGeneral, "Floor General",
                    "Organises the half-court, rarely turns it over, and wants the ball when the clock is short.",
                    new[] { AttributeType.Playmaking, AttributeType.Clutch }, new[] { AttributeType.Rebounding, AttributeType.Finishing },
                    new AttributeSet(55, 68, 82, 58, 45, 66, 72, 76),
                    new AiTendencies(0.35f, 0.35f, 0.85f, 0.25f, 0.10f, 0.15f, 0.45f, 0.25f, 0.60f)),

                Make(Archetype.DeepShooter, "Deep Shooter",
                    "Lives beyond the arc. Needs a screen or a kick-out, but punishes every sag.",
                    new[] { AttributeType.Shooting, AttributeType.Clutch }, new[] { AttributeType.Finishing, AttributeType.Defense },
                    new AttributeSet(50, 88, 60, 52, 45, 68, 68, 75),
                    new AiTendencies(0.80f, 0.20f, 0.45f, 0.35f, 0.10f, 0.10f, 0.30f, 0.20f, 0.95f)),

                Make(Archetype.RimRunner, "Rim Runner",
                    "Sprints the lane, rolls hard off screens, and finishes anything near the rim.",
                    new[] { AttributeType.Finishing, AttributeType.Speed }, new[] { AttributeType.Shooting, AttributeType.Playmaking },
                    new AttributeSet(84, 40, 45, 62, 72, 78, 80, 55),
                    new AiTendencies(0.50f, 0.60f, 0.30f, 0.80f, 0.60f, 0.70f, 0.50f, 0.20f, 0.05f)),

                Make(Archetype.LockdownWing, "Lockdown Wing",
                    "Takes the toughest assignment every trip and turns defense into offense.",
                    new[] { AttributeType.Defense, AttributeType.Stamina }, new[] { AttributeType.Playmaking, AttributeType.Finishing },
                    new AttributeSet(58, 60, 52, 88, 60, 74, 78, 60),
                    new AiTendencies(0.40f, 0.35f, 0.50f, 0.45f, 0.30f, 0.40f, 0.80f, 0.25f, 0.60f)),

                Make(Archetype.GlassCleaner, "Glass Cleaner",
                    "Every miss is a second chance. Boxes out, tips out, and screens without complaint.",
                    new[] { AttributeType.Rebounding, AttributeType.Defense }, new[] { AttributeType.Shooting, AttributeType.Speed },
                    new AttributeSet(70, 38, 45, 70, 90, 52, 75, 55),
                    new AiTendencies(0.35f, 0.30f, 0.45f, 0.50f, 0.75f, 0.95f, 0.70f, 0.10f, 0.10f)),

                Make(Archetype.TwoWaySpark, "Two-Way Spark",
                    "Solid at everything, great at effort. Swings momentum on both ends.",
                    new[] { AttributeType.Defense, AttributeType.Stamina }, new[] { AttributeType.Clutch },
                    new AttributeSet(68, 66, 62, 75, 58, 76, 74, 58),
                    new AiTendencies(0.50f, 0.50f, 0.50f, 0.50f, 0.30f, 0.45f, 0.60f, 0.45f, 0.50f)),

                Make(Archetype.PostAnchor, "Post Anchor",
                    "Owns the paint on both ends: strong finishes inside, walls up at the rim.",
                    new[] { AttributeType.Finishing, AttributeType.Rebounding }, new[] { AttributeType.Speed, AttributeType.Shooting },
                    new AttributeSet(82, 45, 55, 78, 80, 45, 70, 62),
                    new AiTendencies(0.55f, 0.45f, 0.45f, 0.30f, 0.80f, 0.75f, 0.85f, 0.10f, 0.10f)),

                Make(Archetype.QuickCutter, "Quick Cutter",
                    "Never stands still. Backdoors sleepy defenders and finishes on the move.",
                    new[] { AttributeType.Speed, AttributeType.Finishing }, new[] { AttributeType.Rebounding, AttributeType.Defense },
                    new AttributeSet(76, 58, 58, 58, 48, 88, 78, 60),
                    new AiTendencies(0.45f, 0.55f, 0.45f, 0.95f, 0.20f, 0.30f, 0.35f, 0.40f, 0.30f)),

                Make(Archetype.Playmaker, "Playmaker",
                    "Creative downhill passer. Collapses the defense and finds the open teammate.",
                    new[] { AttributeType.Playmaking, AttributeType.Speed }, new[] { AttributeType.Defense, AttributeType.Shooting },
                    new AttributeSet(70, 58, 88, 55, 50, 80, 70, 60),
                    new AiTendencies(0.30f, 0.70f, 0.80f, 0.30f, 0.15f, 0.20f, 0.35f, 0.35f, 0.40f)),

                Make(Archetype.ShotCreator, "Shot Creator",
                    "Makes something out of nothing off the dribble, especially late in the clock.",
                    new[] { AttributeType.Shooting, AttributeType.Clutch }, new[] { AttributeType.Defense, AttributeType.Rebounding },
                    new AttributeSet(72, 80, 62, 50, 48, 74, 66, 82),
                    new AiTendencies(0.85f, 0.60f, 0.30f, 0.25f, 0.10f, 0.20f, 0.30f, 0.30f, 0.55f)),

                Make(Archetype.HustleGuard, "Hustle Guard",
                    "Pressures full-time, dives for loose balls, and never runs out of gas.",
                    new[] { AttributeType.Stamina, AttributeType.Defense }, new[] { AttributeType.Shooting, AttributeType.Finishing },
                    new AttributeSet(56, 55, 62, 78, 62, 82, 90, 55),
                    new AiTendencies(0.30f, 0.45f, 0.60f, 0.50f, 0.25f, 0.50f, 0.70f, 0.75f, 0.40f)),

                Make(Archetype.StretchForward, "Stretch Forward",
                    "A big who spaces the floor: pops off screens for arc shots and still rebounds.",
                    new[] { AttributeType.Shooting, AttributeType.Rebounding }, new[] { AttributeType.Speed, AttributeType.Playmaking },
                    new AttributeSet(60, 80, 52, 62, 70, 56, 68, 62),
                    new AiTendencies(0.60f, 0.25f, 0.45f, 0.30f, 0.60f, 0.55f, 0.55f, 0.15f, 0.85f)),
            };
            return list;
        }

        private static ArchetypeDef Make(Archetype a, string name, string description,
                                         AttributeType[] strengths, AttributeType[] weaknesses,
                                         AttributeSet baseline, AiTendencies ai)
        {
            return new ArchetypeDef
            {
                id = IdFor(a),
                archetype = a,
                displayName = name,
                description = description,
                strengths = new List<AttributeType>(strengths),
                weaknesses = new List<AttributeType>(weaknesses),
                baseline = baseline,
                ai = ai,
            };
        }
    }
}
