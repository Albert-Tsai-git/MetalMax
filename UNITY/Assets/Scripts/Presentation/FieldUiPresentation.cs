using System;
using System.Linq;
using Game.Core;
using Game.Economy;
using Game.Field;
using Game.Story;
using Game.Tank;
using Game.UI;
using Game.WorldMap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>Field UI skin. It renders the I-22 router and existing game state; it owns no gameplay state.</summary>
    public sealed class FieldUiPresentation : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField] private Sprite mapBase;

        private static readonly Color Ink = new(.91f, .86f, .72f, 1f);
        private static readonly Color DimInk = new(.59f, .61f, .53f, 1f);
        private static readonly Color Plate = new(.075f, .095f, .09f, .96f);
        private static readonly Color PlateLight = new(.13f, .16f, .145f, .98f);
        private static readonly Color Brass = new(.72f, .49f, .22f, 1f);
        private static readonly Color Rust = new(.57f, .23f, .13f, 1f);

        private Canvas _canvas;
        private RectTransform _hud;
        private RectTransform _menu;
        private RectTransform _mapScreen;
        private RectTransform _mapFrame;
        private RectTransform _mapImageRect;
        private Image _mapImage;
        private Text _money;
        private Text _partyText;
        private Text _mode;
        private Text _prompt;
        private Text _menuTitle;
        private Text _menuBody;
        private Text _mapTitle;
        private Text _mapLegend;
        private GameObject _saveButton;
        private GameObject _loadButton;
        private GameObject _mapOpenButton;
        private GameObject _menuOpenButton;
        private RectTransform _playerMarker;
        private float _refreshAt;

        private void OnEnable()
        {
            UIRouter.ScreenChanged += OnScreenChanged;
            UIRouter.TabChanged += OnTabChanged;
            WorldMapService.Discovered += OnLocationDiscovered;
        }

        private void OnDisable()
        {
            UIRouter.ScreenChanged -= OnScreenChanged;
            UIRouter.TabChanged -= OnTabChanged;
            WorldMapService.Discovered -= OnLocationDiscovered;
        }

        private void Start()
        {
            BuildCanvas();
            RefreshAll();
            ApplyRouterState();
        }

        private void Update()
        {
            if (_canvas == null) return;
            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + .2f;
                RefreshAll();
            }
        }

        private void OnScreenChanged(UIScreen previous, UIScreen current) => ApplyRouterState();
        private void OnTabChanged(MenuTab tab) => RefreshMenu();
        private void OnLocationDiscovered(WorldMapData location) { if (UIRouter.Current == UIScreen.WorldMap) RefreshMap(); }

        private void ApplyRouterState()
        {
            if (_hud == null) return;
            var screen = UIRouter.Current;
            var hudActive = screen == UIScreen.None;
            _hud.gameObject.SetActive(hudActive);
            if (_mapOpenButton != null) _mapOpenButton.SetActive(hudActive);
            if (_menuOpenButton != null) _menuOpenButton.SetActive(hudActive);
            _menu.gameObject.SetActive(screen == UIScreen.Menu);
            _mapScreen.gameObject.SetActive(screen == UIScreen.WorldMap);
            if (screen == UIScreen.Menu) RefreshMenu();
            if (screen == UIScreen.WorldMap) RefreshMap();
        }

        private void BuildCanvas()
        {
            var root = new GameObject("FieldUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 80;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;

            _hud = Panel(root.transform, "FieldHud", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -30), new Vector2(440, 274), Plate);
            Panel(_hud, "LeftRule", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(4, 274), Brass);
            Text(_hud, "Eyebrow", "FIELD RECORD   /   07", 14, Brass, TextAnchor.MiddleLeft, new Vector2(24, -22), new Vector2(390, 23), FontStyle.Bold);
            _money = Text(_hud, "Money", "", 21, Ink, TextAnchor.MiddleLeft, new Vector2(24, -55), new Vector2(390, 34), FontStyle.Bold);
            _mode = Text(_hud, "Mode", "", 15, Brass, TextAnchor.MiddleLeft, new Vector2(24, -91), new Vector2(390, 26), FontStyle.Bold);
            Panel(_hud, "Divider", new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -111), new Vector2(388, 1), new Color(.42f, .36f, .25f, .6f));
            _partyText = Text(_hud, "Party", "", 17, Ink, TextAnchor.UpperLeft, new Vector2(24, -126), new Vector2(390, 138), FontStyle.Normal);

            var promptPlate = Panel(root.transform, "InteractionPrompt", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 31), new Vector2(470, 62), Plate);
            Image(promptPlate, "EKey", Icon("Interact"), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(34, 34), Ink);
            _prompt = Text(promptPlate, "Prompt", "", 18, Ink, TextAnchor.MiddleLeft, new Vector2(66, 0), new Vector2(376, 54), FontStyle.Bold);
            _mapOpenButton = Button(root.transform, "OpenMap", "Map", "M   荒野地图", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -33), new Vector2(216, 58), () => UIRouter.Open(UIScreen.WorldMap)).gameObject;
            _menuOpenButton = Button(root.transform, "OpenMenu", "Party", "Esc   行旅菜单", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-32, -103), new Vector2(216, 52), () => UIRouter.Open(UIScreen.Menu)).gameObject;
            Text(root.transform, "Controls", "WASD 移动   ·   Shift 奔跑   ·   F 上下车", 14, DimInk, TextAnchor.MiddleRight, new Vector2(-34, 28), new Vector2(520, 24), FontStyle.Normal, new Vector2(1, 0), new Vector2(1, 0));

            BuildMenu(root.transform);
            BuildMap(root.transform);
        }

        private void BuildMenu(Transform parent)
        {
            _menu = Panel(parent, "MenuScreen", Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.025f, .034f, .032f, .88f));
            var frame = Panel(_menu, "MenuFrame", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1120, 690), Plate);
            Panel(frame, "TopRule", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 4), Brass);
            Text(frame, "Eyebrow", "FIELD DOSSIER     /     ZHANQIAO ROUTE", 14, Brass, TextAnchor.MiddleLeft, new Vector2(36, -26), new Vector2(700, 26), FontStyle.Bold);
            var close = Button(frame, "Close", "System", "Esc   返回荒野", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -25), new Vector2(210, 48), UIRouter.Close);
            _menuTitle = Text(frame, "Title", "", 32, Ink, TextAnchor.MiddleLeft, new Vector2(36, -82), new Vector2(710, 48), FontStyle.Bold);
            Panel(frame, "Divider", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, -116), new Vector2(0, 1), new Color(.42f, .36f, .25f, .6f));
            var nav = Panel(frame, "Navigation", new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(24, 22), new Vector2(286, -148), PlateLight);
            var tabs = (MenuTab[])Enum.GetValues(typeof(MenuTab));
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                Button(nav, "Tab_" + tab, IconName(tab), "", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -22 - i * 74), new Vector2(-16, 60), () => UIRouter.SetTab(tab), new Vector2(0, .5f), new Vector2(0, .5f));
                var label = Text(nav, "Label_" + tab, TextDB.Get("UI.Menu." + tab), 18, Ink, TextAnchor.MiddleLeft, new Vector2(72, -22 - i * 74), new Vector2(182, 60), FontStyle.Bold, new Vector2(0, 1), new Vector2(0, 1));
                label.raycastTarget = false;
            }
            var content = Panel(frame, "Content", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(336, 34), new Vector2(-368, -166), new Color(0, 0, 0, 0));
            _menuBody = Text(content, "Body", "", 19, Ink, TextAnchor.UpperLeft, new Vector2(12, -14), new Vector2(-24, -28), FontStyle.Normal, Vector2.zero, Vector2.one);
            _saveButton = Button(content, "Save", "Repair", "保存行旅", new Vector2(.05f, .72f), new Vector2(.05f, .72f), Vector2.zero, new Vector2(230, 58), SaveGame).gameObject;
            _loadButton = Button(content, "Load", "Map", "读取记录", new Vector2(.05f, .58f), new Vector2(.05f, .58f), Vector2.zero, new Vector2(230, 58), LoadGame).gameObject;
            Text(frame, "Footer", "记录只读自当前行旅档案    /    Q · E 或方向键切换分页", 14, DimInk, TextAnchor.MiddleLeft, new Vector2(36, 9), new Vector2(850, 25), FontStyle.Normal, new Vector2(0, 0), new Vector2(0, 0));
            _menu.gameObject.SetActive(false);
        }

        private void BuildMap(Transform parent)
        {
            _mapScreen = Panel(parent, "WorldMapScreen", Vector2.zero, Vector2.one, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, new Color(.02f, .03f, .03f, .93f));
            var frame = Panel(_mapScreen, "MapFrame", new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(1330, 760), Plate);
            Panel(frame, "TopRule", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 4), Brass);
            Text(frame, "Eyebrow", "NAVIGATION CHART     /     SIGNALS ONLY", 14, Brass, TextAnchor.MiddleLeft, new Vector2(34, -27), new Vector2(700, 24), FontStyle.Bold);
            _mapTitle = Text(frame, "Title", "", 30, Ink, TextAnchor.MiddleLeft, new Vector2(34, -74), new Vector2(760, 42), FontStyle.Bold);
            Button(frame, "MapClose", "Map", "M   收起地图", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -27), new Vector2(196, 48), UIRouter.Close);
            _mapFrame = Panel(frame, "MapWindow", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0), new Vector2(32, 42), new Vector2(-360, -132), PlateLight);
            _mapImage = Image(_mapFrame, "MapBase", mapBase, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            _mapImage.preserveAspect = true;
            _mapImageRect = (RectTransform)_mapImage.transform;
            _mapImageRect.anchorMin = new Vector2(.5f, 0);
            _mapImageRect.anchorMax = new Vector2(.5f, 1);
            _mapImageRect.sizeDelta = new Vector2(628, 0);
            _mapImageRect.anchoredPosition = Vector2.zero;
            _mapLegend = Text(frame, "Legend", "", 18, Ink, TextAnchor.UpperLeft, new Vector2(-306, -138), new Vector2(268, -180), FontStyle.Normal, new Vector2(1, 1), new Vector2(1, 0));
            Text(frame, "MapFooter", "地点仅在发现后标记    /    ▲ 当前坐标", 14, DimInk, TextAnchor.MiddleLeft, new Vector2(34, 11), new Vector2(760, 24), FontStyle.Normal, new Vector2(0, 0), new Vector2(0, 0));
            _mapScreen.gameObject.SetActive(false);
        }

        private void RefreshAll()
        {
            var session = GameSession.Instance;
            var player = FindAnyObjectByType<FieldPlayerController>();
            _money.text = $"{session.gold:N0} G";
            _mode.text = player != null && player.OnFoot ? "步行   /   徒步模式" : "乘车   /   战车模式";
            _partyText.text = string.Join("\n", session.party.Select(p =>
                $"{TextDB.Name(p.id)}   Lv {p.level}     HP {p.hp}/{p.maxHp}" + (p.tank != null ? $"     SP {p.tank.currentSp}/{p.tank.MaxSp}" : "")));
            var interactable = player != null ? player.GetComponent<FieldInteractor>()?.Current : null;
            _prompt.transform.parent.gameObject.SetActive(interactable != null && UIRouter.Current == UIScreen.None);
            _prompt.text = interactable != null ? TextDB.Get(interactable.PromptKey) : "";
            if (UIRouter.Current == UIScreen.Menu) RefreshMenu();
            if (UIRouter.Current == UIScreen.WorldMap) UpdatePlayerMarker();
        }

        private void RefreshMenu()
        {
            if (_menuBody == null) return;
            var session = GameSession.Instance;
            var state = session.State;
            var tab = UIRouter.Tab;
            _menuTitle.text = TextDB.Get("UI.Menu." + tab);
            if (_saveButton != null) _saveButton.SetActive(tab == MenuTab.System);
            if (_loadButton != null) _loadButton.SetActive(tab == MenuTab.System);
            switch (tab)
            {
                case MenuTab.Party:
                    _menuBody.text = string.Join("\n\n", session.party.Select(p =>
                        $"{TextDB.Name(p.id)}    Lv {p.level}    {(p.IsAlive ? "行动可用" : "无法战斗")}\nHP   {p.hp,3}/{p.maxHp,-3}    ATK {p.attack,2}    DEF {p.defense,2}\n" +
                        (p.tank != null ? $"{p.tank.tankName}   SP {p.tank.currentSp}/{p.tank.MaxSp}   {(p.IsTankActive ? "乘员已登车" : "乘员步行")}" : "徒步成员")));
                    break;
                case MenuTab.Items:
                    _menuBody.text = state.items.Count == 0 ? "背包里还没有补给。\n\n沿旧公路搜寻箱柜，或向栈桥镇商人采购。" :
                        string.Join("\n\n", state.items.Select(i => $"{(GameDB.Item(i.id) != null ? GameDB.Item(i.id).DisplayName : TextDB.Name(i.id))}      × {i.count}\n{TextDB.Desc(i.id)}"));
                    break;
                case MenuTab.Tank:
                    var tanks = session.party.Where(p => p.tank != null).Select(p => p.tank).ToArray();
                    _menuBody.text = tanks.Length == 0 ? "队伍没有可用战车。" : string.Join("\n\n", tanks.Select(t =>
                        $"{t.tankName}    SP {t.currentSp}/{t.MaxSp}\n载重 {t.TotalWeight:F1}/{t.LoadCapacity:F1} t    余量 {t.FreeLoad:F1} t\n" +
                        "底盘  " + PartName(t.chassis) + "\n引擎  " + PartName(t.engine) + "\nC装置  " + PartName(t.cUnit) + "\n武器  " + string.Join(" / ", t.weapons.Where(w => w != null).Select(PartName))));
                    break;
                case MenuTab.Quests:
                    _menuBody.text = state.quests.Count == 0 ? "还没有收到正式委托。\n\n继续沿着信号源调查，新的线索会记录在这里。" : string.Join("\n\n", state.quests.Select(q =>
                    {
                        var quest = GameDB.Quest(q.id);
                        var objective = quest != null && quest.steps.Count > 0 ? quest.StepText(Mathf.Clamp(q.step, 0, quest.steps.Count - 1)) : "目标记录待更新";
                        return $"{TextDB.Name(q.id)}    /    {(q.state == QuestState.Completed ? "已完成" : "进行中")}\n{objective}";
                    }));
                    break;
                case MenuTab.System:
                    _menuBody.text = "行旅记录保存在本机。选择一个操作：";
                    break;
            }
        }

        private void RefreshMap()
        {
            if (_mapTitle == null) return;
            var player = FindAnyObjectByType<FieldPlayerController>();
            var map = player != null ? player.Map : null;
            _mapTitle.text = map != null ? TextDB.Name(map.entryId) : "没有可用地图";
            if (mapBase != null) _mapImage.sprite = mapBase;
            if (map == null || _mapImageRect == null) return;
            var state = GameSession.Instance.State;
            foreach (Transform child in _mapImageRect)
                if (child.name.StartsWith("MapMark_") && child.name != "MapMark_Player") Destroy(child.gameObject);
            var locations = WorldMapService.KnownLocations(state, map.entryId).ToArray();
            foreach (var location in locations)
            {
                var n = WorldMapService.Normalize(map, new Vector3(location.position.x, 0, location.position.y));
                var mark = Marker(_mapImageRect, "MapMark_" + location.entryId, n, Brass, 18);
            }
            if (_playerMarker != null) Destroy(_playerMarker.gameObject);
            var playerDot = Marker(_mapImageRect, "MapMark_Player", Vector2.zero, new Color(.84f, .33f, .15f), 22);
            _playerMarker = playerDot.rectTransform;
            UpdatePlayerMarker();
            _mapLegend.text = locations.Length == 0
                ? "未发现地点\n\n沿路行驶，进入信号范围后会自动记录坐标。\n\n▲ 当前位置"
                : "已发现信号\n\n" + string.Join("\n", locations.Select(l => "◆  " + TextDB.Name(l.entryId))) + "\n\n▲ 当前位置";
        }

        private void UpdatePlayerMarker()
        {
            var player = FindAnyObjectByType<FieldPlayerController>();
            var map = player != null ? player.Map : null;
            if (player == null || map == null || _playerMarker == null) return;
            var normalized = WorldMapService.Normalize(map, player.transform.position);
            _playerMarker.anchorMin = normalized;
            _playerMarker.anchorMax = normalized;
        }

        private string PartName(PartInstance part) => part != null && part.data != null ? part.data.DisplayName : "—";

        private Image Marker(Transform parent, string name, Vector2 normalized, Color color, float size)
        {
            var image = Image(parent, name, null, new Vector2(normalized.x, normalized.y), new Vector2(normalized.x, normalized.y), Vector2.zero, new Vector2(size, size), color);
            image.raycastTarget = false;
            var text = Text(image.transform, "Glyph", "", Mathf.RoundToInt(size), color, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero, FontStyle.Bold, Vector2.zero, Vector2.one);
            var textRect = (RectTransform)text.transform;
            textRect.pivot = new Vector2(.5f, .5f);
            textRect.anchoredPosition = Vector2.zero;
            text.font = font;
            text.text = name.EndsWith("Player", StringComparison.Ordinal) ? "▲" : "◆";
            text.fontSize = Mathf.RoundToInt(size);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return image;
        }

        private Image Button(Transform parent, string name, string iconName, string label, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Action click, Vector2? iconMin = null, Vector2? iconMax = null)
        {
            var image = Image(parent, name, null, anchorMin, anchorMax, position, size, PlateLight);
            image.raycastTarget = true;
            var pointer = image.gameObject.AddComponent<FieldUiPointerButton>();
            pointer.Initialize(image, click);
            var icon = Image(image.transform, "Icon", Icon(iconName), iconMin ?? new Vector2(.5f, .5f), iconMax ?? new Vector2(.5f, .5f), iconMin.HasValue ? Vector2.zero : new Vector2(-size.x / 2 + 31, 0), new Vector2(28, 28), Brass);
            icon.raycastTarget = false;
            if (!string.IsNullOrEmpty(label))
            {
                var text = Text(image.transform, "Label", label, 16, Ink, TextAnchor.MiddleLeft, new Vector2(58, 0), new Vector2(-70, -8), FontStyle.Bold, Vector2.zero, Vector2.one);
                text.raycastTarget = false;
            }
            return image;
        }

        private RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private Text Text(Transform parent, string name, string value, int size, Color color, TextAnchor alignment, Vector2 position, Vector2 dimensions, FontStyle style, Vector2? anchorMin = null, Vector2? anchorMax = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin ?? new Vector2(0, 1); rect.anchorMax = anchorMax ?? new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = dimensions;
            var text = go.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment; text.fontStyle = style; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Image Image(Transform parent, string name, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = (anchorMin + anchorMax) * .5f; rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.sprite = sprite; image.color = color; image.preserveAspect = sprite != null;
            return image;
        }

        private Sprite Icon(string id) => Resources.Load<Sprite>($"Icons/UI_Icon_{id}");
        private string IconName(MenuTab tab) => tab.ToString();

        private void SaveGame()
        {
            var player = FindAnyObjectByType<FieldPlayerController>();
            var result = GameSession.Instance.SaveGame(0, player != null ? player.transform.position : Vector3.zero);
            _menuBody.text = result == OpResult.Ok ? "行旅记录已保存至槽位 0。" : "保存失败：" + TextDB.Get("UI.Result." + result);
        }

        private void LoadGame()
        {
            var result = GameSession.Instance.LoadGame(0);
            _menuBody.text = result == OpResult.Ok ? "记录已读取。" : "读取失败：" + TextDB.Get("UI.Result." + result);
        }

    }

    public sealed class FieldUiPointerButton : MonoBehaviour
    {
        private Image _image;
        private RectTransform _rect;
        private Action _click;

        public void Initialize(Image image, Action click)
        {
            _image = image;
            _rect = image.rectTransform;
            _click = click;
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || _image == null) return;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(_rect, mouse.position.ReadValue());
            _image.color = over ? new Color(.22f, .23f, .19f, 1f) : new Color(.13f, .16f, .145f, .98f);
            if (over && mouse.leftButton.wasPressedThisFrame) _click?.Invoke();
        }
    }
}
