using System.Collections.Generic;
using Game.Battle;
using Game.Core.Save;
using Game.Tank;
using Game.Progression;
using Game.Story;
using Game.Town;
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
        public const string PumpStationSceneName = "Dungeon_PumpStation";
        public const string SaltBeltSceneName = "Field_SaltBelt";
        public const string GhostCitySceneName = "Dungeon_GhostCity";

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
            // 步行时遇敌：战车停在别处，全员步行作战
            bool onFoot = State.vehicle.parked;
            foreach (var p in party)
            {
                p.tankAway = onFoot;
                if (onFoot) p.inTank = false;
            }
            _returnScene = ArtSceneLoader.CurrentLogicScene ?? SceneManager.GetActiveScene().name;
            _returnPosition = playerPosition;
            _hasReturnPosition = true;
            Debug.Log($"[Session] 遇敌，从 {_returnScene} 进入战斗");
            SceneManager.LoadScene(BattleSceneName);
        }

        /// <summary>全灭后待复活的城镇 ID，野外场景加载时取用</summary>
        private string _respawnTown;

        /// <summary>
        /// 战斗结束：胜利发放奖励并登记赏金首；全灭时全员复活、金钱减半，回到最后到访城镇。
        /// </summary>
        public void EndBattle(BattleState result, int expGain, int goldGain)
        {
            if (result == BattleState.Victory)
            {
                LevelService.AwardBattleExp(State, expGain);
                gold += goldGain;
                if (PendingEnemies != null) BountyService.OnVictory(State, PendingEnemies);
            }
            else if (result == BattleState.Defeat)
            {
                foreach (var p in party) p.hp = p.maxHp;
                gold /= 2;
                _respawnTown = State.lastTown;
                // 回城复活时战车一并拖回（原作中由拖车服务找回战车）
                State.vehicle = new VehicleState();
                var owner = party.Find(p => p.tank != null);
                if (owner != null) owner.inTank = true;
                _hasReturnPosition = false;
                Debug.Log($"[Session] 全灭：金钱减半，回到 {_respawnTown}");
            }

            PendingEnemies = null;
            foreach (var p in party) p.tankAway = false;
            StoryService.Refresh(State);
            Debug.Log($"[Session] 战斗结束 {result}，金钱 {gold}G，经验 {exp}");
            // 全灭时回到最后到访城镇入口所在的野外场景
            string scene = result == BattleState.Defeat ? TownFieldScene(_respawnTown)
                : string.IsNullOrEmpty(_returnScene) ? FieldSceneName : _returnScene;
            SceneManager.LoadScene(scene);
        }

        /// <summary>城镇入口所在的野外场景（未配置时为 Field）</summary>
        public static string TownFieldScene(string townId)
        {
            string s = GameDB.Town(townId)?.fieldScene;
            return string.IsNullOrEmpty(s) ? FieldSceneName : s;
        }

        /// <summary>野外场景加载后，取出全灭后应复活的城镇（只取一次）</summary>
        public bool TryConsumeRespawnTown(out string townId)
        {
            townId = _respawnTown;
            _respawnTown = null;
            return !string.IsNullOrEmpty(townId);
        }

        /// <summary>存档：记录当前逻辑场景与玩家位置</summary>
        public OpResult SaveGame(int slot, Vector3 playerPosition)
        {
            string scene = ArtSceneLoader.CurrentLogicScene ?? SceneManager.GetActiveScene().name;
            return SaveSystem.Save(slot, State, scene, playerPosition);
        }

        /// <summary>读档：替换玩家状态并回到存档时的场景与位置</summary>
        public OpResult LoadGame(int slot)
        {
            var r = SaveSystem.Load(slot, out var data, out var state);
            if (r != OpResult.Ok) return r;
            State = state;
            PendingEnemies = null;
            StoryService.AbortDialogue();
            StoryService.Refresh(State);
            _returnPosition = data.position;
            _hasReturnPosition = true;
            SceneManager.LoadScene(string.IsNullOrEmpty(data.scene) ? FieldSceneName : data.scene);
            return r;
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
        /// <summary>初始队伍：猎人（乘一号车）与机械师，属性来自 characters.csv</summary>
        public static List<Combatant> CreateParty()
        {
            var hunter = GameDB.Character("CHR_Hunter").CreateCombatant();
            hunter.tank = CreateTank();
            hunter.inTank = true;
            var mechanic = GameDB.Character("CHR_Mechanic").CreateCombatant();
            return new List<Combatant> { hunter, mechanic };
        }

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
