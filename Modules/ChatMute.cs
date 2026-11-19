// Mute is based on the mute command of https://github.com/Gurge44/EndlessHostRoles Patches/ChatCommandPatch.cs (GPL-3.0)
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

/// <summary>
/// 決めた時間、客の書き込みを止める (ホストだけ)。書き込みそのものは本編のサーバーが全員へ配るので止められない。
/// ミュート中の人が書くと、コマンドとして扱わず、チャット欄を空白で押し出してそれまでの会話を送り直す。
/// 押し出しと本人への知らせは間を空けて送る (連投で送信待ちの列を埋めないように)
/// </summary>
public static class ChatMute
{
    private const int DefaultMinutes = 5;
    private const int MaxMinutes = 60;
    private const float PushOutInterval = 3f;
    private const float NoticeInterval = 5f;

    private static readonly Dictionary<int, float> until = new();    // clientId → 解ける時刻 (realtimeSinceStartup)
    private static readonly Dictionary<int, float> noticedAt = new();
    private static float pushedAt = -100f;

    /// <summary>部屋に入った時に呼ぶ</summary>
    public static void Reset()
    {
        until.Clear();
        noticedAt.Clear();
    }

    private static float Remaining(PlayerControl pc)
    {
        if (pc == null || !until.TryGetValue(pc.OwnerId, out var t)) return 0f;
        float left = t - Time.realtimeSinceStartup;
        if (left > 0f) return left;
        until.Remove(pc.OwnerId);
        return 0f;
    }

    public static bool IsMuted(PlayerControl pc) => Remaining(pc) > 0f;

    /// <summary>客の書き込みが届いた時に呼ぶ (ホストだけ)。ミュート中なら書き込みを片づけて true を返す</summary>
    public static bool OnChat(PlayerControl sender)
    {
        if (sender == null || sender.AmOwner || !AmongUsClient.Instance.AmHost) return false;
        float left = Remaining(sender);
        if (left <= 0f) return false;
        float now = Time.realtimeSinceStartup;
        if (now - pushedAt >= PushOutInterval)
        {
            pushedAt = now;
            ChatPushOut.PushOut();
        }
        if (!noticedAt.TryGetValue(sender.OwnerId, out var last) || now - last >= NoticeInterval)
        {
            noticedAt[sender.OwnerId] = now;
            Utils.SendMessageAutoSplit(string.Format(GetString("Mute.Notice"), Mathf.CeilToInt(left)), true, sendTo: sender.PlayerId);
        }
        return true;
    }

    /// <summary>/mute 相手 [分]・/mute だけで今のミュートの一覧</summary>
    public static void MuteCommand(ChatCommandContext c)
    {
        void Reply(string text)
        {
            HostNote.Post(text);
            if (c.Player != null && !c.Player.AmOwner) Utils.SendMessageAutoSplit(text, true, sendTo: c.Player.PlayerId);
        }
        if (c.Args.Length < 2)
        {
            var list = Main.AllPlayerControls.Where(IsMuted)
                .Select(pc => $"{pc.PlayerId}: {ChatPushOut.Escape(pc.Data?.PlayerName ?? "")} ({Mathf.CeilToInt(Remaining(pc))}s)").ToArray();
            Reply(list.Length == 0 ? GetString("Mute.None") : string.Join("\n", list));
            return;
        }
        int minutes = DefaultMinutes;
        var nameArgs = c.Args[1..];
        if (nameArgs.Length > 1 && int.TryParse(nameArgs[^1], out var m))
        {
            minutes = Math.Clamp(m, 1, MaxMinutes);
            nameArgs = nameArgs[..^1];
        }
        var target = FindTarget(string.Join(" ", nameArgs), c, Reply);
        if (target == null) return;
        until[target.OwnerId] = Time.realtimeSinceStartup + minutes * 60f;
        noticedAt.Remove(target.OwnerId);
        Logger.Info($"ミュート: {target.Data?.PlayerName} {minutes} 分", "ChatMute");
        Reply(string.Format(GetString("Mute.Done"), ChatPushOut.Escape(target.Data?.PlayerName ?? ""), minutes));
        Utils.SendMessageAutoSplit(string.Format(GetString("Mute.Notice"), minutes * 60), true, sendTo: target.PlayerId);
    }

    /// <summary>/unmute 相手</summary>
    public static void UnmuteCommand(ChatCommandContext c)
    {
        void Reply(string text)
        {
            HostNote.Post(text);
            if (c.Player != null && !c.Player.AmOwner) Utils.SendMessageAutoSplit(text, true, sendTo: c.Player.PlayerId);
        }
        if (c.Args.Length < 2)
        {
            Reply(GetString("Mute.UnmuteUsage"));
            return;
        }
        var target = FindTarget(string.Join(" ", c.Args[1..]), c, Reply);
        if (target == null) return;
        until.Remove(target.OwnerId);
        noticedAt.Remove(target.OwnerId);
        Reply(string.Format(GetString("Mute.Lifted"), ChatPushOut.Escape(target.Data?.PlayerName ?? "")));
        Utils.SendMessageAutoSplit(GetString("Mute.LiftedNotice"), true, sendTo: target.PlayerId);
    }

    private static PlayerControl FindTarget(string arg, ChatCommandContext c, Action<string> reply)
    {
        var target = byte.TryParse(arg, out var id) ? Utils.GetPlayerById(id) : null;
        target ??= ChatCommands.FindPlayer(arg);
        if (target == null || target.AmOwner)
        {
            reply(string.Format(GetString("Message.SelectRolesNoPlayer"), arg));
            return null;
        }
        // モデレーターは自分や他のモデレーターをミュートできない
        if (c.Player != null && !c.Player.AmOwner && (target == c.Player || Moderators.Is(target)))
        {
            reply(GetString("Mute.CannotMuteModerator"));
            return null;
        }
        return target;
    }
}
