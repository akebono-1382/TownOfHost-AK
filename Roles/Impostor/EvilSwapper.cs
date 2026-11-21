using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilSwapper : RoleBase, IImpostor, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilSwapper),
            player => new EvilSwapper(player),
            CustomRoles.EvilSwapper,
            () => RoleTypes.Impostor,
            CustomRoleTypes.Impostor,
            27400,
            SetupOptionItem,
            "Esw",
            OptionSort: (2, 10),
            from: From.TOR_GM_Edition
        );
    public EvilSwapper(PlayerControl player) : base( RoleInfo, player )
    {
        swap = OptionMaxswap.GetInt();
        Swap1 = byte.MaxValue;
        Swap2 = byte.MaxValue;
    }
    bool ISelfVoter.CanUseVoted() => swap > 0;
    static OptionItem OptionKillCoolDown;
    static OptionItem OptionMaxswap;
    private static OptionItem OptionCanButton;
    int swap;

    public byte Swap1;
    public byte Swap2;
    enum OptionName
    {
        GrimReaperCanButtom
    }
    private static void SetupOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
        OptionMaxswap = IntegerOptionItem.Create(RoleInfo, 11, GeneralOption.OptionCount, new(1, 99, 1), 1);
        OptionCanButton = BooleanOptionItem.Create(RoleInfo, 12, OptionName.GrimReaperCanButtom, false);
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reportreson)
    {
        if (reporter.Is(CustomRoles.EvilSwapper) && target == null && !OptionCanButton.GetBool())
        {
            reportreson = DontReportreson.Other;
            return true;
        }
        return false;
    }
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (Madmate.MadAvenger.Skill) return true;
        if (Swap1 == byte.MaxValue || Swap2 == byte.MaxValue)
        {
            if (CheckSelfVoteMode(Player, votedForId, out var status))
            {
                if (status is VoteStatus.Self)
                    Utils.SendMessage(string.Format(GetString("SkillMode"), GetString("Mode.ConnectSaver"), GetString("Vote.ConnectSaver")) + GetString("VoteSkillMode"), Player.PlayerId);
                if (status is VoteStatus.Skip)
                {
                    Swap1 = byte.MaxValue;
                    Swap2 = byte.MaxValue;
                    SetMode(Player, false);
                    Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
                }
                if (status is VoteStatus.Vote)
                    Tagech();
                return false;
            }
            else
            {
                if (voter.PlayerId != votedForId && Is(voter) && votedForId != SkipId && ((Swap1 != 255 && Swap2 == 255) || (Swap1 == 255 && Swap2 != 255)))
                {
                    Tagech();
                    return false;
                }
            }
        }
        return true;

        void Tagech()
        {
            if (votedForId == voter.PlayerId || swap <= 0) return;

            if (Swap1 == byte.MaxValue) Swap1 = votedForId;
            else if (Swap2 == byte.MaxValue) Swap2 = votedForId;

            if (Swap1 == Swap2) Swap2 = byte.MaxValue;

            var targetpc1 = PlayerCatch.GetPlayerById(Swap1);
            var targetpc2 = PlayerCatch.GetPlayerById(Swap2);

            if (!targetpc1.IsAlive() || targetpc1 == null) Swap1 = byte.MaxValue;
            if (!targetpc2.IsAlive() || targetpc2 == null) Swap2 = byte.MaxValue;

            if (Swap1 != byte.MaxValue || Swap2 != byte.MaxValue)
            {
                var Nowtargetcount = (Swap1 != byte.MaxValue && Swap2 != byte.MaxValue) ? GetString("TowPlayer") : GetString("OnePlayer");
                var lasttext = string.Format(GetString("Skill.Balancer"), Nowtargetcount, UtilsName.GetPlayerColor(PlayerCatch.GetPlayerById(votedForId), true));
                Utils.SendMessage(lasttext.ToString(), Player.PlayerId);
            }
            if (Swap1 != byte.MaxValue && Swap2 != byte.MaxValue)
            {
                Utils.SendMessage(GetString("Skill.ConnectSaver"), Player.PlayerId);
            }

            swap--;
        }
    }
    public float CalculateKillCooldown() => OptionKillCoolDown.GetFloat();
    public override string GetProgressText(bool comms = false, bool GameLog = false) =>
        Utils.ColorString(RoleInfo.RoleColor, $"({swap})");
}