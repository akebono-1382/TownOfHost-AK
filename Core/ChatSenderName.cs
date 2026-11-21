using System;

using TownOfHostL;

namespace TownOfHostL.Core;

/// <summary>
/// チャットを流すために送り手の名前を見出しに書き換えた後、客の画面に戻す名前を決める。
/// 試合中、ホストの手元にある名前はホストから見た表示 (役職・タスク数・役職の色) に書き換わっているので、
/// それを戻すと客に送り手の役職が見えてしまう。試合中は、NotifyRoles が他人の名前として送る本当の名前で戻す。
/// </summary>
public static class ChatSenderName
{
    private static int resendToken;

    // 公式サーバーは 1 パケット 999 バイトまでの名前の書き換えを通し、1000 バイトを超えると届かずに切断することがある。
    // チャット 1 通は「見出しの名前に本文を入れる書き換え + チャット + 名前の戻し」を 1 パケットに入れるので、
    // 本文は戻す名前とヘッダの分を引いた残りに収める
    public const int PacketLimit = 990;
    private const int Overhead = 80;
    // 名前を借りる人が死んでいる時 (生きている人が居ない時のホスト) は、前後に自分の情報 (NetworkedPlayerInfo) を
    // 1 回ずつ付けて生きている扱いにして送る
    private const int DeadHostInfo = 300;
    private const int MinBody = 300;

    /// <summary>
    /// お知らせを送るのに名前を借りる人。ふだんはホスト。試合中にホストが死んでいる時は、生きている中で ID が最小の人の
    /// 名前を見出しに付け替えて送る (死んだ人の書き込みは生きている人に見えないので、ホストのままだと宛先ごとに
    /// 生き返った扱いの情報を挟んで送り分けることになる)。生きている人が居なければホスト
    /// </summary>
    public static PlayerControl Sender()
    {
        var host = PlayerControl.LocalPlayer;
        if (host == null || host.Data == null || !host.Data.IsDead || !GameStates.IsInGame) return host;
        PlayerControl best = null;
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc == null || pc.AmOwner || pc.Data == null || pc.Data.IsDead || pc.Data.Disconnected) continue;
            if (best == null || pc.PlayerId < best.PlayerId) best = pc;
        }
        return best ?? host;
    }

    /// <summary>1 通の本文 (見出しを含む) に使えるバイト数</summary>
    public static int BodyBudget(PlayerControl sender)
    {
        if (sender == null || sender.Data == null) return MinBody;
        int budget = PacketLimit - Overhead - System.Text.Encoding.UTF8.GetByteCount(RestoreFor(sender) ?? "");
        // ホストが死んでいる間は、名前を借りる人が送るまでに居なくなると情報を挟む送り分けになるので、その分を常に空ける
        // (送信の関所は大きなパケットを分けるが、分けると情報とチャットの届く順が崩れる)
        var host = PlayerControl.LocalPlayer;
        if (GameStates.IsInGame && (sender.Data.IsDead || (host != null && host.Data != null && host.Data.IsDead))) budget -= DeadHostInfo;
        return Math.Max(budget, MinBody);
    }

    /// <summary>組み終えたパケットが上限を超えていたら記録する (本文の予算の見積もりが外れた時の手がかり)</summary>
    public static void CheckPacket(Hazel.MessageWriter stream, string tag)
    {
        if (stream != null && stream.Length > PacketLimit)
            Logger.Warn($"チャットのパケットが {stream.Length} バイトで上限 {PacketLimit} を超えています", tag);
    }

    /// <summary>
    /// 客の画面に戻す名前。ロビーでは今の名前 (名前の飾りを含む) をそのまま戻す。
    /// 会議の外では NotifyRoles が他人の名前として送る形に合わせる (変身中は変身先の名前・通信妨害のカモフラ中は隠す)。
    /// 1 人宛ての時は、その人に最後に送った名前をそのまま戻す
    /// </summary>
    public static string RestoreFor(PlayerControl sender, PlayerControl seer = null)
    {
        if (!GameStates.IsInGame) return sender.Data.PlayerName;
        if (GameStates.IsMeeting) return sender.GetRealName(isMeeting: true);
        var name = Main.AllPlayerNames.TryGetValue(sender.PlayerId, out var realName) ? realName : sender.Data.PlayerName;
        if (Main.CheckShapeshift.TryGetValue(sender.PlayerId, out var shifted) && shifted
            && Main.ShapeshiftTarget.TryGetValue(sender.PlayerId, out var shiftTo) && shiftTo != sender.PlayerId
            && Main.AllPlayerNames.TryGetValue(shiftTo, out var shiftName))
            name = shiftName;
        if (NameParts.HideSeenName(false)) name = $"<size=0%>{name}</size>";
        // 最後に送った名前 (役職の行や印を含む) が長い時は使わない。本文の予算は宛先に関わらない名前の長さで見積もっている
        if (seer != null && !seer.AmOwner && Main.LastNotifyNames != null
            && Main.LastNotifyNames.TryGetValue((sender.PlayerId, seer.PlayerId), out var shown) && shown != null
            && System.Text.Encoding.UTF8.GetByteCount(shown) <= Math.Max(System.Text.Encoding.UTF8.GetByteCount(name ?? ""), ShownNameSlack))
            return shown;
        return name;
    }
    // 最後に送った名前を戻す時に、本名より長くてもよいバイト数 (キノコカオスの隠し名などが収まる長さ・Overhead の内)
    private const int ShownNameSlack = 32;

    /// <summary>
    /// 全員宛ての 1 通を、宛先ごとに分けて送るか。キノコカオスで名前を隠した人が居る間は、戻す名前が人によって違い、
    /// NotifyRoles も止めているので送り直しで隠し直せない
    /// </summary>
    public static bool SplitsBroadcast()
        => GameStates.IsInGame && !GameStates.IsMeeting && MushroomMixupUpdateSystemPatch.HidesNamesForAnyone;

    /// <summary>
    /// 戻した名前を、送った名前の覚え書きに写す。clientId が -1 なら全員に戻している。
    /// その後、本来の表示 (仲間の色など) を送り直すのは、メッセージの続きが止んでから 1 回だけにする。
    /// 戻しの送信と同じフレームで送り直すと、届く順番が入れ替わって本当の名前のまま残ることがある。
    /// </summary>
    public static void Restored(PlayerControl sender, string name, int clientId)
    {
        if (!GameStates.IsInGame || Main.LastNotifyNames == null) return;
        foreach (var seer in Main.AllPlayerControls)
        {
            // ホストの画面は手元の SetName で元の名前に戻している
            if (seer.AmOwner) continue;
            if (clientId != -1 && seer.GetClientId() != clientId) continue;
            Main.LastNotifyNames[(sender.PlayerId, seer.PlayerId)] = name;
        }
        int token = ++resendToken;
        float wait = Math.Max(Main.MessageWait.Value, 0.1f) + 0.3f;
        _ = new LateTask(() =>
        {
            if (token != resendToken || !GameStates.IsInGame) return;
            Utils.NotifyRoles();
        }, wait, "ChatSenderNameResend");
    }
}
