using System.Collections.Generic;
using Game.Battle;
using Game.Tank;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 跨场景的全局会话：队伍、金钱、经验，以及野外 ↔ 战斗的切换。
    /// 首次访问 Instance 时自动创建，并设置 DontDestroyOnLoad。
    /// </summary>
    public class GameSession : MonoBehaviour
    {
        public const string BattleSceneName = "Battle";
        public const string FieldSceneName = "Field";

        private static GameSession _instance;
        public static GameSession Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[GameSession]");
                    _instance = go.AddComponent<GameSession>();
                    DontDestroyOnLoad(go);
                    _instance.State.party = DemoFactory.CreateParty();
                    Debug.Log("[Session] 新建会话，初始化演示队伍");
                }
                return _instance;
            }
        }

        /// <summary>玩家可存档状态</summary>
        public PlayerState State { get; private set; } = new(500);

        // I-08 约定的读取入口
        public List<Combatant> party => State.party;
        public int gold { get => State.Gold; set => State.Gold = value; }
        public int exp { get => State.exp; set => State.exp = value; }

        /// <summary>待进入的战斗的敌人列表</summary>
        public List<Combatant> PendingEnemies { get; private set; }

        private string _returnScene;
        private Vector3 _returnPosition;
        private bool _hasReturnPosition;

        /// <summary>从野外进入战斗</summary>
        public void StartBattle(List<Combatant> enemies, Vector3 playerPosition)
        {
            PendingEnemies = enemies;
            _returnScene = ArtSceneLoader.CurrentLogicScene ?? SceneManager.GetActiveScene().name;
            _returnPosition = playerPosition;
            _hasReturnPosition = true;
            Debug.Log($"[Session] 遇敌，从 {_returnScene} 进入战斗");
            SceneManager.LoadScene(BattleSceneName);
        }

        /// <summary>战斗结束：发放奖励并返回野外；全灭时回满状态（原型阶段先这样处理）</summary>
        public void EndBattle(BattleState result, int expGain, int goldGain)
        {
            if (result == BattleState.Victory)
            {
                exp += expGain;
                gold += goldGain;
            }
            else if (result == BattleState.Defeat)
            {
                // TODO: 正式版改为回到最后到过的城镇、扣除一半金钱
                foreach (var p in party) p.hp = p.maxHp;
                gold /= 2;
                Debug.Log("[Session] 全灭：队伍回满，金钱减半");
            }

            // 倒下的角色保留 1 HP，便于原型测试
            foreach (var p in party) if (p.hp <= 0) p.hp = 1;

            PendingEnemies = null;
            Debug.Log($"[Session] 战斗结束 {result}，金钱 {gold}G，经验 {exp}");
            SceneManager.LoadScene(string.IsNullOrEmpty(_returnScene) ? FieldSceneName : _returnScene);
        }

        /// <summary>野外场景加载后，取出应该回到的位置（只取一次）</summary>
        public bool TryConsumeReturnPosition(out Vector3 pos)
        {
            pos = _returnPosition;
            if (!_hasReturnPosition) return false;
            _hasReturnPosition = false;
            return true;
        }
    }

    /// <summary>演示用的初始队伍与战车</summary>
    public static class DemoFactory
    {
        public static List<Combatant> CreateParty() => new()
        {
            new Combatant
            {
                id = "CHR_Hunter", name = TextDB.Name("CHR_Hunter"), side = Side.Player,
                maxHp = 80, hp = 80, attack = 18, defense = 8, speed = 12,
                tank = CreateTank(), inTank = true,
            },
            new Combatant
            {
                id = "CHR_Mechanic", name = TextDB.Name("CHR_Mechanic"), side = Side.Player,
                maxHp = 60, hp = 60, attack = 12, defense = 6, speed = 9,
            },
        };

        /// <summary>初始战车：轻型底盘 + V8 + 基础 C 装置 + 75mm 炮 + 7.7mm 机枪</summary>
        public static TankLoadout CreateTank()
        {
            var tank = new TankLoadout { tankName = "一号车" };
            tank.TryEquip(new PartInstance(GameDB.Part("TNK_Chassis_Light")), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part("TNK_Engine_V8")), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part("TNK_CUnit_Basic")), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part("WPN_Cannon_75")), 0, out _);
            tank.TryEquip(new PartInstance(GameDB.Part("WPN_MG_77")), 1, out _);
            tank.FillArmor();
            return tank;
        }
    }
}
