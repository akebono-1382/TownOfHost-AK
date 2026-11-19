using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;

namespace TownOfHostL.Modules.Net;

/// <summary>
/// TOHL の独自の電文 (<see cref="CustomRPC"/>) のうち、版の知らせ以外を送らずに捨てる関所。
/// 客の TOHL は客の間ホストの処理と受け口を外して本編の客のまま動くので、独自の電文を読む人がいない。
/// 送っても通信が増えるだけで、役職の同期などは役職の覗き見にも使える。
/// 役職ごとの送る処理はそのまま書き、送り口 (StartRpcImmediately / FinishRpcImmediately) だけで止める。
/// </summary>
[HarmonyPatch]
public static class CustomRpcGate
{
    // 止めた電文の書き込み先。FinishRpcImmediately で見分けて捨てる
    private static readonly HashSet<IntPtr> dropped = new();

    /// <summary>送ってよい独自の電文 (版の知らせと、その頼み直し)</summary>
    private static bool Allowed(byte callId)
        => callId is (byte)CustomRPC.VersionCheck or (byte)CustomRPC.RequestRetryVersionCheck;

    public static bool IsDropped(byte callId)
        => callId >= (byte)CustomRPC.VersionCheck && Enum.IsDefined(typeof(CustomRPC), (int)callId) && !Allowed(callId);

    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.StartRpcImmediately)), HarmonyPostfix]
    public static void StartPostfix([HarmonyArgument(1)] byte callId, MessageWriter __result)
    {
        if (__result == null) return;
        // 書き込み先は使い回されるので、送る電文に使われた時は印を外す (止めた電文が途中の例外で閉じられずに残っても、後の電文を捨てない)
        if (IsDropped(callId)) dropped.Add(__result.Pointer);
        else if (dropped.Count > 0) dropped.Remove(__result.Pointer);
    }

    [HarmonyPatch(typeof(InnerNet.InnerNetClient), nameof(InnerNet.InnerNetClient.FinishRpcImmediately)), HarmonyPrefix]
    public static bool FinishPrefix([HarmonyArgument(0)] MessageWriter msg)
    {
        if (msg == null || dropped.Count == 0 || !dropped.Remove(msg.Pointer)) return true;
        msg.Recycle();
        return false;
    }
}
