using System;
using Game.Tank;

namespace Game.Battle
{
    /// <summary>
    /// 伤害与命中公式，集中放在这里方便调数值。
    /// 伤害 = (攻击 - 防御 / 2) × 随机(0.9~1.1)，最低 1。
    /// </summary>
    public static class DamageCalculator
    {
        public const float RandomMin = 0.9f;
        public const float RandomMax = 1.1f;
        public const int HumanBaseAccuracy = 90;

        /// <summary>伤害 = (攻击 - 防御 / 2) × 随机 × 属性倍率；倍率为 0（免疫）时伤害为 0，否则最低 1</summary>
        public static int Damage(int attack, int defense, Random rng, float elementRate = 1f)
        {
            if (elementRate <= 0f) return 0;
            float raw = attack - defense / 2f;
            float rand = RandomMin + (float)rng.NextDouble() * (RandomMax - RandomMin);
            return Math.Max(1, (int)(raw * rand * elementRate));
        }

        /// <summary>命中率 = 基础命中 + C 装置加成 - 目标回避，限制在 5%~99%</summary>
        public static bool RollHit(int accuracy, int evade, Random rng)
        {
            int chance = Math.Clamp(accuracy - evade, 5, 99);
            return rng.Next(100) < chance;
        }

        /// <summary>战车武器的实际命中</summary>
        public static int WeaponAccuracy(Combatant actor, PartInstance weapon)
        {
            int acc = ((WeaponData)weapon.data).accuracy;
            if (actor.tank?.cUnit is { IsFunctional: true } c)
                acc += (int)(((CUnitData)c.data).hitBonus * c.PerformanceRate);
            return acc;
        }
    }
}
