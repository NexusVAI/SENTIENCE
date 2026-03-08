using System;

namespace GTA5MOD2026
{
    public class NPCNeeds
    {
        public float Safety { get; set; } = 70f;
        public float Social { get; set; } = 50f;
        public float Purpose { get; set; } = 50f;
        public float Curiosity { get; set; } = 50f;
        public float Aggression { get; set; } = 20f;
        public float Energy { get; set; } = 100f;

        public void InitFromPersonality(string personality)
        {
            switch (personality)
            {
                case "友善":
                    Safety = 60f;
                    Social = 80f;
                    Purpose = 60f;
                    Curiosity = 60f;
                    Aggression = 10f;
                    break;
                case "暴躁":
                    Safety = 40f;
                    Social = 30f;
                    Purpose = 50f;
                    Curiosity = 40f;
                    Aggression = 85f;
                    break;
                case "胆小":
                    Safety = 95f;
                    Social = 40f;
                    Purpose = 30f;
                    Curiosity = 20f;
                    Aggression = 5f;
                    break;
                case "搞笑":
                    Safety = 50f;
                    Social = 90f;
                    Purpose = 40f;
                    Curiosity = 80f;
                    Aggression = 15f;
                    break;
                case "冷漠":
                    Safety = 50f;
                    Social = 10f;
                    Purpose = 30f;
                    Curiosity = 15f;
                    Aggression = 30f;
                    break;
            }
        }

        public void Update(PerceptionData perception,
            float deltaTime)
        {
            if (perception.NearbyPedCount == 0)
                Social = Math.Max(0, Social - 0.5f * deltaTime);
            else
                Social = Math.Min(100,
                    Social + 0.3f * deltaTime);

            Purpose = Math.Max(0,
                Purpose - 0.2f * deltaTime);

            if (perception.DangerLevel > 30f)
                Safety = Math.Max(0,
                    Safety - perception.DangerLevel
                        * 0.1f * deltaTime);
            else
                Safety = Math.Min(100,
                    Safety + 1f * deltaTime);

            if (perception.PlayerVisible
                && perception.PlayerDistance < 20f)
                Curiosity = Math.Min(100,
                    Curiosity + 0.5f * deltaTime);

            Energy = Math.Max(0,
                Energy - 0.1f * deltaTime);

            if (perception.DangerLevel < 20f)
                Aggression = Math.Max(
                    GetBaseAggression(),
                    Aggression - 0.3f * deltaTime);
        }

        public void ReactToEvent(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Shooting:
                    Safety -= 30f;
                    Aggression += 10f;
                    break;
                case EventType.Killing:
                    Safety -= 50f;
                    break;
                case EventType.Explosion:
                    Safety -= 40f;
                    Curiosity += 10f;
                    break;
                case EventType.Speeding:
                    Safety -= 10f;
                    Curiosity += 5f;
                    break;
            }

            Safety = Clamp(Safety);
            Social = Clamp(Social);
            Purpose = Clamp(Purpose);
            Curiosity = Clamp(Curiosity);
            Aggression = Clamp(Aggression);
        }

        public string GetMostUrgentNeed()
        {
            if (Energy < 15f)
                return "rest";

            float lowestValue = 100f;
            string urgent = "none";

            if (Safety < lowestValue)
            {
                lowestValue = Safety;
                urgent = "safety";
            }
            if (Social < lowestValue)
            {
                lowestValue = Social;
                urgent = "social";
            }
            if (Purpose < lowestValue)
            {
                lowestValue = Purpose;
                urgent = "purpose";
            }
            if (Energy < 30f && Energy < lowestValue)
            {
                lowestValue = Energy;
                urgent = "rest";
            }

            if (Curiosity > 80f)
                urgent = "curiosity";
            if (Aggression > 80f)
                urgent = "aggression";

            return urgent;
        }

        private float GetBaseAggression()
        {
            return Aggression > 50f ? 30f : 10f;
        }

        private float Clamp(float value)
        {
            return Math.Max(0f, Math.Min(100f, value));
        }

        public override string ToString()
        {
            return $"安全:{Safety:F0} 社交:{Social:F0} " +
                $"目标:{Purpose:F0} 好奇:{Curiosity:F0} " +
                $"攻击:{Aggression:F0} 体力:{Energy:F0}";
        }
    }
}
