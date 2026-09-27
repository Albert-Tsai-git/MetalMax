using System.Linq;
using Game.Core;
using Game.Economy;
using Game.UI;
using Game.WorldMap;
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
                result = GarageService.Repair(s.State, p.tank, out int c);
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
            if (UIRouter.Current == UIScreen.WorldMap && player != null) DrawMapDebug(player, style);
            else if (UIRouter.Current == UIScreen.Menu) DrawMenuDebug(style);
        }

        // 调试用地图：正式界面由 Codex 在表现层实现后删除
        private static void DrawMapDebug(FieldPlayerController player, GUIStyle style)
        {
            const float size = 360;
            var area = new Rect(Screen.width - size - 20, 20, size, size);
            GUI.Box(area, player.Map != null ? player.Map.DisplayName : "（无地图）", style);
            if (player.Map == null) return;
            void Dot(Vector2 n, string label)
            {
                float x = area.x + n.x * size, y = area.y + (1 - n.y) * size;
                GUI.Label(new Rect(x - 4, y - 10, 160, 20), label);
            }
            foreach (var l in WorldMapService.KnownLocations(GameSession.Instance.State, player.Map.entryId))
                Dot(WorldMapService.Normalize(player.Map, new Vector3(l.position.x, 0, l.position.y)), "■ " + l.DisplayName);
            Dot(WorldMapService.Normalize(player.Map, player.transform.position), "▲ 我");
        }

        // 调试用菜单：只显示分页，正式内容由表现层绘制
        private static void DrawMenuDebug(GUIStyle style)
        {
            var tabs = string.Join("  ", System.Enum.GetNames(typeof(MenuTab))
                .Select(t => t == UIRouter.Tab.ToString() ? $"[{t}]" : t));
            GUI.Box(new Rect(Screen.width / 2f - 250, Screen.height / 2f - 60, 500, 120),
                $"菜单（调试）\n{tabs}\nQ/E 切换分页，Esc 关闭", style);
        }
    }
}
