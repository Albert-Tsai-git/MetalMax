using System.Linq;
using Game.Core;
using Game.Economy;
using Game.Town;
using UnityEngine;

namespace Game.Field
{
    /// <summary>
    /// 原型调试用城镇菜单（IMGUI）：住宿、修理、购买、领赏。正式城镇界面由 Codex 在表现层实现后删除。
    /// </summary>
    public class TownDebugMenu : MonoBehaviour
    {
        private string _msg = "";

        private void OnGUI()
        {
            var gate = TownGate.Current;
            if (gate == null) { _msg = ""; return; }
            var s = GameSession.Instance.State;
            var town = GameDB.Town(gate.townId);
            if (town == null) return;

            GUILayout.BeginArea(new Rect(Screen.width - 330, 10, 320, Screen.height - 20), GUI.skin.box);
            GUILayout.Label($"<b>{town.DisplayName}</b>   {s.Gold}G");

            if (GUILayout.Button($"住宿（{town.innPrice}G）")) _msg = Result(TownService.Rest(s, town.townId));

            if (town.hasGarage)
            {
                foreach (var p in s.party.Where(p => p.tank != null))
                {
                    int cost = p.tank.RepairCost();
                    if (GUILayout.Button($"修理 {p.tank.tankName}（{cost}G）"))
                    {
                        var r = GarageService.RepairAffordable(s, p.tank, out _);
                        if (r == OpResult.Ok) GarageService.FillArmor(p.tank);
                        _msg = Result(r);
                    }
                    int ammo = p.tank.RefillCost();
                    if (GUILayout.Button($"补充弹药 {p.tank.tankName}（{ammo}G）"))
                        _msg = Result(GarageService.Refill(s, p.tank, out _));
                }
            }

            var shop = GameDB.Shop(town.shopId);
            if (shop != null)
            {
                GUILayout.Label($"— {shop.DisplayName} —");
                foreach (var item in shop.goods)
                    if (GUILayout.Button($"{item.DisplayName}  {item.price}G"))
                        _msg = Result(ShopService.Buy(s, shop, item, out _));
                GUILayout.Label($"背包 {s.inventory.Count} 件");
            }

            if (town.hasBountyOffice)
            {
                GUILayout.Label("— 赏金办事处 —");
                foreach (var b in BountyService.All())
                    GUILayout.Label($"{b.DisplayName}  {b.bounty}G  [{BountyService.StateOf(s, b.enemyId)}]");
                if (GUILayout.Button("领取赏金"))
                {
                    var r = BountyService.ClaimAll(s, town.townId, out int total);
                    _msg = r == OpResult.Ok ? $"领取 {total}G" : Result(r);
                }
            }

            if (_msg != "") GUILayout.Label(_msg);
            GUILayout.EndArea();
        }

        private static string Result(OpResult r) => TextDB.Get($"UI.Result.{r}");
    }
}
