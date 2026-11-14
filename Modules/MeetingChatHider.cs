using System.Collections.Generic;
using System.Linq;

using Hazel;

using TownOfHost.Modules;
using TownOfHost.Patches;
using static TownOfHost.Utils;
using static TownOfHost.PlayerCatch;

namespace TownOfHost
{
    /// <summary>
    /// 会議中のチャット秘匿。公式の /cmd には依存しない。
    ///  ・会議開始 0.2 秒後 : 各バニラクライアントから見て「自分以外」を死亡扱いにする
    ///                        (死亡者のチャットは生存者に表示されないので、コマンドが他人に見えない)
    ///  ・コマンド以外が来た : 送信者をそのクライアントにだけ一時的に生き返らせ、チャットを送り、再び死亡扱いに戻す
    /// ※ホスト自身は死亡扱いにしない (システムメッセージが見えなくなるため)
    /// ※MODクライアントは「/」始まりを自前でキャンセルするので対象外
    /// </summary>
    public static class MeetingChatHider
    {
        /// <summary>死亡扱いの送信が完了しているか</summary>
        public static bool Applied { get; private set; }

        private const float ApplyDelay = 0.2f;
        /// <summary>1パケットに詰めるPlayerInfoの数 (大きすぎるとパケットが膨らむため分割)</summary>
        private const int ChunkSize = 5;

        private static bool CanUse =>
            AmongUsClient.Instance.AmHost
            && Options.ExHideChatCommand.GetBool()
            && !IsRestriction()
            && GameStates.CalledMeeting && GameStates.IsMeeting
            && !AntiBlackout.IsSet && !AntiBlackout.IsCached;

        /// <summary>死亡扱いを見せる相手 (生存中・バニラクライアント・ホスト以外)</summary>
        private static bool IsViewer(PlayerControl pc) =>
            pc != null && pc.IsAlive()
            && pc.PlayerId != PlayerControl.LocalPlayer.PlayerId
            && !pc.IsModClient()
            && pc.GetClientId() != -1;

        /// <summary>会議開始時に呼ぶ (MeetingHudPatch.StartPatch の Postfix など)</summary>
        public static void OnMeetingStart()
        {
            Applied = false;
            if (!AmongUsClient.Instance.AmHost) return;

            _ = new LateTask(() =>
            {
                if (!CanUse) return;
                Applied = true;

                foreach (var viewer in AllAlivePlayerControls.Where(IsViewer).ToArray())
                {
                    // viewer から見て「自分以外」(ホストは除く) を死亡扱いにする
                    var targets = AllAlivePlayerControls
                        .Where(p => p.PlayerId != viewer.PlayerId && p.PlayerId != PlayerControl.LocalPlayer.PlayerId)
                        .ToArray();
                    for (int i = 0; i < targets.Length; i += ChunkSize)
                        SendDataTo(viewer, targets.Skip(i).Take(ChunkSize), isDead: true);
                }
            }, ApplyDelay, "MeetingChatHide");
        }

        /// <summary>会議終了時に呼ぶ (MeetingHud の終了処理)。本来の生死状態を全員に戻す</summary>
        public static void OnMeetingEnd()
        {
            if (!Applied) return;
            Applied = false;
            if (AmongUsClient.Instance.AmHost) RPC.RpcSyncAllNetworkedPlayer();
        }

        /// <summary>
        /// コマンド以外のチャットを、死亡扱いで見えなくなっているクライアントへ届ける。
        /// 送信者を生き返らせる → 名前設定 → チャット送信 → 再び死亡扱い、を個別に送る。
        /// </summary>
        public static void RelayChat(PlayerControl sender, string text)
        {
            if (!Applied || !CanUse) return;
            // ホストは死亡扱いにしていない / 死亡者のチャットは元々死亡者にしか見えない
            if (sender == null || sender.PlayerId == PlayerControl.LocalPlayer.PlayerId || !sender.IsAlive()) return;

            foreach (var viewer in AllAlivePlayerControls.Where(IsViewer).ToArray())
            {
                if (viewer.PlayerId == sender.PlayerId) continue;

                string name = sender.GetRealName(isMeeting: true).ApplyNameColorData(viewer, sender, true);

                var s = CustomRpcSender.Create("MeetingChatRelay", SendOption.Reliable);
                s.StartMessage(viewer.GetClientId());
                GameDataSerializePatch.SerializeMessageCount++;
                try
                {
                    WriteData(s, sender, isDead: false); // 生き返らせる
                    s.StartRpc(sender.NetId, (byte)RpcCalls.SetName)
                        .Write(sender.NetId)
                        .Write(name)
                        .EndRpc();
                    s.StartRpc(sender.NetId, (byte)RpcCalls.SendChat)
                        .Write(text)
                        .EndRpc();
                    WriteData(s, sender, isDead: true);  // 再び死亡扱い
                    s.EndMessage();
                    s.SendMessage();
                }
                finally
                {
                    GameDataSerializePatch.SerializeMessageCount--;
                }
            }
        }

        private static void SendDataTo(PlayerControl viewer, IEnumerable<PlayerControl> targets, bool isDead)
        {
            var sender = CustomRpcSender.Create("MeetingChatHide", SendOption.Reliable);
            sender.StartMessage(viewer.GetClientId());
            GameDataSerializePatch.SerializeMessageCount++;
            try
            {
                foreach (var target in targets) WriteData(sender, target, isDead);
                sender.EndMessage();
                sender.SendMessage();
            }
            finally
            {
                GameDataSerializePatch.SerializeMessageCount--;
            }
        }

        /// <summary>target の PlayerInfo を isDead の値で書き込む (ホスト側の本来の値は即座に戻す)</summary>
        private static void WriteData(CustomRpcSender sender, PlayerControl target, bool isDead)
        {
            var data = target.Data;
            var original = data.IsDead;
            data.IsDead = isDead;
            try
            {
                sender.Write((wit) =>
                {
                    wit.StartMessage(1); //0x01 Data
                    {
                        wit.WritePacked(data.NetId);
                        data.Serialize(wit, false);
                    }
                    wit.EndMessage();
                }, true);
            }
            finally
            {
                data.IsDead = original;
            }
        }
    }
}
