using System.Collections.Generic;
using UnityEngine;

namespace TownOfHostL.Modules.SpawnEditor;

// ランダムスポーンで使う元の部屋を、マップごとに 1 つずつ選べるようにする設定
public static class SpawnRoomOptions
{
    private static readonly Dictionary<int, OptionItem> Choose = new();
    private static readonly Dictionary<int, Dictionary<string, OptionItem>> Rooms = new();

    public static void Setup(int mapId, OptionItem randomSpawn, IEnumerable<string> keys)
    {
        var parent = StringOptionItem.Create("ChooseSpawnRooms", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetParent(randomSpawn)
            .SetGameMode(CustomGameMode.All);
        Choose[mapId] = parent;
        var rooms = Rooms[mapId] = new();
        foreach (var key in keys)
        {
            var k = key;
            rooms[k] = StringOptionItem.Create("SpawnRoom", new string[] { "ColoredOff", "ColoredOn" }, 1, TabGroup.ModMainSettings, false)
                .SetKeyName($"SpawnRoom.{k}")
                .SetOptionName(() => Label(k))
                .SetParent(parent)
                .SetGameMode(CustomGameMode.All);
        }
    }

    private static int Key(int mapId) => mapId == 3 ? 0 : mapId;

    public static string Label(string key) => Translator.GetString($"SpawnRoom.{key}");

    public static bool IsChoosing(int mapId) => Choose.TryGetValue(Key(mapId), out var o) && o.GetBool();

    public static bool IsOn(int mapId, string key)
        => !Rooms.TryGetValue(Key(mapId), out var rooms) || !rooms.TryGetValue(key, out var o) || o.GetValue() == 1;

    public static void Reset(int mapId)
    {
        mapId = Key(mapId);
        if (Rooms.TryGetValue(mapId, out var rooms)) foreach (var r in rooms.Values) r.SetValue(1, false, false);
        if (Choose.TryGetValue(mapId, out var parent)) parent.SetValue(0, true, false);
    }

    // 部屋のピンを押した時。選ぶ設定がまだ切ってあれば、入れてから押した部屋だけ外す
    public static bool Toggle(int mapId, string key, out bool turnedOn)
    {
        turnedOn = false;
        mapId = Key(mapId);
        if (!Choose.TryGetValue(mapId, out var parent) || !Rooms.TryGetValue(mapId, out var rooms) || !rooms.TryGetValue(key, out var room)) return false;
        if (!parent.GetBool())
        {
            for (var p = parent; p != null; p = p.Parent) p.SetValue(1, false, false);
            foreach (var r in rooms.Values) r.SetValue(1, false, false);
            turnedOn = true;
        }
        room.SetValue(room.GetValue() == 1 ? 0 : 1, true, false);
        return true;
    }
}
