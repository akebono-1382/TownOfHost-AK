using AmongUs.GameOptions;
using TownOfHostL.Roles.Core;
using TownOfHostL.Roles.Core.Interfaces;

namespace TownOfHostL.Roles.Impostor;

/// <summary>
/// サロゲートに選ばれた人がなる、キルのできるマッドメイト。配られず、<see cref="SurrogateSystem.CreateProxy"/> で役職が変わる。
/// キルのクールはサロゲートと共有し、キルの後はしばらくサロゲートの姿になる。サロゲートは斬れない。
/// </summary>
public sealed class MadProxy : RoleBase, IKiller
{
    // 役職の元: TownOfHost-hamo
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(MadProxy),
            player => new MadProxy(player),
            CustomRoles.MadProxy,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Madmate,
            (int)Options.offsetId.Hamo + 95100, // 並び順だけに使う (メニューには出さない)
            null,
            "Mpx",
            flags: RoleFlags.DontShowOption
        );

    public MadProxy(PlayerControl player) : base(RoleInfo, player, () => HasTask.False) { }

    public bool CanUseKillButton() => Player.IsAlive();
    public float CalculateKillCooldown() => Surrogate.MadProxyKillCooldown;
    public bool CanUseSabotageButton() => false;
    public bool CanUseImpostorVentButton() => true;

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (!info.CanKill) return;
        // 主のサロゲートは斬れない
        if (SurrogateSystem.TryGetOwner(Player.PlayerId, out var owner) && info.AttemptTarget.PlayerId == owner.PlayerId)
            info.DoKill = false;
    }

    public void OnMurderPlayerAsKiller(MurderInfo info)
    {
        SurrogateSystem.ShareKillCooldownAfterKill(Player);
        SurrogateSystem.TransformProxyAfterKill(Player);
    }
}
