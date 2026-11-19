using System.Linq;
using AmongUs.GameOptions;

using TownOfHostL.Roles.Core;
using TownOfHostL.Roles.Core.Interfaces;
using TownOfHostL.Roles.Impostor;

namespace TownOfHostL.Roles.Crewmate;

public sealed class Merlin : RoleBase, IRoomTasker
{
    // 役職の元: ExtremeRoles (TownOfHost-K・TownOfHost-hamo 経由)
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Merlin),
            player => new Merlin(player),
            CustomRoles.Merlin,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            (int)Options.offsetId.Hamo + 76800,
            null,
            "mer",
            "#8cc2ff",
            flags: RoleFlags.DontShowOption
        );
    public Merlin(PlayerControl player)
    : base(
        RoleInfo,
        player,
        () => Assassin.OptionMerlinHasTask.GetBool() ? HasTask.True : HasTask.False
    )
    {
        completeroom = 0;

        worktask = Assassin.OptionMerlinHasTask.GetBool() ? Assassin.OptionMerlinWorkTask.GetInt() : 0;
        CanSeeNeutral = Assassin.OptionMerlinCanSeeNeutral?.GetBool() ?? false;
        OnlyNeutralKiller = Assassin.OptionMerlinOnlyNeutralKiller?.GetBool() ?? false;
        IsGlayColor = Assassin.OptionMerlinCantSeeNeutralColor?.GetBool() ?? false;
        CanSeeMadmate = Assassin.OptionMerlinCanSeeMadmate?.GetBool() ?? false;
    }
    public static int worktask;
    public int completeroom;

    static bool CanSeeNeutral;
    static bool IsGlayColor;
    static bool CanSeeMadmate;
    static bool OnlyNeutralKiller;

    public override void Add()
    {
        foreach (var impostor in Main.AllPlayerControls.Where(player => player.Is(CustomRoleTypes.Impostor) || player.GetCustomRole() is CustomRoles.Egoist))
        {
            NameColorManager.Add(Player.PlayerId, impostor.PlayerId, "#ff1919");
        }
        if (CanSeeNeutral)
        {
            foreach (var neutral in Main.AllPlayerControls.Where(player => player.Is(CustomRoleTypes.Neutral) || (player.Is(CustomRoleTypes.Madmate) && CanSeeMadmate)))
            {
                if (!OnlyNeutralKiller || neutral.IsNeutralKiller() || neutral.Is(CustomRoles.GrimReaper))
                    NameColorManager.Add(Player.PlayerId, neutral.PlayerId, IsGlayColor ? "#555555" : neutral.GetRoleColorCode());
            }
        }
        Assassin.MarlinIds.Add(Player.PlayerId);
    }
    int? IRoomTasker.GetMaxTaskCount() => worktask;
    bool IRoomTasker.IsAssignRoomTask() => completeroom != worktask;
    void CheckFin()
    {
        if (MyTaskState.CompletedTasksCount < MyTaskState.AllTasksCount) return;
        Utils.AddGameLog("Task", string.Format(Translator.GetString("Taskfin"), Player.ColoredName()));
    }
    void IRoomTasker.OnComplete(int completeroom)
    {
        this.completeroom = completeroom;
        CheckFin();
        MyTaskState.Update(Player);
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting || seer != seen) return "";
        return (seer.GetRoleClass() as IRoomTasker)?.GetLowerText(seer, RoleInfo.RoleColorCode) ?? "";
    }
}
