using System;
using System.Collections.Generic;
using System.Linq;
using Game.Tank;
using UnityEngine;
using Random = System.Random;

namespace Game.Battle
{
    public enum BattleState
    {
        WaitingForCommands, // 等待玩家下达本回合指令
        Resolving,          // 结算中
        Victory,
        Defeat,
        Escaped,
    }

    /// <summary>
    /// 回合制战斗核心。纯逻辑，不依赖场景和 UI：
    /// 1. 外部调用 SubmitCommands 提交玩家指令；
    /// 2. 系统生成敌人指令，按速度排序依次结算；
    /// 3. 通过事件把战斗过程通知给 UI / 演出层。
    /// </summary>
    public class BattleSystem
    {
        public readonly List<Combatant> players;
        public readonly List<Combatant> enemies;
        public BattleState State { get; private set; } = BattleState.WaitingForCommands;
        public int Turn { get; private set; } = 1;

        /// <summary>战斗日志，UI 可以直接逐行显示</summary>
        public event Action<string> OnLog;
        /// <summary>单次伤害：攻击者、目标、伤害值、是否打在战车上</summary>
        public event Action<Combatant, Combatant, int, bool> OnDamage;
        public event Action<BattleState> OnBattleEnd;

        /// <summary>逃跑成功率</summary>
        public float escapeChance = 0.5f;

        private readonly Random _rng;
        private readonly HashSet<Combatant> _defending = new();

        public BattleSystem(List<Combatant> players, List<Combatant> enemies, int seed = 0)
        {
            this.players = players;
            this.enemies = enemies;
            _rng = seed == 0 ? new Random() : new Random(seed);
            Log($"战斗开始！遭遇 {string.Join("、", enemies)}");
        }

        /// <summary>广播战斗开始；由创建方在订阅完成后调用</summary>
        public void Begin() => BattleEvents.RaiseStarted(this);

        public IEnumerable<Combatant> AlivePlayers => players.Where(p => p.IsAlive);
        public IEnumerable<Combatant> AliveEnemies => enemies.Where(e => e.IsAlive);

        /// <summary>提交玩家本回合的全部指令，然后结算整个回合</summary>
        public void SubmitCommands(List<BattleAction> playerActions)
        {
            if (State != BattleState.WaitingForCommands) return;
            State = BattleState.Resolving;
            Log($"—— 第 {Turn} 回合 ——");
            BattleEvents.RaiseTurnStarted(Turn);

            var actions = new List<BattleAction>(playerActions);
            actions.AddRange(EnemyAI());

            // 按速度排序，速度相同时随机
            var ordered = actions
                .OrderByDescending(a => a.actor.speed)
                .ThenBy(_ => _rng.Next())
                .ToList();

            _defending.Clear();
            foreach (var a in ordered.Where(a => a.type == ActionType.Defend)) _defending.Add(a.actor);

            foreach (var action in ordered)
            {
                if (!action.actor.CanAct) continue;
                Execute(action);
                if (CheckEnd()) return;
            }

            Turn++;
            State = BattleState.WaitingForCommands;
        }

        #region 行动结算

        private void Execute(BattleAction a)
        {
            BattleEvents.RaiseActionStarted(a.actor, a.type, a.type == ActionType.TankWeapon ? a.weapon : null);
            switch (a.type)
            {
                case ActionType.HumanAttack:
                    DoHumanAttack(a.actor, RetargetIfDead(a));
                    break;
                case ActionType.TankWeapon:
                    DoTankWeapon(a);
                    break;
                case ActionType.BoardTank:
                    if (a.actor.tank != null && !a.actor.tank.IsDestroyed)
                    {
                        a.actor.inTank = true;
                        Log($"{a.actor} 登上了 {a.actor.tank.tankName}");
                        BattleEvents.RaiseBoardChanged(a.actor, true);
                    }
                    break;
                case ActionType.LeaveTank:
                    a.actor.inTank = false;
                    Log($"{a.actor} 下了车");
                    BattleEvents.RaiseBoardChanged(a.actor, false);
                    break;
                case ActionType.Defend:
                    Log($"{a.actor} 进入防御姿态");
                    break;
                case ActionType.Escape:
                    bool escaped = _rng.NextDouble() < escapeChance;
                    BattleEvents.RaiseEscapeAttempted(a.actor, escaped);
                    if (escaped)
                    {
                        Log("成功逃跑！");
                        End(BattleState.Escaped);
                    }
                    else Log("逃跑失败！");
                    break;
            }
        }

        private void DoHumanAttack(Combatant actor, Combatant target)
        {
            if (target == null) return;
            if (!DamageCalculator.RollHit(DamageCalculator.HumanBaseAccuracy, target.TotalEvade, _rng))
            {
                Log($"{actor} 攻击 {target}，没有命中");
                BattleEvents.RaiseMissed(actor, target);
                return;
            }
            int dmg = DamageCalculator.Damage(actor.attack, target.TotalDefense, _rng);
            ApplyDamage(actor, target, dmg);
        }

