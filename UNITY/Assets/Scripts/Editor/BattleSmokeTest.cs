using System.Linq;
using Game.Battle;
using Game.Core;
using Game.Tank;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 战斗冒烟测试：不进入 Play 模式，用固定种子连续自动打多场，检查流程能否正常结束。
    /// 命令行：Unity -batchmode -executeMethod Game.EditorTools.BattleSmokeTest.Run
    /// </summary>
    public static class BattleSmokeTest
    {
        [MenuItem("Game/战斗冒烟测试")]
        public static void Run()
        {
            int victory = 0, defeat = 0, escaped = 0, stuck = 0;
            // 统计对外事件，确认表现层能收到
            int started = 0, turns = 0, hits = 0, misses = 0, defeated = 0, ended = 0;
            System.Action<BattleSystem> onStart = _ => started++;
            System.Action<int> onTurn = _ => turns++;
            System.Action<Combatant, Combatant, int, bool> onHit = (_, _, _, _) => hits++;
            System.Action<Combatant, Combatant> onMiss = (_, _) => misses++;
            System.Action<Combatant> onDefeat = _ => defeated++;
            System.Action<BattleState, int, int> onEnd = (_, _, _) => ended++;
            BattleEvents.Started += onStart; BattleEvents.TurnStarted += onTurn; BattleEvents.Hit += onHit;
            BattleEvents.Missed += onMiss; BattleEvents.Defeated += onDefeat; BattleEvents.Ended += onEnd;
            for (int seed = 1; seed <= 20; seed++)
            {
                var party = DemoFactory.CreateParty();
                var enemies = new[]
                {
                    AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Resources/GameData/Enemies/ENM_Ant.asset"),
                    AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Resources/GameData/Enemies/ENM_TurretBug.asset"),
                }.Where(e => e != null).Select((e, i) => e.CreateCombatant($" {i}")).ToList();

                var battle = new BattleSystem(party, enemies, seed);
                battle.Begin();
                while (battle.State == BattleState.WaitingForCommands && battle.Turn <= 100)
                {
                    var cmds = battle.AlivePlayers.Select(p =>
                    {
                        var target = battle.AliveEnemies.First();
                        var weapon = p.IsTankActive
                            ? p.tank.weapons.FirstOrDefault(w => w != null && w.IsFunctional && w.HasAmmo)
                            : null;
                        return weapon != null ? BattleAction.Fire(p, weapon, target) : BattleAction.Attack(p, target);
                    }).ToList();
                    battle.SubmitCommands(cmds);
                }

                switch (battle.State)
                {
                    case BattleState.Victory: victory++; break;
                    case BattleState.Defeat: defeat++; break;
                    case BattleState.Escaped: escaped++; break;
                    default: stuck++; break;
                }
            }
            BattleEvents.Started -= onStart; BattleEvents.TurnStarted -= onTurn; BattleEvents.Hit -= onHit;
            BattleEvents.Missed -= onMiss; BattleEvents.Defeated -= onDefeat; BattleEvents.Ended -= onEnd;
            Debug.Log($"[SmokeTest] 20 场：胜 {victory} / 败 {defeat} / 逃 {escaped} / 超时 {stuck}");
            Debug.Log($"[SmokeTest] 事件：开始 {started} / 回合 {turns} / 命中 {hits} / 未中 {misses} / 倒下 {defeated} / 结束 {ended}");
            if (started != 20 || ended != 20 - stuck || hits == 0)
                throw new System.Exception("[SmokeTest] 战斗事件计数异常");
        }
    }
}
