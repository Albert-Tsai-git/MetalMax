using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Items;
using Game.Progression;
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

        /// <summary>道具所在的玩家状态；为空时不能使用道具</summary>
        public PlayerState Inventory { get; set; }

        /// <summary>逃跑成功率</summary>
        public float escapeChance = 0.5f;

        private readonly Random _rng;
        private readonly HashSet<Combatant> _defending = new();

        public BattleSystem(List<Combatant> players, List<Combatant> enemies, int seed = 0)
        {
            this.players = players;
            this.enemies = enemies;
            _rng = seed == 0 ? new Random() : new Random(seed);
            AssignGroups();
            Log($"战斗开始！遭遇 {string.Join("、", enemies)}");
        }

        /// <summary>同种敌人（数据 ID 相同）编为一组，按首次出现顺序编号；没有 ID 的单位各自成组</summary>
        private void AssignGroups()
        {
            var order = new List<string>();
            foreach (var e in enemies)
            {
                string key = string.IsNullOrEmpty(e.id) ? $"#{enemies.IndexOf(e)}" : e.id;
                if (!order.Contains(key)) order.Add(key);
                e.groupIndex = order.IndexOf(key);
            }
            foreach (var p in players) p.groupIndex = 0;
        }

        /// <summary>敌方现存的组数</summary>
        public int AliveGroupCount => AliveEnemies.Select(e => e.groupIndex).Distinct().Count();

        /// <summary>按攻击范围展开目标：单体为指定目标，一组为目标所在组的全部存活单位，全体为对方全部</summary>
        private List<Combatant> ExpandTargets(BattleAction a, AttackRange range)
        {
            var foes = a.actor.side == Side.Player ? AliveEnemies.ToList() : AlivePlayers.ToList();
            var main = RetargetIfDead(a);
            return range switch
            {
                AttackRange.Single => new List<Combatant> { main },
                AttackRange.Group => main == null ? new List<Combatant>() : foes.Where(f => f.groupIndex == main.groupIndex).ToList(),
                _ => foes,
            };
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
                    DoHumanAttack(a);
                    break;
                case ActionType.TankWeapon:
                    DoTankWeapon(a);
                    break;
                case ActionType.BoardTank:
                    if (a.actor.CanBoard)
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
                case ActionType.Skill:
                    DoSkill(a);
                    break;
                case ActionType.UseItem:
                    DoUseItem(a);
                    break;
                case ActionType.Repair:
                    DoRepair(a.actor, a.targets.FirstOrDefault());
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

        /// <summary>人类攻击：步行时用手持武器（范围、属性），乘车中不会走到这里</summary>
        private void DoHumanAttack(BattleAction a)
        {
            var actor = a.actor;
            var weapon = actor.side == Side.Player ? actor.GearIn(Game.Equipment.GearSlot.Weapon) : null;
            var range = weapon != null ? weapon.range : AttackRange.Single;
            var element = weapon != null ? weapon.element : Element.Normal;
            int power = actor.side == Side.Player ? actor.FootAttack : actor.attack;
            if (weapon != null) Log($"{actor} 使用 {weapon.DisplayName}！");
            foreach (var target in ExpandTargets(a, range).Where(t => t != null && t.IsAlive))
            {
                if (!DamageCalculator.RollHit(DamageCalculator.HumanBaseAccuracy, target.TotalEvade, _rng))
                {
                    Log($"{actor} 攻击 {target}，没有命中");
                    BattleEvents.RaiseMissed(actor, target);
                    continue;
                }
                ApplyElementDamage(actor, target, power, target.TotalDefense, element);
            }
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

            var targets = ExpandTargets(a, wd.range);

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
                ApplyElementDamage(actor, t, weapon.Attack, t.TotalDefense, wd.element);
            }
        }

        private void DoSkill(BattleAction a)
        {
            var actor = a.actor;
            var skill = a.skill;
            if (skill == null) return;
            Log($"{actor} 使用 {skill.DisplayName}！");
            BattleEvents.RaiseSkillUsed(actor, skill.skillId);

            if (skill.IsHeal)
            {
                int amount = Math.Max(1, actor.maxHp * skill.healPercent / 100);
                int before = actor.hp;
                actor.hp = Math.Min(actor.maxHp, actor.hp + amount);
                Log($"  {actor} 回复 {actor.hp - before} HP");
                BattleEvents.RaiseHealed(actor, actor.hp - before);
                return;
            }

            var targets = ExpandTargets(a, skill.range);
            int atk = actor.attack * skill.power / 100;
            for (int h = 0; h < skill.hits; h++)
            {
                foreach (var t in targets.Where(t => t != null && t.IsAlive))
                {
                    if (!DamageCalculator.RollHit(DamageCalculator.HumanBaseAccuracy + skill.accuracyBonus, t.TotalEvade, _rng))
                    {
                        Log($"  没有命中 {t}");
                        BattleEvents.RaiseMissed(actor, t);
                        continue;
                    }
                    // 穿透战车的技能按乘员自身防御计算
                    int def = skill.pierceTank ? t.defense : t.TotalDefense;
                    ApplyElementDamage(actor, t, atk, def, skill.element, skill.pierceTank, skill.partBreakChance);
                }
            }
        }

        /// <summary>
        /// 战斗中修理：回复目标战车 SP（按修理者职业与等级）；SP 已满且修理者等级足够时，把一个损坏部件修回正常。
        /// 大破部件与失去战斗能力的战车不能在战斗中修理。
        /// </summary>
        private void DoRepair(Combatant actor, Combatant owner)
        {
            var data = GameDB.Character(actor.id);
            int amount = data?.RepairAmount(actor.level) ?? 0;
            var tank = owner?.tank;
            if (amount <= 0 || tank == null || tank.IsDestroyed)
            {
                Log($"{actor} 无法修理");
                return;
            }
            PartInstance fixedPart = null;
            int before = tank.currentSp;
            if (tank.currentSp < tank.MaxSp)
                tank.currentSp = Math.Min(tank.MaxSp, tank.currentSp + amount);
            else if (actor.level >= data.partRepairLevel)
            {
                fixedPart = tank.AllParts().FirstOrDefault(x => x.condition == PartCondition.Damaged);
                fixedPart?.Repair();
            }
            tank.NotifyChanged();
            int sp = tank.currentSp - before;
            Log(fixedPart != null ? $"{actor} 修好了 {fixedPart.data.DisplayName}" : $"{actor} 修理 {tank.tankName}，SP +{sp}");
            BattleEvents.RaiseRepaired(actor, owner, sp, fixedPart);
        }

                private void DoUseItem(BattleAction a)
        {
            if (Inventory == null) { Log("无法使用道具"); return; }
            var target = a.targets.FirstOrDefault();
            var r = ItemService.Use(Inventory, a.itemId, a.actor, target);
            if (r != OpResult.Ok)
            {
                Log($"{a.actor} 的道具没有效果");
                return;
            }
            Log($"{a.actor} 使用了 {GameDB.Item(a.itemId)?.DisplayName}");
            BattleEvents.RaiseItemUsed(a.actor, a.itemId, target);
        }

        /// <summary>计算属性倍率后结算伤害；弱点、抗性、免疫会广播 ElementHit</summary>
        private void ApplyElementDamage(Combatant attacker, Combatant target, int attack, int defense, Element element,
            bool pierceTank = false, int partBreakChance = 0)
        {
            float rate = target.ElementRate(element);
            if (!Mathf.Approximately(rate, 1f))
            {
                Log(rate <= 0f ? $"  {target} 对{element}攻击免疫" : rate > 1f ? "  效果拔群！" : "  效果不佳……");
                BattleEvents.RaiseElementHit(target, element, rate);
            }
            if (rate <= 0f) return;
            ApplyDamage(attacker, target, DamageCalculator.Damage(attack, defense, _rng, rate), pierceTank, partBreakChance);
        }

        /// <summary>
        /// 伤害落点：乘车时打战车，否则打人。pierceTank 时直接打乘员；
        /// partBreakChance 为命中战车时额外损坏部件的概率（百分比）。
        /// </summary>
        private void ApplyDamage(Combatant attacker, Combatant target, int dmg, bool pierceTank = false, int partBreakChance = 0)
        {
            if (_defending.Contains(target)) dmg = Math.Max(1, dmg / 2);

            if (target.IsTankActive && !pierceTank)
            {
                var broken = target.tank.TakeDamage(dmg, _rng);
                if (broken == null && partBreakChance > 0 && _rng.Next(100) < partBreakChance)
                    broken = target.tank.BreakRandomPart(_rng);
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

        /// <summary>每个存活敌人按自身 AI 类型与技能表决定行动</summary>
        private IEnumerable<BattleAction> EnemyAI()
        {
            var targets = AlivePlayers.ToList();
            if (targets.Count == 0) yield break;
            foreach (var e in AliveEnemies)
            {
                var a = Battle.EnemyAI.Decide(e, targets, _rng);
                if (a != null) yield return a;
            }
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
