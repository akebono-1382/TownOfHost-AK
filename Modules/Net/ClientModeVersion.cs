using System;
using HarmonyLib;
using Hazel;
using TownOfHostL.Core.Modes;

namespace TownOfHostL.Modules.Net;

/// <summary>
/// 客の間 (<see cref="TownOfHostL.Core.VanillaHideAndSeek.IsClientMode"/>) は電文の受け口 (Standard) を外しているので、
/// ホストが TOHL かどうかを左上に出すためのホストの版だけをここで受ける。ホスト以外から来た物は読まない。
/// 客の側からは何も送らない (ホストは入った人に版を送り直す)。
/// </summary>
[HarmonyPatch, PatchBundle(PatchBundles.Core)]
public static class ClientModeVersion
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc)), HarmonyPostfix]
    public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != (byte)CustomRPC.VersionCheck || !TownOfHostL.Core.VanillaHideAndSeek.IsClientMode) return;
        var client = AmongUsClient.Instance;
        if (client == null || __instance == null || __instance.OwnerId != client.HostId) return;
        try
        {
            var sub = MessageReader.Get(reader);
            var version = Version.Parse(sub.ReadString());
            var tag = sub.ReadString();
            var forkId = 3 <= version.Major ? sub.ReadString() : Main.OriginalForkId;
            sub.Recycle();
            Main.playerVersion[__instance.PlayerId] = new PlayerVersion(version, tag, forkId);
        }
        catch (Exception ex)
        {
            Logger.Warn($"ホストの版を読めませんでした: {ex.Message}", "ClientMode");
        }
    }
}
