using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

// ロビーの右の板の「プライバシー」の欄を、今日の日付と時刻の欄に替える。
// 部屋は非公開のまま: 公開・非公開を切り替えるボタン (Privacy (Interactive)) は隠して押せないようにし、
// 押せない方の欄 (Privacy (Not Interactive): 黒い箱に「ローカル」などの字) を出して、字を日時に替える
public static class LobbyClock
{
    private const string Root = "AspectSize/GameSettingsButtons/";

    private static TextMeshPro clock;
    private static TextMeshPro vanillaText;
    private static int shownMinute = -1;

    [TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
    [HarmonyPatch(typeof(LobbyInfoPane), nameof(LobbyInfoPane.Update))]
    static class UpdatePatch
    {
        public static void Postfix(LobbyInfoPane __instance)
        {
            try { Apply(__instance); }
            catch (Exception e) { Logger.Warn($"日時の欄を出せませんでした: {e.Message}", nameof(LobbyClock)); }
        }
    }

    private static void Apply(LobbyInfoPane pane)
    {
        if (!pane) return;
        if (!clock && !Build(pane)) return;

        // 本編は部屋の状態に合わせて 2 つの欄を出し直すので、毎回押せない方だけを出す
        var t = pane.transform;
        var interactive = t.Find(Root + "Privacy (Interactive)");
        if (interactive && interactive.gameObject.activeSelf) interactive.gameObject.SetActive(false);
        var fixedBox = clock.transform.parent;
        if (fixedBox && !fixedBox.gameObject.activeSelf) fixedBox.gameObject.SetActive(true);
        if (vanillaText && vanillaText.enabled) vanillaText.enabled = false;

        var now = DateTime.Now;
        int minute = now.Hour * 60 + now.Minute;
        if (minute == shownMinute) return;
        shownMinute = minute;
        var days = GetString("LobbyClock.Weekdays").Split(',');
        string day = days.Length == 7 ? days[(int)now.DayOfWeek] : now.DayOfWeek.ToString()[..3];
        clock.text = $"{now.Month}/{now.Day}({day}) {now:HH:mm}";
    }

    private static bool Build(LobbyInfoPane pane)
    {
        var t = pane.transform;
        var src = t.Find(Root + "Privacy (Not Interactive)/PrivacyText");
        vanillaText = src ? src.GetComponent<TextMeshPro>() : null;
        if (!vanillaText) return false;

        // 本編が書き直さない写しの字を同じ所に重ねる
        var copy = UnityEngine.Object.Instantiate(vanillaText.gameObject, src.parent);
        copy.name = "Tohl_LobbyClock";
        copy.transform.localPosition = src.localPosition;
        foreach (var tr in copy.GetComponentsInChildren<TextTranslatorTMP>(true)) UnityEngine.Object.Destroy(tr);
        clock = copy.GetComponent<TextMeshPro>();
        clock.enabled = true;
        clock.enableAutoSizing = true;
        clock.fontSizeMin = 1f;
        clock.fontSizeMax = Mathf.Max(vanillaText.fontSize, 1f);
        shownMinute = -1;

        // 見出しの「プライバシー」を「日時」に
        var label = t.Find("AspectSize/PrivacyLabel/Text_TMP");
        if (label)
        {
            foreach (var tr in label.GetComponents<TextTranslatorTMP>()) UnityEngine.Object.Destroy(tr);
            var tmp = label.GetComponent<TextMeshPro>();
            if (tmp) tmp.text = GetString("LobbyClock.Label");
        }
        return true;
    }
}
