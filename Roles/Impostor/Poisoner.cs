using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public class PoisonInfo
    {
        public PoisonInfo(byte playerId, int concentration = 0)
        {
            PlayerId = playerId;
            Concentration = concentration;
        }
        //プレイヤーID
        public byte PlayerId;
        //進行度
        public float Progress = 0f;
        //入った毒ガスの濃さ
        public int Concentration;
    }

    public sealed class Poisoner : RoleBase, IImpostor, IUsePhantomButton
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Poisoner),
                player => new Poisoner(player),
                CustomRoles.Poisoner,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                27300,
                SetupOptionItem,
                "Pso",
                OptionSort: (3, 15)
            );
        public Poisoner(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            KillCooldown = OptionKillCoolDown.GetFloat();
            Cooldown = OptionCoolDown.GetFloat();
            DeadTime = OptionDeadTime.GetInt() * 100;

            CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
        }
        enum Op
        {
            PoisonerDeadTime,
        }
        //Key: プレイヤー　Value:秒数
        public static List<PoisonInfo> Poison = new();
        static OptionItem OptionKillCoolDown;
        static OptionItem OptionCoolDown;
        static OptionItem OptionDeadTime;
        static float KillCooldown;
        static float Cooldown;
        static float DeadTime;
        byte? tyokusetu; //直接入れたプレイヤー。
        private static void SetupOptionItem()
        {
            OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f)
                .SetValueFormat(OptionFormat.Seconds);
            OptionCoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 30f)
                .SetValueFormat(OptionFormat.Seconds);
            OptionDeadTime = FloatOptionItem.Create(RoleInfo, 5, Op.PoisonerDeadTime, new(0f, 999f, 0.5f), 80f)
                .SetValueFormat(OptionFormat.Seconds);
        }
        public float CalculateKillCooldown() => KillCooldown;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = Cooldown;
        }

        public static void OnFixedUpdateOthers(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (!GameStates.IsInTask || !Poison.Any(i => i.PlayerId == player.PlayerId)) return;

            foreach (var info in Poison.Where(i => i.PlayerId == player.PlayerId))
            {
                var infopc = PlayerCatch.GetPlayerById(info.PlayerId);
                if (infopc == null || !infopc.IsAlive())
                {
                    Poison.Remove(info);
                    return;
                }
                info.Progress += Time.fixedDeltaTime * info.Concentration;
                if (info.Progress >= DeadTime)
                {
                    PlayerControl kill = null;
                    //未割り当てエラー吐くので一旦仮で
                    PlayerControl pois = PlayerCatch.GetPlayerById(0);
                    foreach (var pc in PlayerCatch.AllPlayerControls.Where(p => p.Is(CustomRoles.Poisoner)))
                    {
                        pois = pc;
                        if (pc.GetRoleClass() is Poisoner p && p.tyokusetu == info.PlayerId) kill = pc;
                    }

                    //イビブレも毒ガスだし一旦これで(
                    if (CustomRoleManager.OnCheckMurder(kill is null ? pois : kill, player, player, player, Killpower: 2, deathReason: CustomDeathReason.Suffocation))
                    {
                        foreach (var pc in PlayerCatch.AllPlayerControls.Where(p => p.Is(CustomRoles.Poisoner)))
                        {
                            RPC.PlaySoundRPC(pc.PlayerId, Sounds.KillSound);
                        }
                    }
                }
            }
        }

        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = true;
            ResetCooldown = true;

            var target = Player.GetKillTarget(true);
            if (target == null || target.Is(CustomRoleTypes.Impostor))
            {
                ResetCooldown = false;
                return;
            }
            foreach (var data in Poison) if (data.PlayerId == target.PlayerId) return;

            if (tyokusetu == null) tyokusetu = target.PlayerId;
            Poison.Add(new PoisonInfo(target.PlayerId, 100));
        }
    }
}