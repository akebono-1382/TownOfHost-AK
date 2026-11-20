using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

// ロビーの右の板の「プライバシー」の欄を、今日の日付と時刻の欄に替える。
// 部屋は非公開のまま: 公開・非公開を切り替えるボタンは隠して押せないようにする
public static class LobbyClock
{
    private const string Name = "Tohl_LobbyClock";

    private static TextMeshPro text;
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
        // 本編は公開・非公開のボタンを状態に合わせて出し直すので、毎回隠す
        var anchor = Hide(pane.HostPrivateButton);
        var other = Hide(pane.HostPublicButton);
        anchor ??= other;

        if (!text)
        {
            if (anchor == null) return;
            Build(pane, anchor);
            if (!text) return;
            shownMinute = -1;
        }

        var now = DateTime.Now;
        int minute = now.Hour * 60 + now.Minute;
        if (minute == shownMinute) return;
        shownMinute = minute;
        var days = GetString("LobbyClock.Weekdays").Split(',');
        string day = days.Length == 7 ? days[(int)now.DayOfWeek] : now.DayOfWeek.ToString()[..3];
        text.text = $"{now.Month}/{now.Day}({day}) {now:HH:mm}";
    }

    // ボタンを隠して、隠す前の置き場を返す (無ければ null)
    private static Transform Hide(UiElement button)
    {
        if (!button) return null;
        var go = button.gameObject;
        if (go.activeSelf) go.SetActive(false);
        return go.transform;
    }

    // プリセットの値の箱 (黒い箱に白い字) を写して、ボタンの所に置く。見出しの「プライバシー」は「日時」に書き換える
    private static void Build(LobbyInfoPane pane, Transform anchor)
    {
        var box = pane.transform.Find("AspectSize/ModeValue");
        if (!box) return;
        var copy = UnityEngine.Object.Instantiate(box.gameObject, box.parent);
        copy.name = Name;
        copy.transform.position = new Vector3(box.position.x, anchor.position.y, box.position.z);
        foreach (var t in copy.GetComponentsInChildren<TextTranslatorTMP>(true)) UnityEngine.Object.Destroy(t);
        foreach (var b in copy.GetComponentsInChildren<Collider2D>(true)) b.enabled = false;
        text = copy.GetComponentInChildren<TextMeshPro>(true);
        if (text)
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 1f;
            text.fontSizeMax = 3.1f;
        }

        foreach (var label in pane.GetComponentsInChildren<TextTranslatorTMP>(true))
        {
            if (label.TargetText != StringNames.PrivacyLabel) continue;
            var tmp = label.GetComponent<TextMeshPro>();
            UnityEngine.Object.Destroy(label);
            if (tmp) tmp.text = GetString("LobbyClock.Label");
        }
    }
}
