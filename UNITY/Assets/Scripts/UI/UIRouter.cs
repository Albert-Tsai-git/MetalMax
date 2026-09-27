using System;
using System.Collections.Generic;
using Game.Story;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>界面 ID。表现层按 ID 显示对应界面</summary>
    public enum UIScreen { None, Menu, WorldMap }

    /// <summary>主菜单分页</summary>
    public enum MenuTab { Party, Items, Tank, Quests, System }

    /// <summary>
    /// 界面路由（逻辑层）：维护打开的界面栈与菜单分页，处理打开/关闭按键，决定野外是否暂停操作。
    /// 只管“开哪个界面”，不负责绘制；绘制由表现层订阅 ScreenChanged / TabChanged 实现。
    /// 按键：Esc 打开菜单 / 关闭最上层界面，M 开关大地图，Q/E 或 ←/→ 切换菜单分页（菜单打开时）。
    /// </summary>
    public static class UIRouter
    {
        private static readonly List<UIScreen> Stack = new();

        /// <summary>界面变化（原界面，新界面）</summary>
        public static event Action<UIScreen, UIScreen> ScreenChanged;
        /// <summary>菜单分页变化</summary>
        public static event Action<MenuTab> TabChanged;

        public static UIScreen Current => Stack.Count > 0 ? Stack[^1] : UIScreen.None;
        public static MenuTab Tab { get; private set; }

        /// <summary>野外是否应暂停移动与交互：有界面打开或对话进行中</summary>
        public static bool BlocksFieldInput => Current != UIScreen.None || StoryService.ActiveDialogue != null;

        public static void Open(UIScreen screen)
        {
            if (screen == UIScreen.None || Current == screen) return;
            var prev = Current;
            Stack.Remove(screen);
            Stack.Add(screen);
            Debug.Log($"[UI] 打开 {screen}");
            ScreenChanged?.Invoke(prev, screen);
        }

        /// <summary>关闭最上层界面</summary>
        public static void Close()
        {
            if (Stack.Count == 0) return;
            var prev = Current;
            Stack.RemoveAt(Stack.Count - 1);
            Debug.Log($"[UI] 关闭 {prev}");
            ScreenChanged?.Invoke(prev, Current);
        }

        public static void Toggle(UIScreen screen)
        {
            if (Current == screen) Close();
            else Open(screen);
        }

        /// <summary>切换场景等情况下清空全部界面</summary>
        public static void CloseAll()
        {
            while (Stack.Count > 0) Close();
        }

        public static void SetTab(MenuTab tab)
        {
            if (Tab == tab) return;
            Tab = tab;
            TabChanged?.Invoke(tab);
        }

        public static void CycleTab(int delta)
        {
            int n = Enum.GetValues(typeof(MenuTab)).Length;
            SetTab((MenuTab)(((int)Tab + delta + n) % n));
        }

        /// <summary>处理界面按键（由 UIInput 每帧调用）；inField 为当前是否在野外/迷宫</summary>
        public static void HandleInput(Keyboard kb, bool inField)
        {
            if (kb == null || StoryService.ActiveDialogue != null) return;
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (Current != UIScreen.None) Close();
                else if (inField) Open(UIScreen.Menu);
            }
            else if (inField && kb.mKey.wasPressedThisFrame && (Current == UIScreen.None || Current == UIScreen.WorldMap))
                Toggle(UIScreen.WorldMap);
            else if (Current == UIScreen.Menu)
            {
                if (kb.qKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) CycleTab(-1);
                if (kb.eKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) CycleTab(1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Stack.Clear();
            Tab = MenuTab.Party;
            ScreenChanged = null;
            TabChanged = null;
        }
    }
}