        private void DoTankWeapon(BattleAction a)
        {
            var actor = a.actor;
            var weapon = a.weapon;
            // 执行时可能已经下车、武器已损坏或弹药耗尽
            if (!actor.IsTankActive || weapon == null || !weapon.IsFunctional || !weapon.HasAmmo)
            {
                Log($"{actor} 的武器无法使用");
                return;
            }

            var wd = (WeaponData)weapon.data;
            if (wd.maxAmmo >= 0) weapon.currentAmmo--;

            // 根据攻击范围展开目标
            var foes = actor.side == Side.Player ? AliveEnemies.ToList() : AlivePlayers.ToList();
            var targets = wd.range switch
            {
                AttackRange.Single => new List<Combatant> { RetargetIfDead(a) },
                AttackRange.Group => foes.Take(3).ToList(),
                _ => foes,
            };

            Log($"{actor} 发射 {wd.DisplayName}！");
            int acc = DamageCalculator.WeaponAccuracy(actor, weapon);
            foreach (var t in targets.Where(t => t != null && t.IsAlive))
            {
                if (!DamageCalculator.RollHit(acc, t.TotalEvade, _rng))
                {
                    Log($"  没有命中 {t}");
                    BattleEvents.RaiseMissed(actor, t);
                    continue;
                }
                ApplyDamage(actor, t, DamageCalculator.Damage(weapon.Attack, t.TotalDefense, _rng));
            }
        }

        /// <summary>伤害落点：乘车时打战车，否则打人</summary>
        private void ApplyDamage(Combatant attacker, Combatant target, int dmg)
        {
            if (_defending.Contains(target)) dmg = Math.Max(1, dmg / 2);

            if (target.IsTankActive)
            {
                var broken = target.tank.TakeDamage(dmg, _rng);
                Log($"  {target} 的战车受到 {dmg} 伤害，SP 剩余 {target.tank.currentSp}");
                OnDamage?.Invoke(attacker, target, dmg, true);
                BattleEvents.RaiseHit(attacker, target, dmg, true);
                if (broken != null)
                {
                    Log($"  {broken.data.DisplayName} {(broken.condition == PartCondition.Broken ? "大破" : "损坏")}！");
                    BattleEvents.RaisePartDamaged(target, broken);
                }
                if (target.tank.IsDestroyed)
                {
                    target.inTank = false;
                    Log($"  {target.tank.tankName} 失去战斗能力，{target} 被迫下车！");
                    BattleEvents.RaiseTankDisabled(target);
                }
            }
            else
            {
                target.hp = Math.Max(0, target.hp - dmg);
                Log($"  {target} 受到 {dmg} 伤害，HP {target.hp}/{target.maxHp}");
                OnDamage?.Invoke(attacker, target, dmg, false);
                BattleEvents.RaiseHit(attacker, target, dmg, false);
                if (!target.IsAlive)
                {
                    Log($"  {target} 倒下了！");
                    BattleEvents.RaiseDefeated(target);
                }
            }
        }

        /// <summary>原目标已死亡时，自动改打另一个存活目标</summary>
        private Combatant RetargetIfDead(BattleAction a)
        {
            var t = a.targets.FirstOrDefault();
            if (t != null && t.IsAlive) return t;
            var foes = a.actor.side == Side.Player ? AliveEnemies : AlivePlayers;
            return foes.FirstOrDefault();
        }

        #endregion

        #region 敌人 AI

        /// <summary>最简单的 AI：随机攻击一名存活玩家。后续可以按敌人类型扩展。</summary>
        private IEnumerable<BattleAction> EnemyAI()
        {
            var targets = AlivePlayers.ToList();
            if (targets.Count == 0) yield break;
            foreach (var e in AliveEnemies)
                yield return BattleAction.Attack(e, targets[_rng.Next(targets.Count)]);
        }

        #endregion

        #region 胜负判定与结算

        private bool CheckEnd()
        {
            if (State is BattleState.Escaped) return true;
            if (!AliveEnemies.Any()) { End(BattleState.Victory); return true; }
            if (!AlivePlayers.Any()) { End(BattleState.Defeat); return true; }
            return false;
        }

        private void End(BattleState result)
        {
            State = result;
            if (result == BattleState.Victory)
            {
                Log($"胜利！获得经验 {TotalExp}、金钱 {TotalGold}G");
            }
            else if (result == BattleState.Defeat)
            {
                Log("全灭……");
            }
            OnBattleEnd?.Invoke(result);
            bool win = result == BattleState.Victory;
            BattleEvents.RaiseEnded(result, win ? TotalExp : 0, win ? TotalGold : 0);
        }

        public int TotalExp => enemies.Sum(e => e.expReward);
        public int TotalGold => enemies.Sum(e => e.goldReward);

        #endregion

        private void Log(string msg)
        {
            Debug.Log($"[Battle] {msg}");
            OnLog?.Invoke(msg);
        }
    }
}
