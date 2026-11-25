using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

using AmongUs.GameOptions;

namespace TownOfHost.Roles.Impostor;

public sealed class Hatekiller : RoleBase , IImpostor
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Hatekiller),
            player => new Hatekiller(player),
            CustomRoles.Hatekiller,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            29100,
            SetupOptionItem,
            "htk",
            OptionSort: (7, 9),
            from: From.TownOfHost_Pko
        );

    public Hatekiller(PlayerControl player)
        : base(RoleInfo, player)
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        DecreaseKillCool = OptionDecreaseKillCool.GetFloat();
        MinKillCooldown = OptionMinKillCooldown.GetFloat();

        ImpostorVote = OptionImpostorVote.GetBool();
        SelfVote = OptionSelfVote.GetBool();
    }

    #region オプション
    private static OptionItem OptionKillCooldown; float KillCooldown;
    private static OptionItem OptionDecreaseKillCool; static float DecreaseKillCool;
    private static OptionItem OptionMinKillCooldown; static float MinKillCooldown;
    private static OptionItem OptionImpostorVote; static bool ImpostorVote;
    private static OptionItem OptionSelfVote; static bool SelfVote;
    private static void SetupOptionItem()
    {
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, "HatekillerFirstKillCooldown", OptionBaseCoolTime, 35f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionDecreaseKillCool = FloatOptionItem.Create(RoleInfo, 11, "HatekillerDecreaseKillCool", OptionBaseCoolTime, 5f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionMinKillCooldown = FloatOptionItem.Create(RoleInfo, 12, "HatekillerMinKillCooldown", OptionBaseCoolTime, 15f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionImpostorVote = BooleanOptionItem.Create(RoleInfo, 13, "HatekillerImpostorVote", true, false);
        OptionSelfVote = BooleanOptionItem.Create(RoleInfo, 14, "HatekillerSelfVote", true, false);
    }
    #endregion
    public float CalculateKillCooldown() => KillCooldown;
    public static void CheckVote(PlayerControl voter, PlayerControl voteFor, int numVotes)
    {
        bool nullflag = false;
        if (voter is null)
        {
            nullflag = true;
            Logger.Error("voterがnull!", "HateKiller");
        }
        if (voteFor is null)
        {
            nullflag = true;
            Logger.Error("voteForがnull!", "HateKiller");
        }
        if (nullflag)
        {
            Logger.Error("voterまたはvoteForがnullのためreturn", "HateKiller");
            return;
        }
        if (voteFor.GetRoleClass() is not Hatekiller hatekiller) return;
        if (hatekiller.KillCooldown <= MinKillCooldown)
        {
            Logger.Warn($"今のキルクール({hatekiller.KillCooldown})が最低値{MinKillCooldown}以下です", "HateKiller");
            hatekiller.KillCooldown = MinKillCooldown;
            return;
        }
        if (voter.Is(CustomRoleTypes.Impostor) && ImpostorVote is false) return;
        if (voter.PlayerId == voteFor.PlayerId && SelfVote is false) return;
        var decraase = DecreaseKillCool * numVotes;
        hatekiller.KillCooldown -= decraase;
        if (hatekiller.KillCooldown < MinKillCooldown) hatekiller.KillCooldown = MinKillCooldown;
        Logger.Info($"{hatekiller.Player.GetNameWithRole().RemoveHtmlTags()}のキルクールを{hatekiller.KillCooldown}秒に短縮", "HateKiller");
    }
}
