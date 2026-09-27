using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Tank;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// 战斗场景入口 + 原型 UI（IMGUI）。
    /// 从 GameSession 取队伍和敌人，创建 BattleSystem；玩家逐个选指令和目标，全员选完后结算。
    /// 直接运行 Battle 场景（没有从野外进入）时，会使用演示敌人。
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Tooltip("日志最多显示的行数")]
        public int maxLogLines = 14;
        [Tooltip("结束后停留几秒再返回野外")]
        public float returnDelay = 2f;

        private BattleSystem _battle;
        private readonly List<string> _log = new();
        private readonly List<BattleAction> _pending = new();
        private List<Combatant> _commandQueue = new();

        // 当前正在选指令的角色及中间状态
        private Combatant _current;
        private ActionType? _chosenType;
        private PartInstance _chosenWeapon;
        private string _chosenItem;
        private float _endTimer = -1f;

        private GUIStyle _box, _btn, _label;

        private void Start()
        {
            var session = GameSession.Instance;
            var enemies = session.PendingEnemies ?? new List<Combatant>
            {
                new() { id = "ENM_Ant", name = TextDB.Name("ENM_Ant") + " A", side = Side.Enemy, maxHp = 40, hp = 40, attack = 22, defense = 6, speed = 11, expReward = 10, goldReward = 20 },
                new() { id = "ENM_TurretBug", name = TextDB.Name("ENM_TurretBug"), side = Side.Enemy, maxHp = 90, hp = 90, attack = 35, defense = 15, speed = 5, expReward = 25, goldReward = 60 },
            };

            _battle = new BattleSystem(session.party, enemies) { Inventory = session.State };
            _battle.OnLog += AddLog;
            AddLog($"战斗开始！遭遇 {string.Join("、", enemies)}");
            _battle.OnBattleEnd += _ => _endTimer = returnDelay;
            _battle.Begin();
            BeginCommandPhase();
        }

        private void OnDestroy()
        {
            if (_battle != null) _battle.OnLog -= AddLog;
        }

        private void Update()
        {
            if (_endTimer < 0f) return;
            _endTimer -= Time.deltaTime;
            if (_endTimer < 0f)
                GameSession.Instance.EndBattle(_battle.State, _battle.TotalExp, _battle.TotalGold);
        }

        private void AddLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > maxLogLines) _log.RemoveRange(0, _log.Count - maxLogLines);
        }

        #region 指令流程

        private void BeginCommandPhase()
        {
            _pending.Clear();
            _commandQueue = _battle.AlivePlayers.ToList();
            NextActor();
        }

        private void NextActor()
        {
            _chosenType = null;
            _chosenWeapon = null;
            if (_commandQueue.Count == 0)
            {
                _current = null;
                _battle.SubmitCommands(new List<BattleAction>(_pending));
                if (_battle.State == BattleState.WaitingForCommands) BeginCommandPhase();
                return;
            }
            _current = _commandQueue[0];
            _commandQueue.RemoveAt(0);
        }

        private void Commit(BattleAction a)
        {
            _pending.Add(a);
            NextActor();
        }

        /// <summary>撤回上一名角色的指令</summary>
        private void Undo()
        {
            if (_pending.Count == 0) return;
            var last = _pending[^1];
            _pending.RemoveAt(_pending.Count - 1);
            if (_current != null) _commandQueue.Insert(0, _current);
            _current = last.actor;
            _chosenType = null;
            _chosenWeapon = null;
        }

        #endregion

        #region IMGUI 绘制

        private void InitStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 16, wordWrap = true };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 18 };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        }

        private void OnGUI()
        {
            if (_battle == null) return;
            InitStyles();
            float w = Screen.width, h = Screen.height;

            // 顶部：敌人
            GUILayout.BeginArea(new Rect(10, 10, w - 20, 60));
            GUILayout.BeginHorizontal();
            foreach (var e in _battle.enemies)
            {
                string c = e.IsAlive ? "white" : "grey";
                GUILayout.Label($"<color={c}><b>{e.name}</b>  HP {e.hp}/{e.maxHp}</color>", _label, GUILayout.Width(220));
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // 中部：日志
            GUI.Box(new Rect(10, 70, w - 20, h - 330), string.Join("\n", _log), _box);

            // 底部左：我方状态
            GUILayout.BeginArea(new Rect(10, h - 250, w * 0.5f - 15, 240), _box);
            foreach (var p in _battle.players) DrawPlayerStatus(p);
            GUILayout.EndArea();

            // 底部右：指令菜单
            GUILayout.BeginArea(new Rect(w * 0.5f + 5, h - 250, w * 0.5f - 15, 240), _box);
            DrawCommandMenu();
            GUILayout.EndArea();
        }

        private void DrawPlayerStatus(Combatant p)
        {
            string mark = p == _current ? "▶ " : "   ";
            string line = $"{mark}<b>{p.name}</b>  HP {p.hp}/{p.maxHp}";
            if (p.tank != null)
            {
                var t = p.tank;
                string where = p.IsTankActive ? "乘车" : "步行";
                line += $"  [{where}] {t.tankName} SP {t.currentSp}/{t.MaxSp}";
                var broken = t.AllParts().Where(x => x.condition != PartCondition.Normal)
                    .Select(x => $"{x.data.DisplayName}{(x.condition == PartCondition.Broken ? "大破" : "损坏")}");
                var s = string.Join(" ", broken);
                if (s.Length > 0) line += $"\n      <color=orange>{s}</color>";
            }
            GUILayout.Label(line, _label);
        }

        private void DrawCommandMenu()
        {
            if (_battle.State != BattleState.WaitingForCommands)
            {
                GUILayout.Label(_battle.State switch
                {
                    BattleState.Victory => $"<b>胜利！</b> 经验 +{_battle.TotalExp}  金钱 +{_battle.TotalGold}G",
                    BattleState.Defeat => "<b>全灭……</b>",
                    BattleState.Escaped => "<b>逃跑成功</b>",
                    _ => "",
                }, _label);
                return;
            }
            if (_current == null) return;

            GUILayout.Label($"<b>{_current.name}</b> 的行动：", _label);

            // 第二步（道具）：选我方目标
            if (_chosenType == ActionType.UseItem)
            {
                GUILayout.Label("选择对象：", _label);
                foreach (var p in _battle.players)
                {
                    if (!GUILayout.Button($"{p.name} (HP {p.hp}/{p.maxHp})", _btn)) continue;
                    Commit(BattleAction.Item(_current, _chosenItem, p));
                    return;
                }
                if (GUILayout.Button("返回", _btn)) { _chosenType = null; _chosenItem = null; }
                return;
            }

            // 第二步：选目标
            if (_chosenType is ActionType.HumanAttack or ActionType.TankWeapon)
            {
                GUILayout.Label("选择目标：", _label);
                foreach (var e in _battle.AliveEnemies)
                {
                    if (!GUILayout.Button($"[{(char)('A' + e.groupIndex)}组] {e.name} (HP {e.hp})", _btn)) continue;
                    Commit(_chosenType == ActionType.TankWeapon
                        ? BattleAction.Fire(_current, _chosenWeapon, e)
                        : BattleAction.Attack(_current, e));
                    return;
                }
                if (GUILayout.Button("返回", _btn)) { _chosenType = null; _chosenWeapon = null; }
                return;
            }

            // 第一步：选指令
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            if (_current.IsTankActive)
            {
                foreach (var wp in _current.tank.weapons.Where(x => x != null))
                {
                    var wd = (WeaponData)wp.data;
                    string ammo = wd.maxAmmo < 0 ? "∞" : $"{wp.currentAmmo}/{wd.maxAmmo}";
                    GUI.enabled = wp.IsFunctional && wp.HasAmmo;
                    if (GUILayout.Button($"{wd.DisplayName}  [{ammo}]", _btn))
                    {
                        _chosenWeapon = wp;
                        // 单体与一组武器需要选目标（一组打目标所在组），全体武器直接发射
                        if (wd.range != AttackRange.All) _chosenType = ActionType.TankWeapon;
                        else Commit(BattleAction.Fire(_current, wp, _battle.AliveEnemies.First()));
                    }
                    GUI.enabled = true;
                }
            }
            else if (GUILayout.Button("攻击", _btn))
            {
                _chosenType = ActionType.HumanAttack;
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (_current.tank != null)
            {
                if (_current.IsTankActive)
                {
                    if (GUILayout.Button("下车", _btn)) { Commit(BattleAction.Simple(_current, ActionType.LeaveTank)); return; }
                }
                else if (!_current.tank.IsDestroyed && GUILayout.Button("上车", _btn))
                {
                    Commit(BattleAction.Simple(_current, ActionType.BoardTank)); return;
                }
            }
            foreach (var stack in GameSession.Instance.State.items)
            {
                if (!GUILayout.Button($"{Game.Core.GameDB.Item(stack.id)?.DisplayName ?? stack.id} ×{stack.count}", _btn)) continue;
                _chosenItem = stack.id;
                _chosenType = ActionType.UseItem;
                return;
            }
            if (GUILayout.Button("防御", _btn)) { Commit(BattleAction.Simple(_current, ActionType.Defend)); return; }
            if (GUILayout.Button("逃跑", _btn)) { Commit(BattleAction.Simple(_current, ActionType.Escape)); return; }
            GUI.enabled = _pending.Count > 0;
            if (GUILayout.Button("撤回上一步", _btn)) Undo();
            GUI.enabled = true;
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        #endregion
    }
}
