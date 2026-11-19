using System;
using HarmonyLib;
using Hazel;
using TownOfHostL.Core.Modes;

namespace TownOfHostL.Modules.Net;

/// <summary>
/// 客の間 (<see cref="TownOfHostL.Core.VanillaHideAndSeek.IsClientMode"/>) は電文の受け口 (Standard) を外しているので、
/// ホストが TOHL かどうかを左上に出すためのホストの版だけをここで受ける。ホスト以外から来た物は読まない。
/// 客の側からは何も送らない (ホストは入った人に版を送り直す)。
/// 独自の番号の電文は本編へ渡さない。本編は知らない番号を役職の電文として読み、ロビーでは役職が無いので例外になる
/// (例外になると、その後ろの Postfix も走らない)。
/// </summary>
[HarmonyPatch, PatchBundle(PatchBundles.Core)]
public static class ClientModeVersion
{
    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc)), HarmonyPrefix]
    public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (!TownOfHostL.Core.VanillaHideAndSeek.IsClientMode) return true;
        if (callId == (byte)CustomRPC.RequestRetryVersionCheck) return false;
        if (callId != (byte)CustomRPC.VersionCheck) return true;
        var client = AmongUsClient.Instance;
        if (client == null || __instance == null || __instance.OwnerId != client.HostId) return false;
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
        return false;
    }
}
