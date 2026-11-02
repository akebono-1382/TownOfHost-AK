using AmongUs.GameOptions;
using TownOfHost.Roles.AddOns.Common;
using TownOfHost.Roles.AddOns.Impostor;
using TownOfHost.Roles.AddOns.Neutral;
using TownOfHost.Roles.Core;

namespace TownOfHost.Roles.Crewmate;

public sealed class Trapper : RoleBase
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Trapper),
            player => new Trapper(player),
            CustomRoles.Trapper,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            21300,
            SetupOptionItem,
            "tra",
            "#5a8fd0",
            (5, 5),
            from: From.TownOfHost
        );
    public Trapper(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        BlockMoveTime = OptionBlockMoveTime.GetFloat();
        Awakened = !Awakening.GetBool() || AwakeningTask.GetInt() < 1;
        IsTraping = false;
    }
    static OptionItem Awakening;
    static OptionItem AwakeningTask;
    bool Awakened;
    private static OptionItem OptionBlockMoveTime;
    enum OptionName
    {
        TrapperBlockMoveTime
    }

    private static float BlockMoveTime;
    bool IsTraping;

    private static void SetupOptionItem()
    {
        OptionBlockMoveTime = FloatOptionItem.Create(RoleInfo, 10, OptionName.TrapperBlockMoveTime, new(1f, 180f, 1f), 5f, false)
            .SetValueFormat(OptionFormat.Seconds);
        Awakening = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.TaskAwakening, false, false);
        AwakeningTask = IntegerOptionItem.Create(RoleInfo, 12, GeneralOption.AwakeningTaskcount, new(1, 255, 1), 5, false, Awakening);
    }

    public static void BlockMove(PlayerControl killer, PlayerControl victim)
    {
        var istrap = victim.GetRoleClass() is Trapper t && t.Awakened;
        var lastit = victim.Is(CustomRoles.LastImpostor) && LastImpostor.GiveTrapping.GetBool();
        var lastnt = victim.Is(CustomRoles.LastNeutral) && LastNeutral.GiveTrapping.GetBool();

        if (!istrap && !victim.Is(CustomRoles.Trapping) && !lastit && !lastnt) return;

        var blocktime = 0f;

        if (istrap) blocktime += BlockMoveTime;
        if (victim.Is(CustomRoles.Trapping)) blocktime += Trapping.BlockMoveTime.GetFloat();
        if (lastit) blocktime += LastImpostor.BlockMoveTime.GetFloat();
        if (lastnt) blocktime += LastNeutral.BlockMoveTime.GetFloat();

        var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
        Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
        ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
        killer.MarkDirtySettings();

        if (victim.GetRoleClass() is Trapper trap)
        {
            trap.IsTraping = true;
            Achievements.RpcCompleteAchievement(trap.Player.PlayerId, 0, achievements[0]);
        }
        _ = new LateTask(() =>
        {
            if (victim.GetRoleClass() is Trapper trap)
            {
                trap.IsTraping = false;
                Achievements.RpcCompleteAchievement(trap.Player.PlayerId, 0, achievements[0]);
            }
            Main.AllPlayerSpeed[killer.PlayerId] = tmpSpeed;
            ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
            killer.MarkDirtySettings();
            RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
        }, blocktime, "Trapper BlockMove");
    }

    public override CustomRoles Misidentify() => Awakened ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    public override bool OnCompleteTask(uint taskid)
    {
        if (MyTaskState.HasCompletedEnoughCountOfTasks(AwakeningTask.GetInt()))
        {
            if (Awakened == false)
                if (!Utils.RoleSendList.Contains(Player.PlayerId))
                    Utils.RoleSendList.Add(Player.PlayerId);
            Awakened = true;
        }
        return true;
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        if (IsTraping && (target?.PlayerId ?? byte.MaxValue) == Player.PlayerId)
            Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
    }
    public static System.Collections.Generic.Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 1, 0, 0);
        var l1 = new Achievement(RoleInfo, 1, 1, 0, 1);
        achievements.Add(0, n1);
        achievements.Add(1, l1);
    }
}