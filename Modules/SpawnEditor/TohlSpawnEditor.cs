// Based on https://github.com/waffle-ful/Aeterna-End-K-not Modules/SpawnEditor/EkSpawnEditor.cs (GPL-3.0)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using SpawnEditorKit;
using TMPro;
using UnityEngine;

namespace TownOfHostL.Modules.SpawnEditor;

internal sealed class TohlSpawnEditorHost : ISpawnEditorHost
{
    public string SavePath => TohlSpawnEditor.SavePath;

    public string Text(string key) => Translator.GetString(key);
    public void Log(string message) => Logger.Info(message, "SpawnEditor");
    public void Warn(string message) => Logger.Warn(message, "SpawnEditor");
    public void Notify(string message) => Logger.SendInGame(message, true);

    public int EditMapId
    {
        get
        {
            if (!TohlSpawnEditor.Editing) return -1;
            if (!AmongUsClient.Instance || !ShipStatus.Instance || !PlayerControl.LocalPlayer) return -1;
            return TohlSpawnEditor.ToStorageMapId(AmongUsClient.Instance.TutorialMapId);
        }
    }

    public Vector2 LocalPlayerPosition => PlayerControl.LocalPlayer.transform.position;
    public void TeleportLocalPlayer(Vector2 position) => PlayerControl.LocalPlayer.NetTransform.SnapTo(position);
    public void TeleportPlayer(PlayerControl player, Vector2 position) => player.NetTransform.SnapTo(position);

    public string RoomNameAt(Vector2 position)
    {
        PlainShipRoom best = null;
        float bestDist = float.MaxValue;
        foreach (var room in ShipStatus.Instance.AllRooms)
        {
            if (!room) continue;
            if (room.roomArea && room.roomArea.OverlapPoint(position)) return RoomName(room);
            float d = Vector2.Distance(position, room.transform.position);
            if (d >= bestDist) continue;
            bestDist = d;
            best = room;
        }
        return best ? RoomName(best) : "";
    }

    private static string RoomName(PlainShipRoom room)
        => TranslationController.InstanceExists ? TranslationController.Instance.GetString(room.RoomId) : room.RoomId.ToString();

    public void StyleText(TextMeshPro text)
    {
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.outlineWidth = 0.2f;
    }

    public IReadOnlyList<Vector2> DefaultSpawnPositions(int mapId)
    {
        var map = TohlSpawnEditor.MapOf(mapId);
        return map == null ? Array.Empty<Vector2>() : map.RoomPool().Select(p => p.Value).ToList();
    }

    public bool CustomOnly => TohlSpawnEditor.Opts(EditMapId) is { } o && o.only.GetValue() == 1;

    public bool CustomActive => TohlSpawnEditor.Opts(EditMapId) is { } o && o.custom.GetBool();

    public bool SpawnModeAvailable => TohlSpawnEditor.Opts(EditMapId) != null;

    public int SpawnMode
    {
        get => !CustomActive ? 0 : CustomOnly ? 2 : 1;
        set
        {
            if (TohlSpawnEditor.Opts(EditMapId) is not { } o) return;
            if (value > 0)
            {
                o.map.SetValue(1, false, false);
                o.random.SetValue(1, false, false);
                o.only.SetValue(value == 2 ? 1 : 0, false, false);
            }
            o.custom.SetValue(value > 0 ? 1 : 0, true, false);
        }
    }

    public string ShareFolder => "./TOHL_DATA/SpawnPoints/";

    public bool CanOpenFolder => OperatingSystem.IsWindows();

    public void OpenFolder(string path)
    {
        if (!OperatingSystem.IsWindows()) return;
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(full);
        Process.Start("explorer.exe", full.Replace("/", "\\"));
    }

    public IReadOnlyList<SpawnRoomPin> RoomPins(int mapId)
    {
        var map = TohlSpawnEditor.MapOf(mapId);
        if (map == null) return Array.Empty<SpawnRoomPin>();
        var pool = map.RoomPool().Select(p => p.Key).ToHashSet();
        return map.Positions.Select(p => new SpawnRoomPin(p.Key, SpawnRoomOptions.Label(p.Key), p.Value, pool.Contains(p.Key))).ToList();
    }

    public void ToggleRoom(int mapId, string key)
    {
        if (!SpawnRoomOptions.Toggle(mapId, key, out bool turnedOn)) return;
        if (SpawnEditorUi.Instance) SpawnEditorUi.Instance.Refresh();
        if (turnedOn) Notify(Translator.GetString("SpawnEditor.RoomsOn"));
    }
}

internal static class TohlSpawnEditor
{
    public const string SavePath = "./TOHL_DATA/SpawnPoints.json";
    private const string OldPath = "./TOHL_DATA/CustomSpawns.json";

