using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public sealed class Blinker : RoleBase, IImpostor, IUsePhantomButton
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Blinker),
                player => new Blinker(player),
                CustomRoles.Blinker,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                28200,
                SetupOptionItem,
                "Bkr",
                OptionSort: (4, 6)
            );
        public Blinker(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            WarpPos = null;
        }
        static OptionItem KillCoolDown;
        static OptionItem CoolDown;
        static OptionItem MeetingKeep;
        Vector2? WarpPos;
        enum Option
        {
            BlinkerMeetingKeep,
        }

        private static void SetupOptionItem()
        {
            KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, new(0, 180, 0.5f), 20f, false)
                .SetValueFormat(OptionFormat.Seconds);
            MeetingKeep = BooleanOptionItem.Create(RoleInfo, 5, Option.BlinkerMeetingKeep, false, false);
        }
        public float CalculateKillCooldown() => KillCoolDown.GetFloat();
        public bool CanUseKillButton() => WarpPos is not null;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = CoolDown.GetFloat();
        }
        public void OnCheckMurderAsKiller(MurderInfo info)
        {
            info.DoKill = WarpPos is not null;
        }
        public void OnMurderPlayerAsKiller(MurderInfo info)
        {
            if (WarpPos is null) return;
            _ = new LateTask(() =>
            {
                Player.RpcSnapToForced(WarpPos ?? Player.transform.position);
                WarpPos = null;
            }, 0.5f);
        }
        public override void AfterMeetingTasks()
        {
            if (MeetingKeep.GetBool() is false) return;

            WarpPos = null;
        }
        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = true;
            ResetCooldown = true;
            if (WarpPos is not null)
            {
                ResetCooldown = false;
                return;
            }

            WarpPos = Player.transform.position;
        }
    }
}