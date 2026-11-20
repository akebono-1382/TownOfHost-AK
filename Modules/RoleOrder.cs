using System.Collections.Generic;
using System.Linq;
using TownOfHostL.Roles.Core;
using TownOfHostL.SettingsUI;
using UnityEngine;

namespace TownOfHostL;

/// <summary>
/// 役職を並べて見せる順。設定画面の区分の順 (インポスター → マッドメイト → クルー → 第三陣営 → ユニット → 属性) で、
/// 区分の中は設定画面と同じ順 (出現率の項目を作った順)。チャットの一覧・ロビーの設定の表示・設定の書き出しで共通に使う
/// </summary>
public static class RoleOrder
{
    private static Dictionary<CustomRoles, int> keys;
    private static Dictionary<CustomRoles, TabGroup> tabs;
    private static int builtFor = -1;

    private static void Build()
    {
        // 項目か役職が増えていたら組み直す (どちらも起動時に作り終える)
        int stamp = OptionItem.AllOptions.Count * 4096 + Options.CustomRoleSpawnChances.Count;
        if (keys != null && builtFor == stamp) return;
        var all = OptionItem.AllOptions;
        var indexOf = new Dictionary<OptionItem, int>(all.Count);
        for (int i = 0; i < all.Count; i++) indexOf.TryAdd(all[i], i);
        keys = new();
        tabs = new();
        // 出現率の項目を共有する役職も、それぞれ同じ所に並ぶ
        foreach (var (role, option) in Options.CustomRoleSpawnChances)
        {
            if (option == null || !indexOf.TryGetValue(option, out var i)) continue;
            int tab = System.Array.IndexOf(RoleCatalog.Tabs, option.Tab);
            if (tab < 0) tab = RoleCatalog.Tabs.Length;
            keys[role] = tab * 1_000_000 + i;
            tabs[role] = option.Tab;
        }
        builtFor = stamp;
    }

    /// <summary>並べる時の鍵 (小さいほど先)。設定の項目の無い役職は最後</summary>
    public static int Key(CustomRoles role)
    {
        Build();
        return keys.TryGetValue(role, out var k) ? k : int.MaxValue;
    }

    public static IEnumerable<CustomRoles> Sort(IEnumerable<CustomRoles> roles) => roles.OrderBy(Key);

    /// <summary>役職の区分 (設定画面の左の列のどこにあるか)。設定の項目の無い役職は null</summary>
    public static TabGroup? TabOf(CustomRoles role)
    {
        Build();
        return tabs.TryGetValue(role, out var t) ? t : null;
    }

    /// <summary>区分の見出し (区分の色の太字)</summary>
    public static string Header(TabGroup tab)
    {
        var color = ColorUtility.ToHtmlStringRGB(GameSettingMenuPatch.CatColor(tab));
        return $"<#{color}><b>■{Translator.GetString("MenuCat." + tab)}</b></color>";
    }
}
