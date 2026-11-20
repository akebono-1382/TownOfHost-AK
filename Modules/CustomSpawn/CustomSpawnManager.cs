using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace TownOfHostL.Modules.CustomSpawn;

public static class CustomSpawnManager
{
    private const string FilePath = "./TOHL_DATA/CustomSpawns.json";
    public const int Version = 1;

    private static CustomSpawnData _data;
    public static CustomSpawnData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    public static void Load()
    {
        _data = new CustomSpawnData();
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return;
            var loaded = JsonSerializer.Deserialize<CustomSpawnData>(json);
            if (loaded?.Presets is { Count: > 0 })
            {
                if (loaded.CurrentPresetId < 0 || loaded.CurrentPresetId >= loaded.Presets.Count) loaded.CurrentPresetId = 0;
                _data = loaded;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"カスタムスポーンの読み込みに失敗: {ex.Message}", "CustomSpawn");
        }
    }

    public static void Save()
    {
        if (_data == null) return;
        try
        {
            Directory.CreateDirectory("TOHL_DATA");
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch (Exception ex)
        {
            Logger.Error($"カスタムスポーンの保存に失敗: {ex.Message}", "CustomSpawn");
        }
    }

    public static List<CustomSpawnPoint> GetPoints(int mapId)
    {
        var preset = Data.CurrentPreset;
        if (preset == null) return null;
        return preset.SpawnMaps.TryGetValue(((MapNames)mapId).ToString(), out var map) ? map.Points : null;
    }

    public static CustomSpawnMap GetOrCreateMap(int mapId)
    {
        var key = ((MapNames)mapId).ToString();
        var maps = Data.CurrentPreset.SpawnMaps;
        if (!maps.TryGetValue(key, out var map)) maps[key] = map = new CustomSpawnMap { MapName = key };
        return map;
    }
}

public class CustomSpawnData
{
    public int Version { get; set; } = CustomSpawnManager.Version;
    public int CurrentPresetId { get; set; }
    public List<CustomSpawnPreset> Presets { get; set; } = new() { new CustomSpawnPreset { Name = "プリセット1" } };

    [JsonIgnore]
    public CustomSpawnPreset CurrentPreset => Presets.Count > CurrentPresetId ? Presets[CurrentPresetId] : null;
}

public class CustomSpawnPreset
{
    public string Name { get; set; } = "";
    public Dictionary<string, CustomSpawnMap> SpawnMaps { get; set; } = new();
}

public class CustomSpawnMap
{
    public string MapName { get; set; } = "";
    public List<CustomSpawnPoint> Points { get; set; } = new();
}

public class CustomSpawnPoint
{
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public string Color { get; set; } = "FFFFFF";

    [JsonIgnore]
    public Vector2 Position
    {
        get => new(X, Y);
        set { X = value.x; Y = value.y; }
    }

    [JsonIgnore]
    public Color32 Tint
    {
        get => ColorUtility.TryParseHtmlString("#" + Color, out var c) ? (Color32)c : new Color32(255, 255, 255, 255);
        set => Color = ColorUtility.ToHtmlStringRGB(value);
    }
}
