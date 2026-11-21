using System;
using System.Collections.Generic;
using Hazel;
using TownOfHostL.Attributes;
using static TownOfHostL.Translator;

namespace TownOfHostL.Modules;

/// <summary>
/// 客から届いた電文のうち、本編の客なら送らない物の受け口 (ホストのみ)。
/// ホストの手元で呼ぶ処理 (役職の能力の会議・暗転対策など) はこの段を通らないので、ここで見るのは客が送った物だけ。
///
/// 頼みの電文 (ホストが判断して結果を配る物) は、ありえない物を捨てても客どうしが食い違わないので、設定に依らず捨てる。
/// ただし局面の境目に遅れて届く正当な頼みと見分けきれないので、知らせるだけで追い出さない。
/// 結果の電文 (本来ホストだけが送る物) は、全員宛てなら他の客はもう受けているので、捨てるとホストだけ食い違う
/// (例: キルを捨てると客の画面では死んでいるのにホストでは生きていて、投票がそろわない)。
/// そのため受けて状態をそろえ、設定に従って知らせるか追い出す。
///
/// 本編の電文は宛先の PlayerControl に届き、送り主の接続は分からない。知らせる名前と追い出す相手は宛先の持ち主で、
/// 本人の物以外を名乗った電文では別の人になりうるので、BAN の一覧には足さない。
/// </summary>
public static class RpcGuard
{
    /// <summary>この試合で記録した (持ち主, 電文) の組。同じ組は 1 度だけ記録して知らせる</summary>
    static readonly HashSet<(int owner, byte callId)> seen = new();
    /// <summary>この試合で追い出しを出した持ち主。同じ人へ何度も出さない</summary>
    static readonly HashSet<int> kicked = new();
    /// <summary>変身の断りを最後に返した時刻 (持ち主ごと)。連投されても返事を増やさない</summary>
    static readonly Dictionary<int, float> lastReject = new();

    [GameModuleInitializer]
    public static void Init()
    {
        seen.Clear();
        kicked.Clear();
        lastReject.Clear();
    }

    /// <summary>true を返した電文は本編へ渡さずに捨てる。ここで例外が出た時は捨てずに本編へ渡す</summary>
    public static bool ShouldDrop(PlayerControl pc, RpcCalls call, MessageReader reader)
    {
        try { return Check(pc, call, reader); }
        catch (Exception ex) { Logger.Exception(ex, nameof(RpcGuard)); return false; }
    }

    static bool Check(PlayerControl pc, RpcCalls call, MessageReader reader)
    {
        if (!AmongUsClient.Instance.AmHost || pc == null) return false;
        // 試合の中 (イントロから結果画面の前まで) だけを見る (ロビーのキルは既存の設定が受け持つ)
        if (!GameStates.IsInGame) return false;

        switch (call)
        {
            // ── 頼み ──
            case RpcCalls.CheckName:
                // 名前の申請は部屋に入った時だけ。試合の途中で通すと、名前の表示の仕組みが組んだ名前を上書きされる
                Detected(pc, call, kickable: false);
                return true;
            case RpcCalls.CheckShapeshift:
                {
                    // 会議中に本編の客が送るのは、会議の始めの「自分へ戻る」(演出なし) と、
                    // 会議の直前に押した変身の行き違い (演出あり・既存の判定が断る) だけ。演出なしで他人へ変わる頼みは捨てる
                    if (!GameStates.IsMeeting) return false;
                    var sub = MessageReader.Get(reader);
                    try
                    {
                        uint targetNetId = sub.ReadPackedUInt32();
                        bool animate = sub.ReadBoolean();
                        if (targetNetId == pc.NetId || animate) return false;
                    }
                    catch { return false; }
                    finally { sub.Recycle(); }
                    float now = UnityEngine.Time.realtimeSinceStartup;
                    if (!lastReject.TryGetValue(pc.OwnerId, out var last) || now - last >= 1f)
                    {
                        lastReject[pc.OwnerId] = now;
                        pc.RpcRejectShapeshift();
                    }
                    Detected(pc, call, kickable: false);
                    return true;
                }

            // ── 結果 (本来ホストだけが送る) ──
            case RpcCalls.MurderPlayer:
            case RpcCalls.Exiled:
            case RpcCalls.SetRole:
            case RpcCalls.StartMeeting:
            case RpcCalls.ProtectPlayer:
            case RpcCalls.Shapeshift:
            case RpcCalls.RejectShapeshift:
            case RpcCalls.StartVanish:
            case RpcCalls.StartAppear:
            case RpcCalls.SetName:
            case RpcCalls.SetColor:
            case RpcCalls.SendChatNote:
            case RpcCalls.UseZipline:
            case RpcCalls.TriggerSpores:
                Detected(pc, call, kickable: true);
                return false;
        }
        return false;
    }

    static void Detected(PlayerControl pc, RpcCalls call, bool kickable)
    {
        int owner = pc.OwnerId;
        if (!seen.Add((owner, (byte)call))) return;
        string name = pc.Data?.PlayerName ?? $"#{pc.PlayerId}";
        Logger.Warn($"本編の客なら送らない電文: {call} (宛先 {name}・持ち主 {owner})", nameof(RpcGuard));

        var mode = Options.CheatIllegalRpc?.GetBool() == true ? Options.CheatIllegalRpc.GetValue() : 0;
        if (mode == 0) return;
        Logger.SendInGame(string.Format(GetString("CheatIllegalRpcNotice"), name, call));

        // ホストの PlayerControl を名乗った電文ではホスト自身を追い出さない。
        // 版を名乗った TOH 系の Mod の客は、その Mod の仕組みで結果の電文を出しうるので知らせるだけにする
        if (mode != 2 || !kickable || pc.AmOwner || pc.IsModClient() || !kicked.Add(owner)) return;
        var client = pc.GetClient();
        if (client == null) return;
        Logger.SendInGame(string.Format(GetString("CheatIllegalRpcKicked"), name));
        AmongUsClient.Instance.KickPlayer(client.Id, false);
    }
}
