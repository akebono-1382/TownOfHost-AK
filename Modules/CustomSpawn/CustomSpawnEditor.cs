using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules.CustomSpawn;

public static class CustomSpawnEditor
{
    public static bool ActiveEditMode;
    public static bool Loaded;
    public const int MaxMarker = 8;

    public static bool Editing => ActiveEditMode && GameStates.IsFreePlay;

    private static readonly List<(StringNames name, Color color)> Colors = new()
    {
        (StringNames.ColorRed, Color.red),
        (StringNames.ColorYellow, Color.yellow),
        (StringNames.ColorCyan, Color.cyan),
        (StringNames.ColorLime, Color.green),
        (StringNames.ColorOrange, Palette.Orange),
        (StringNames.ColorGray, Color.gray),
    };

    private static int MapId => AmongUsClient.Instance.TutorialMapId;
    private static CustomSpawnMap CurrentMap => CustomSpawnManager.GetOrCreateMap(MapId);

    private static readonly List<Marker> worldMarkers = new();
    private static readonly List<Marker> mapMarkers = new();
    private static Sprite markerSprite;

    private enum PanelType { Home, EditSpawn, EditColor }
    private static ShapeshifterMinigame minigame;
    private static int selected = -1;
    private static TextBoxTMP textBox;

    private class Marker
    {
        public SpriteRenderer Renderer;
        public TextMeshPro Text;
        public CustomSpawnPoint Point;

        public Marker(bool onMap)
        {
            Renderer = new GameObject("Tohl_SpawnMarker").AddComponent<SpriteRenderer>();
            Renderer.sprite = MarkerSprite;
            Text = new GameObject("Text").AddComponent<TextMeshPro>();
            Text.transform.SetParent(Renderer.transform);
            var baseText = PlayerControl.LocalPlayer ? PlayerControl.LocalPlayer.cosmetics.nameText : null;
            Text.fontSize = Text.fontSizeMin = Text.fontSizeMax = 1.6f;
            if (baseText)
            {
                Text.font = baseText.font;
                Text.fontMaterial = baseText.fontMaterial;
                Text.outlineWidth = baseText.outlineWidth;
            }
            Text.outlineColor = Color.black;
            Text.alignment = TextAlignmentOptions.Center;
            Text.transform.localPosition = new(0f, 0.35f, -15f);
            Renderer.gameObject.SetActive(false);
            if (!onMap) return;

            int ui = LayerMask.NameToLayer("UI");
            Renderer.gameObject.layer = ui;
            Text.gameObject.layer = ui;
            Renderer.transform.localScale = new(0.65f, 0.65f, 1f);
            var collider = Renderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.5f, 0.5f);
            var button = Renderer.gameObject.AddComponent<PassiveButton>();
            button.OnMouseOut = new();
            button.OnMouseOver = new();
            button.OnClick = new();
            button.OnMouseOut.AddListener((Action)(() => Text.outlineColor = Color.black));
            button.OnMouseOver.AddListener((Action)(() => Text.outlineColor = Color.white));
            button.OnClick.AddListener((Action)(() =>
            {
                if (Point != null) PlayerControl.LocalPlayer.NetTransform.SnapTo(Point.Position);
            }));
        }

        public bool Alive => Renderer;

        public void Set(CustomSpawnPoint point)
        {
            Point = point;
            Refresh();
            Renderer.gameObject.SetActive(point != null);
        }

