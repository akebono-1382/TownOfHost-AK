// Based on https://github.com/haru03741-glitch/More-Roles-Plus src/Menu/ChatNotes.cs (GPL-3.0)
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

    /// <summary>部屋に入った時に呼ぶ (前の部屋で出せなかった返事を次の部屋で出さない)</summary>
    public static void Reset() => pending.Clear();

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

    // 部屋に入るたびに捨てる。部屋に入る処理の本体は通常モードの束にあり、本編のかくれんぼの間は外れるので、ここで別に受ける
    [TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
    static class GameJoinedPatch
    {
        public static void Postfix() => Reset();
    }

    // 本編のかくれんぼの間 (通常モードの束を外す) にも返事を出すので、どのモードでも当てる
    [TownOfHostL.Core.Modes.PatchBundle(TownOfHostL.Core.Modes.PatchBundles.Core)]
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
