using System.Linq;
using Game.Core;
using Game.Economy;
using Game.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Field
{
    /// <summary>野外原型 HUD：显示金钱、经验、队伍状态；按 R 全面修理（模拟回城）</summary>
    public class FieldHUD : MonoBehaviour
    {
        private string _msg = "WASD 移动，Shift 奔跑（步行），F 下车/上车，E 交互，M 地图，Esc 菜单，R 修理，F5 存档，F9 读档";

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || UIRouter.Current != UIScreen.None) return;
            if (kb.f5Key.wasPressedThisFrame)
            {
                var r = GameSession.Instance.SaveGame(0, FindAnyObjectByType<FieldPlayerController>()?.transform.position ?? Vector3.zero);
                _msg = r == OpResult.Ok ? "已存档（槽位 0）" : $"存档失败：{r}";
                return;
            }
            if (kb.f9Key.wasPressedThisFrame)
            {
                var r = GameSession.Instance.LoadGame(0);
                if (r != OpResult.Ok) _msg = $"读档失败：{r}";
                return;
            }
            if (!kb.rKey.wasPressedThisFrame) return;
            var s = GameSession.Instance;
            int cost = 0;
            var result = OpResult.Ok;
            foreach (var p in s.party)
            {
                p.hp = p.maxHp;
                if (p.tank == null) continue;
                result = GarageService.RepairAffordable(s.State, p.tank, out int c);
                if (result == OpResult.NothingToDo) result = OpResult.Ok;
                if (result != OpResult.Ok) break;
                cost += c;
                p.inTank = true;
            }
            _msg = result == OpResult.Ok ? $"修理完成，花费 {cost}G" : $"修理失败：{result}";
        }

        private void OnGUI()
        {
            var s = GameSession.Instance;
            var lines = s.party.Select(p =>
                $"{p.name} Lv{p.level} HP {p.hp}/{p.maxHp}" + (p.tank != null ? $"  {p.tank.tankName} SP {p.tank.currentSp}/{p.tank.MaxSp}" : ""));
            var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft };
            var player = FindAnyObjectByType<FieldPlayerController>();
            var target = player != null ? player.GetComponent<FieldInteractor>()?.Current : null;
            if (target != null)
                GUI.Box(new Rect(Screen.width / 2f - 120, Screen.height - 90, 240, 30), $"E：{TextDB.Get(target.PromptKey)}");
            string mode = player != null && player.OnFoot ? "步行" : "乘车";
            GUI.Box(new Rect(10, 10, 520, 150),
                $"金钱 {s.gold}G    经验 {s.exp}    [{mode}]\n{string.Join("\n", lines)}\n\n{_msg}", style);
        }
    }
}
