using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using TownOfHostL.Roles.Core;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

/// <summary>
/// ロビーでホストが設定を変えたら、触るのが 5 秒止まった所でまとめてチャットに出す (「キルクール: 25秒 → 30秒」)。
/// 元に戻した設定は出さない。役職の設定には役職の名前を付ける。
/// </summary>
/// <remarks>TownOfHost-hamo (rar006/TownOfHost-hamo@f7b00a9e Modules/SettingChangeNotice.cs) を元にした。</remarks>
[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
public static class SettingChangeNotice
{
    public static OptionItem Enabled;

    const float QuietSeconds = 5f;
    const int MaxLines = 8;

    sealed class Change
    {
        public OptionItem Option;
        public string Before;
        public string After;
    }
    static readonly Dictionary<OptionItem, Change> pending = new();
    static float lastChangeAt = -1f;
    static Dictionary<OptionItem, CustomRoles> roleOf;

    public static void SetupCustomOption()
    {
        Enabled = StringOptionItem.Create("SettingChangeNotice", new string[] { "ColoredOff", "ColoredOn" }, 0, TabGroup.ModMainSettings, false)
            .SetGameMode(CustomGameMode.All);
    }

    /// <summary>変わる前の文字を取っておく必要があるか (要らない時は文字を作らずに済ませる)</summary>
    public static bool Wants(OptionItem option)
        => Enabled != null && option != Enabled && Enabled.GetBool() && GameStates.IsLobby
           && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost
           && option is not (PresetOptionItem or ObjectOptionitem or TextOptionItem);

    public static void OnChanged(OptionItem option, string before, string after)
    {
        try
        {
            if (!pending.TryGetValue(option, out var c))
                pending[option] = c = new Change { Option = option, Before = before };
            c.After = after;
            lastChangeAt = Time.time;
        }
        catch { }
    }

    public static void Postfix()
    {
        if (pending.Count == 0 || lastChangeAt < 0f || Time.time - lastChangeAt < QuietSeconds) return;
        try
        {
            if (Enabled != null && Enabled.GetBool() && GameStates.IsLobby && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            {
                var changes = pending.Values.Where(c => c.Before != c.After).ToList();
                if (changes.Count > 0)
                {
                    var sb = new StringBuilder();
                    foreach (var c in changes.Take(MaxLines))
                    {
                        if (sb.Length > 0) sb.Append('\n');
                        sb.Append("・").Append(DisplayName(c.Option)).Append(": ")
                          .Append(Clean(c.Before)).Append(" → ").Append(Clean(c.After));
                    }
                    if (changes.Count > MaxLines) sb.Append('\n').Append(string.Format(GetString("SettingChangeNotice.More"), changes.Count - MaxLines));
                    Utils.SendMessage(sb.ToString(), byte.MaxValue,
                        Utils.ColorString(new Color32(127, 216, 255, 255), GetString("SettingChangeNotice.Title")));
                }
            }
        }
        catch (System.Exception e)
        {
            Logger.Warn($"設定の変わりのお知らせに失敗: {e.Message}", nameof(SettingChangeNotice));
        }
        pending.Clear();
        lastChangeAt = -1f;
    }

    static string Clean(string s) => (s ?? "").RemoveHtmlTags().Trim();

    /// <summary>役職の設定は「シェリフのキルクール」のように役職の名前を付ける (「キルクール」だけではどの役職か分からない)</summary>
    static string DisplayName(OptionItem option)
    {
        var name = Clean(option.GetName(disableColor: true));
        roleOf ??= Options.CustomRoleSpawnChances.ToDictionary(kv => (OptionItem)kv.Value, kv => kv.Key);
        for (var o = option; o != null; o = o.Parent)
        {
            if (!roleOf.TryGetValue(o, out var role)) continue;
            if (o == option) return name; // 役職そのものの出る率
            var roleName = Clean(Utils.GetRoleName(role));
            return string.IsNullOrEmpty(roleName) || name.Contains(roleName) ? name : string.Format(GetString("SettingChangeNotice.RoleOf"), roleName, name);
        }
        return name;
    }
}
