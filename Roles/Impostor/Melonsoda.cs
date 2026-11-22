using AmongUs.GameOptions;
using HarmonyLib;
using UnityEngine;

using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class Melonsoda : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Melonsoda),
            player => new Melonsoda(player),
            CustomRoles.Melonsoda,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            28600,
            SetupOptionItem,
            "Med",
            "#00ff7f",
            (6, 17)
        );

    public Melonsoda(PlayerControl player)
        : base(RoleInfo, player)
    {
        Cooldown = OptionCooldown.GetFloat();
        Duration = OptionDuration.GetFloat();
    }
    static float? Durationtimer;
    public static byte Owner;
    [GameModuleInitializer]
    public static void Init()
    {
        Durationtimer = null;
        Owner = byte.MaxValue;
    }
    #region Option
    private static OptionItem OptionCooldown; static float Cooldown;
    private static OptionItem OptionDuration; static float Duration;
    private static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.Cooldown, OptionBaseCoolTime, 25f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDuration = FloatOptionItem.Create(RoleInfo, 11, GeneralOption.Duration, OptionBaseCoolTime, 10f)
            .SetValueFormat(OptionFormat.Seconds);
    }
    #endregion
    bool IUsePhantomButton.IsresetAfterKill => false;
    bool IUsePhantomButton.IsPhantomRole => Is(PlayerCatch.GetPlayerById(Owner)) || Owner is byte.MaxValue;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Is(PlayerCatch.GetPlayerById(Owner)) ? Duration : Owner != byte.MaxValue ? 255f : Cooldown;
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;

        if (Owner != byte.MaxValue)
        {
            ResetCooldown = false;
            return;
        }

        Owner = Player.PlayerId;
        Durationtimer = Duration;
        //仮
        Camouflager.NowUse = true;
        PlayerCatch.AllAlivePlayerControls.DoIf(p => p.Is(CustomRoles.Camouflager), pc => pc.SyncSettings());

        foreach (var target in PlayerCatch.AllAlivePlayerControls)
        {
            var sender = CustomRpcSender.Create(name: $"Camouflage.RpcSetSkin({target.Data.GetLogPlayerName()})");
            byte color = (byte)ModColors.PlayerColor.Lime;

            target.SetColor(color);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetColor)
                .Write(target.Data.NetId)
                .Write(color)
                .EndRpc();

            target.SetHat("hat_mira_milk", color);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetHatStr)
                .Write("hat_mira_milk")
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetHatStr))
                .EndRpc();

            target.SetSkin("skin_BowKidskin", color);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetSkinStr)
                .Write("skin_BowKidskin")
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetSkinStr))
                .EndRpc();

            target.SetVisor("visor_lollipopCrew", color);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetVisorStr)
                .Write("visor_lollipopCrew")
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetVisorStr))
                .EndRpc();

            target.SetPet("pet_Strawb", color);
            sender.AutoStartRpc(target.NetId, (byte)RpcCalls.SetPetStr)
                .Write("pet_Strawb")
                .Write(target.GetNextRpcSequenceId(RpcCalls.SetPetStr))
                .EndRpc();
            sender.SendMessage();
        }
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (!Is(PlayerCatch.GetPlayerById(Owner))) return;
        if (Durationtimer is null) return;
        Durationtimer -= Time.fixedDeltaTime;

        if (Durationtimer <= 0f)
        {
            Owner = byte.MaxValue;
            Durationtimer = null;
            Camouflager.NowUse = false;
            PlayerCatch.AllAlivePlayerControls.DoIf(p => p.Is(CustomRoles.Camouflager), pc => pc.SyncSettings());
            PlayerCatch.AllAlivePlayerControls.DoIf(p => p.Is(CustomRoles.Camouflager), pc => pc.RpcResetAbilityCooldown());
            if (Camouflager.MyUse is false)
            {
                PlayerCatch.AllAlivePlayerControls.Do(pc => Camouflage.RpcSetSkin(pc, true, true));
            }
        }
        //使用中かつカモフラがfalseに戻したとき
        else if (Camouflager.NowUse is false)
        {
            Camouflager.NowUse = true;
        }
    }
}
