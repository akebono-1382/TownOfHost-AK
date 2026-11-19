using System.Collections.Generic;
using HarmonyLib;

namespace TownOfHostL.Modules;

/// <summary>
/// 自分の画面だけに出す返事 (コマンドの結果など) をチャット欄に足す。
/// チャット欄が隠れている間 (部屋に着いた直後・試合の読み込み中・操作盤のボタンから呼んだ時など) に足すと
/// 本編の表示の処理が途中で止まるので、欄が出るまで溜めておく
/// </summary>
public static class HostNote
{
    private const int MaxPending = 20;
    private static readonly List<string> pending = new();

    public static void Post(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (pending.Count == 0 && TryAdd(text)) return;
        if (pending.Count >= MaxPending) pending.RemoveAt(0);
        pending.Add(text);
    }

    private static bool Visible(out ChatController chat)
    {
        chat = HudManager.InstanceExists ? HudManager.Instance.Chat : null;
        return chat != null && chat.gameObject.activeInHierarchy && PlayerControl.LocalPlayer != null;
    }

    private static bool TryAdd(string text)
    {
        if (!Visible(out var chat)) return false;
        chat.AddChat(PlayerControl.LocalPlayer, text);
        return true;
    }

    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    static class HudUpdatePatch
    {
        public static void Postfix()
        {
            if (pending.Count == 0 || !Visible(out var chat)) return;
            // 先に取り出して空にする (足す途中で例外が出ても、毎フレーム同じ物を足し直さない)
            var notes = pending.ToArray();
            pending.Clear();
            foreach (var text in notes)
            {
                try { chat.AddChat(PlayerControl.LocalPlayer, text); }
                catch (System.Exception e) { Logger.Warn($"返事を出せませんでした: {e.Message}", "HostNote"); }
            }
        }
    }
}