    // タイトルの「カスタムスポーン」から入った練習の間だけ編集できる
    public static bool EditMode;
    public static bool Editing => EditMode && GameStates.IsFreePlay;

    public static void Init()
    {
        Migrate();
        SpawnStore.Init(new TohlSpawnEditorHost());
    }

    public static int ToStorageMapId(int mapId) => mapId switch
    {
        0 or 3 => 0,
        1 or 2 or 4 or 5 => mapId,
        _ => -1
    };

    public static RandomSpawn.SpawnMap MapOf(int mapId) => mapId switch
    {
        0 or 3 => new RandomSpawn.SkeldSpawnMap(),
        1 => new RandomSpawn.MiraHQSpawnMap(),
        2 => new RandomSpawn.PolusSpawnMap(),
        4 => new RandomSpawn.AirshipSpawnMap(),
        5 => new RandomSpawn.FungleSpawnMap(),
        _ => null
    };

    public static (OptionItem map, OptionItem random, OptionItem custom, OptionItem only)? Opts(int mapId) => mapId switch
    {
        0 or 3 => (Options.MapOption_Skeld, Options.RandomSpawn_Skeld, Options.CustomSpawn_Skeld, Options.CustomSpawnOnly_Skeld),
        1 => (Options.MapOption_MiraHQ, Options.RandomSpawn_MiraHQ, Options.CustomSpawn_MiraHQ, Options.CustomSpawnOnly_MiraHQ),
        2 => (Options.MapOption_Polus, Options.RandomSpawn_Polus, Options.CustomSpawn_Polus, Options.CustomSpawnOnly_Polus),
        4 => (Options.MapOption_Airship, Options.RandomSpawn_Airship, Options.CustomSpawn_Airship, Options.CustomSpawnOnly_Airship),
        5 => (Options.MapOption_Fungle, Options.RandomSpawn_Fungle, Options.CustomSpawn_Fungle, Options.CustomSpawnOnly_Fungle),
        _ => null
    };

    // 自分で決めた地点から 1 つ選ぶ。部屋が当たった時は false (元の部屋から選ぶ)
    public static bool TryPick(RandomSpawn.SpawnMap map, out Vector2 position)
    {
        position = default;
        int mapId = Main.NormalOptions.MapId;
        int storageId = ToStorageMapId(mapId);
        if (storageId < 0 || Opts(mapId) is not { } o || !o.custom.GetBool()) return false;

        var points = SpawnStore.GetEnabledPoints(storageId);
        bool only = o.only.GetValue() == 1;
        if (!SpawnPicker.TryPick(points, map.RoomPool().Count(), only, out var picked)) return false;

        position = new Vector2(mapId == 3 ? -picked.X : picked.X, picked.Y);
        Logger.Info($"カスタムスポーン: {picked.Name}", "RandomSpawn");
        return true;
    }

    // hamo 式の保存 (CustomSpawns.json) が残っていれば、初回だけ新しい形へ移す
    private static void Migrate()
    {
        try
        {
            if (File.Exists(SavePath) || !File.Exists(OldPath)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(OldPath));
            var lib = new SpawnLibrary();
            if (doc.RootElement.TryGetProperty("CurrentPresetId", out var cur)) lib.CurrentPreset = cur.GetInt32();
            if (doc.RootElement.TryGetProperty("Presets", out var presets))
            {
                foreach (var pr in presets.EnumerateArray())
                {
                    var preset = new SpawnPreset { Name = pr.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "" };
                    if (pr.TryGetProperty("SpawnMaps", out var maps))
                    {
                        foreach (var m in maps.EnumerateObject())
                        {
                            if (!Enum.TryParse<MapNames>(m.Name, out var mapName)) continue;
                            int id = ToStorageMapId((int)mapName);
                            if (id < 0 || !m.Value.TryGetProperty("Points", out var pts)) continue;
                            var list = preset.GetPoints(id, true);
                            foreach (var p in pts.EnumerateArray())
                            {
                                list.Add(new SpawnPoint
                                {
                                    Name = p.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "",
                                    X = p.GetProperty("X").GetSingle(),
                                    Y = p.GetProperty("Y").GetSingle(),
                                    Color = list.Count,
                                });
                            }
                        }
                    }
                    lib.Presets.Add(preset);
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, JsonSerializer.Serialize(lib, new JsonSerializerOptions { WriteIndented = true }));
            Logger.Info($"カスタムスポーンを新しい形へ移しました ({lib.Presets.Count} プリセット)", "SpawnEditor");
        }
        catch (Exception e)
        {
            Logger.Warn($"カスタムスポーンの引き継ぎに失敗: {e.Message}", "SpawnEditor");
        }
    }
}
