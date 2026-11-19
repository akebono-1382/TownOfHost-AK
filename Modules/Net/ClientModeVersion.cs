using System;
using HarmonyLib;
using Hazel;
using TownOfHostL.Core.Modes;

namespace TownOfHostL.Modules.Net;

/// <summary>
/// 客の間 (<see cref="TownOfHostL.Core.VanillaHideAndSeek.IsClientMode"/>) は電文の受け口 (Standard) を外しているので、
/// ホストが TOHL かどうかを左上に出すためのホストの版だけをここで受ける。ホスト以外から来た物は読まない。
/// </summary>
[HarmonyPatch, PatchBundle(PatchBundles.Core)]
public static class ClientModeVersion
{
    /// <summary>
    /// 自分の版を知らせ、ホストに版を送り直してもらう。ホストの版は入った直後 (客の間に切り替わる前) に届いて読み落とすため。
    /// 自分の PlayerControl が出来るまで待つ。ホストはこの 2 つの電文だけは客から受け付ける
    /// </summary>
    public static void Exchange(int tries = 20)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.AmHost || !TownOfHostL.Core.VanillaHideAndSeek.IsClientMode) return;
        if (PlayerControl.LocalPlayer == null)
        {
            if (tries > 0) _ = new LateTask(() => Exchange(tries - 1), 0.5f, "ClientModeVersion");
            return;
        }
        RPC.RpcVersionCheck();
        var writer = client.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.RequestRetryVersionCheck, SendOption.Reliable, client.HostId);
        client.FinishRpcImmediately(writer);
    }

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