        public void Refresh()
        {
            if (Point == null || !Renderer) return;
            Text.text = Point.Name;
            Color c = Point.Tint;
            Text.color = c;
            Renderer.color = c;
            if (Renderer.gameObject.layer != LayerMask.NameToLayer("UI"))
                Renderer.transform.position = new Vector3(Point.Position.x, Point.Position.y, Point.Position.y / 1000f - 0.01f);
        }
    }

    private static Sprite MarkerSprite
    {
        get
        {
            if (markerSprite) return markerSprite;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 25f) / 3.5f);
                    float dot = Mathf.Clamp01(9.5f - d);
                    float cross = (Mathf.Abs(x - c) < 1.6f || Mathf.Abs(y - c) < 1.6f) && d > 13f && d < 21f ? 0.85f : 0f;
                    float a = Mathf.Max(ring, Mathf.Max(dot, cross));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            markerSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            markerSprite.hideFlags |= HideFlags.HideAndDontSave;
            return markerSprite;
        }
    }

    private static void Init()
    {
        worldMarkers.Clear();
        mapMarkers.Clear();
        var points = CurrentMap.Points;
        for (int i = 0; i < MaxMarker; i++)
        {
            var m = new Marker(false);
            worldMarkers.Add(m);
            mapMarkers.Add(new Marker(true));
            if (i < points.Count) m.Set(points[i]);
        }
    }

    private static void RefreshMarkers()
    {
        var points = CurrentMap.Points;
        for (int i = 0; i < worldMarkers.Count; i++)
        {
            if (!worldMarkers[i].Alive) continue;
            worldMarkers[i].Set(i < points.Count ? points[i] : null);
        }
    }

    public static void Finish()
    {
        if (!ActiveEditMode) return;
        CustomSpawnManager.Save();
        ActiveEditMode = false;
        Loaded = false;
        worldMarkers.Clear();
        mapMarkers.Clear();
    }

    public static void FixedUpdate(PlayerControl player)
    {
        if (!player.AmOwner || !Loaded) return;
        string name = Main.nickName != "" ? Main.nickName : DataManager.player.Customization.Name;
        int count = CurrentMap.Points.Count;
        string text = $"<#ffffff>{name}<{(count >= MaxMarker ? "#ff1919" : "#8cffff")}> ({count}/{MaxMarker})";
        if (player.cosmetics.nameText.text != text) player.cosmetics.nameText.text = text;
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    private static class HudPatch
    {
        public static void Postfix(HudManager __instance)
        {
            if (!Editing) return;
            if (!Loaded)
            {
                if (!ShipStatus.Instance || !PlayerControl.LocalPlayer) return;
                ShipStatus.Instance.EmergencyButton?.gameObject.SetActive(false);
                PlayerControl.LocalPlayer.RpcSetRole(RoleTypes.Shapeshifter, true);
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, 1);
                GameOptionsManager.Instance.CurrentGameOptions.SetFloat(FloatOptionNames.ShapeshifterCooldown, 0f);
                foreach (var console in UnityEngine.Object.FindObjectsOfType<SystemConsole>(true))
                    if (console.FreeplayOnly) console.gameObject.SetActive(false);
                Init();
                Loaded = true;
            }
            GameObject.Find("Main Camera/ShadowQuad")?.SetActive(false);
            __instance.ReportButton?.Hide();
            __instance.ImpostorVentButton?.Hide();
            __instance.KillButton?.Hide();
            __instance.SabotageButton?.Hide();
            var task = __instance.TaskPanel?.taskText;
            var info = GetString("CustomSpawnEditInfo");
            if (task && task.text != info) task.text = info;
        }
    }

    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.CurrentGameOptions), MethodType.Setter)]
    private static class KeepOptionsPatch
    {
        public static bool Prefix(GameOptionsManager __instance) => !ActiveEditMode || __instance.currentGameOptions == null;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSetRole))]
    private static class ForceShapeshifterPatch
    {
        public static void Prefix(ref RoleTypes roleType)
        {
            if (Editing) roleType = RoleTypes.Shapeshifter;
        }
    }

    [HarmonyPatch(typeof(MapBehaviour), nameof(MapBehaviour.Show))]
    private static class MapPatch
    {
        public static void Prefix([HarmonyArgument(0)] MapOptions opts)
        {
            if (Editing && opts != null) opts.Mode = MapOptions.Modes.Normal;
        }

        public static void Postfix(MapBehaviour __instance)
        {
            if (!Editing || !Loaded) return;
            var points = CurrentMap.Points;
            for (int i = 0; i < mapMarkers.Count; i++)
            {
                var marker = mapMarkers[i];
                if (!marker.Alive) continue;
                var go = marker.Renderer.gameObject;
                bool active = i < points.Count;
                go.transform.SetParent(__instance.HerePoint.transform.parent);
                marker.Set(active ? points[i] : null);
                if (!active) continue;
                Vector3 pos = points[i].Position / ShipStatus.Instance.MapScale;
                pos.x *= Mathf.Sign(ShipStatus.Instance.transform.localScale.x);
                pos.z = -1f;
                go.transform.localPosition = pos;
            }
            __instance.taskOverlay?.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.DisconnectInternal))]
    private static class DisconnectPatch
    {
        public static void Prefix() { if (GameStates.IsFreePlay) Finish(); }
    }

    [HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
    private static class MinigamePatch
    {
        public static bool Prefix(ShapeshifterMinigame __instance)
        {
            if (!Editing) return true;
            minigame = __instance;
            selected = -1;

            Minigame.Instance = __instance;
            __instance.timeOpened = Time.realtimeSinceStartup;
            if (MapBehaviour.Instance) MapBehaviour.Instance.Close();
            PlayerControl.LocalPlayer?.MyPhysics.SetNormalizedVelocity(Vector2.zero);
            __instance.StartCoroutine(__instance.CoAnimateOpen());

            __instance.potentialVictims = new();
            var selectable = new Il2CppSystem.Collections.Generic.List<UiElement>();
            for (int i = 0; i < MaxMarker + 1; i++)
            {
                var panel = UnityEngine.Object.Instantiate(__instance.PanelPrefab, __instance.transform);
                panel.transform.localPosition = new Vector3(__instance.XStart + i % 3 * __instance.XOffset, __instance.YStart + i / 3 * __instance.YOffset, -1f);
                SetPanel(panel, i, PanelType.Home);
                __instance.potentialVictims.Add(panel);
                selectable.Add(panel.Button);
            }
            ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, selectable);

            textBox = CreateTextBox(__instance.transform.Find("PhoneUI/UI_Icon_Wifi"));
            UpdateTextBox();
            return false;
        }
    }

    private static void UpdatePanels(PanelType type)
    {
        if (!minigame) return;
        var panels = minigame.potentialVictims;
        for (int i = 0; i < panels.Count; i++) SetPanel(panels[i], i, type);
        UpdateTextBox();
    }

    private static void SetPanel(ShapeshifterPanel panel, int index, PanelType type)
    {
        panel.SetPlayer(index, PlayerControl.LocalPlayer.Data, null);
        panel.shapeshift = null;
        string nameText = "";
        string subText = "";
        Action onClick = null;

        panel.LevelNumberText.text = index.ToString();
        panel.PlayerIcon.gameObject.SetActive(false);
        SetLocalX(panel.NameText.transform, 0f);
        SetLocalX(panel.ColorBlindName.transform, 0f);
        panel.NameText.rectTransform.offsetMax += new Vector2(0.2f, 0f);
        panel.ColorBlindName.rectTransform.offsetMax += new Vector2(0.2f, 0f);

        switch (type)
        {
            case PanelType.Home: HomePanel(index, ref onClick, ref nameText, ref subText); break;
            case PanelType.EditSpawn: EditPanel(index, ref onClick, ref nameText); break;
            case PanelType.EditColor: ColorPanel(index, ref onClick, ref nameText); break;
        }

        panel.shapeshift = (Il2CppSystem.Action)(onClick ?? (() => { }));
        panel.NameText.text = nameText;
        panel.ColorBlindName.text = subText;
        panel.ColorBlindName.enabled = true;
        panel.Background.color = new(1f, 1f, 1f, string.IsNullOrWhiteSpace(nameText) ? 0.4f : 1f);
    }

    private static void SetLocalX(Transform t, float x)
    {
        var p = t.localPosition;
        t.localPosition = new Vector3(x, p.y, p.z);
    }

    private static void Close() => Minigame.Instance?.ForceClose();
    private static void Ding() => RPC.PlaySound(PlayerControl.LocalPlayer.PlayerId, Sounds.TaskComplete);

    private static void HomePanel(int index, ref Action onClick, ref string text, ref string subText)
    {
        var points = CurrentMap.Points;
        if (index == 0)
        {
            bool canMake = points.Count < MaxMarker;
            text = GetString(canMake ? "ED.Add" : "ED.NoAdd");
            subText = $"<{(canMake ? "#8cffff" : "#ff1919")}>({points.Count}/{MaxMarker})";
            if (!canMake) return;
            onClick = () =>
            {
                points.Add(new CustomSpawnPoint
                {
                    Name = $"{GetString("EDCustomSpawn")}{points.Count + 1}",
                    Position = PlayerControl.LocalPlayer.transform.position,
                });
                RefreshMarkers();
                CustomSpawnManager.Save();
                Close();
                Ding();
            };
            return;
        }

        int n = index - 1;
        if (points.Count <= n) return;
        var point = points[n];
        text = point.Name;
        subText = $"<#8cffff>@{GetShipRoomName(point.Position)}";
        onClick = () =>
        {
            selected = n;
            UpdatePanels(PanelType.EditSpawn);
        };
    }

    private static void EditPanel(int index, ref Action onClick, ref string text)
    {
        var points = CurrentMap.Points;
        if (selected < 0 || selected >= points.Count) return;
        var point = points[selected];
        switch (index)
        {
            case 0:
                text = GetString("ED.Move");
                onClick = () =>
                {
                    PlayerControl.LocalPlayer.NetTransform.SnapTo(point.Position);
                    Close();
                };
                break;
            case 1:
                text = GetString("ED.Movehere");
                onClick = () =>
                {
                    point.Position = PlayerControl.LocalPlayer.transform.position;
                    RefreshMarkers();
                    CustomSpawnManager.Save();
                    Close();
                    Ding();
                };
                break;
            case 2:
                text = GetString("ED.Delete");
                onClick = () =>
                {
                    points.Remove(point);
                    selected = -1;
                    RefreshMarkers();
                    CustomSpawnManager.Save();
                    Close();
                };
                break;
            case 3:
                text = GetString("ED.Back");
                onClick = () =>
                {
                    selected = -1;
                    UpdatePanels(PanelType.Home);
                };
                break;
            case 5:
                text = GetString("ED.EditColor");
                onClick = () => UpdatePanels(PanelType.EditColor);
                break;
        }
    }

    private static void ColorPanel(int index, ref Action onClick, ref string text)
    {
        var points = CurrentMap.Points;
        if (selected < 0 || selected >= points.Count) return;
        var point = points[selected];
        if (index == 6)
        {
            text = GetString("ED.Back");
            onClick = () => UpdatePanels(PanelType.EditSpawn);
            return;
        }
        if (index >= Colors.Count) return;
        var (name, color) = Colors[index];
        text = DestroyableSingleton<TranslationController>.Instance.GetString(name);
        onClick = () =>
        {
            point.Tint = color;
            RefreshMarkers();
            CustomSpawnManager.Save();
            Close();
        };
    }

    public static string GetShipRoomName(Vector2 position)
    {
        var ship = ShipStatus.Instance;
        if (!ship) return "";
        var tc = DestroyableSingleton<TranslationController>.Instance;
        PlainShipRoom room = null;
        foreach (var psr in ship.AllRooms)
        {
            if (psr.roomArea != null && psr.roomArea.OverlapPoint(position)) room = psr;
        }
        string roomName = room == null ? "" : tc.GetString(room.RoomId);
        if (room != null && room.RoomId != SystemTypes.Hallway) return roomName;

        var distance = new Dictionary<int, float>();
        if (MapId == 5)
        {
            distance[200] = Vector2.SqrMagnitude(position - new Vector2(-7.95f, -14.10f));
            distance[201] = Vector2.SqrMagnitude(position - new Vector2(1.74f, -9.76f));
            distance[202] = Vector2.SqrMagnitude(position - new Vector2(15.81f, -8.3f));
            distance[203] = Vector2.SqrMagnitude(position - new Vector2(-8.95f, 1.79f));
        }
        foreach (var r in ship.AllRooms)
        {
            if (r.RoomId == SystemTypes.Hallway) continue;
            distance[(int)r.RoomId] = Vector2.SqrMagnitude(position - (Vector2)r.transform.position);
        }
        if (distance.Count == 0) return roomName;
        int nearest = distance.OrderBy(x => x.Value).First().Key;
        if (room != null && nearest < 200 && (SystemTypes)nearest == SystemTypes.VaultRoom) nearest = (int)SystemTypes.Comms;
        string near = nearest >= 200 ? GetString($"ModMapName.{nearest}") : tc.GetString((SystemTypes)nearest);
        return room == null ? string.Format(GetString("Nearroom"), near) : near + roomName;
    }

    private static TextBoxTMP CreateTextBox(Transform parent)
    {
        var collider = new GameObject("Tohl_SpawnName").AddComponent<BoxCollider2D>();
        var box = collider.gameObject.AddComponent<TextBoxTMP>();
        var button = box.gameObject.AddComponent<PassiveButton>();
        var text = new GameObject("Text").AddComponent<TextMeshPro>();
        string backup = "";

        box.AllowEmail = false;
        box.AllowSymbols = true;
        box.AllowPaste = true;
        box.tempTxt = new();
        box.outputText = text;
        box.compoText = "";
        box.text = "";
        box.characterLimit = 15;
        box.OnChange = new();
        box.OnEnter = new();
        box.OnFocusLost = new();
        box.OnEnter.AddListener((Action)Commit);
        box.OnFocusLost.AddListener((Action)Commit);

        if (parent) box.transform.SetParent(parent);
        box.transform.localPosition = new Vector3(0f, 0f, -1f);

        button.OnMouseOut = new();
        button.OnMouseOver = new();
        button.OnClick = new();
        button.OnClick.AddListener((Action)(() => { box.GiveFocus(); backup = box.text; }));

        collider.offset = new Vector2(1.5f, 0f);
        collider.size = new Vector2(3f, 0.25f);

        text.fontSize = text.fontSizeMax = text.fontSizeMin = 2f;
        text.alignment = TextAlignmentOptions.Left;
        text.transform.SetParent(box.transform);
        text.transform.localPosition = new(10.2f, 0.025f);

        int ui = LayerMask.NameToLayer("UI");
        box.gameObject.layer = ui;
        text.gameObject.layer = ui;
        return box;

        void Commit()
        {
            if (string.IsNullOrWhiteSpace(box.text))
            {
                box.SetText(backup);
                return;
            }
            var points = CurrentMap.Points;
            if (selected < 0 || selected >= points.Count) CustomSpawnManager.Data.CurrentPreset.Name = box.text;
            else
            {
                points[selected].Name = box.text;
                RefreshMarkers();
            }
            CustomSpawnManager.Save();
        }
    }

    private static void UpdateTextBox()
    {
        if (!textBox) return;
        var points = CurrentMap.Points;
        string s = selected >= 0 && selected < points.Count ? points[selected].Name : CustomSpawnManager.Data.CurrentPreset.Name;
        textBox.outputText.text = s;
        textBox.text = s;
    }
}
