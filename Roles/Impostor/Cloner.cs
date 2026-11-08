using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Cloner : RoleBase, IImpostor, IUsePhantomButton, IPetTrigger
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Cloner),
                player => new Cloner(player),
                CustomRoles.Cloner,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                26200,
                SetupOptionItem,
                "Clr",
                OptionSort: (6, 13)
            );
        public Cloner(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            player = null;
            now = 0;
            use = OptUseCount.GetInt() == 0 ? -50 : OptUseCount.GetInt();
        }
        static OptionItem KillCoolDown;
        static OptionItem CoolDown;
        static OptionItem OptUseCount;
        int use;
        public static IEnumerable<PlayerControl> AllDeadPlayerControls =>
            PlayerCatch.AllPlayerControls.ToArray().Where(p => p != null && !p.IsAlive() &&
            (PlayerCatch.OldAlivePlayerControles.Contains(p) ||
            UtilsGameLog.day <= 1));

        int now;
        private static void SetupOptionItem()
        {
            KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptUseCount = IntegerOptionItem.Create(RoleInfo, 5, GeneralOption.OptionCount, new(0, 99, 1), 0, false)
                .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        }
        public float CalculateKillCooldown() => KillCoolDown.GetFloat();
        bool IUsePhantomButton.IsPhantomRole => use > 0 || use < -20;
        bool IUsePhantomButton.IsresetAfterKill => false;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = CoolDown.GetFloat();
        }

        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = true;
            ResetCooldown = true;

            if (use <= 0 && use > -20) return;

            if (use > -20)
            {
                use--;
            }

            if (!AmongUsClient.Instance.AmHost) return;

            var pc = AllDeadPlayerControls.ToArray()[now];

            if (pc == null || pc.IsAlive()) return;
            var original = pc.transform.position;
            SendRPC();
            pc.RpcSnapToForced(Player.transform.position);
            RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);

            //キルモーション出さないためにターゲット本人は除外
            foreach (var seer in PlayerCatch.AllPlayerControls.Where(p => p.PlayerId != pc.PlayerId))
            {
                pc.RpcMurderPlayerDesync(pc, seer);
            }
            pc.RpcSnapToForced(original);
        }

        bool IPetTrigger.OnPetUsed()
        {
            if (AllDeadPlayerControls.Count() <= 0) return false;

            now++;
            if (now >= AllDeadPlayerControls.Count())
            {
                now = 0;
            }
            UtilsNotifyRoles.NotifyRoles(OnlyMeName: true, SpecifySeer: Player);
            return true;
        }

        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            if (AllDeadPlayerControls.Count() <= 0) return "";

            var pc = AllDeadPlayerControls.ToArray()[now];
            var name = UtilsName.GetPlayerColor(AllDeadPlayerControls.ToArray()[now]);

            return string.Format(GetString("ClonerLower"), name); ;
        }

        public override string GetProgressText(bool comms = false, bool GameLog = false)
            => use < -20 ? "" : Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Cloner), $"({use})");

        void SendRPC()
        {
            using var sender = CreateSender();
            sender.Writer.WritePacked(use);
        }

        public override void ReceiveRPC(MessageReader reader)
        {
            use = reader.ReadPackedInt32();
        }
    }
}
