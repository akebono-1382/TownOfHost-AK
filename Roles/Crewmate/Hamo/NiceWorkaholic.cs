using AmongUs.GameOptions;
using System.Linq;
using TownOfHostL.Roles.Core;

namespace TownOfHostL.Roles.Crewmate;

public sealed class NiceWorkaholic : RoleBase
{
    // 役職の元: TownOfHost_Pko (TownOfHost-hamo 経由)
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(NiceWorkaholic),
            player => new NiceWorkaholic(player),
            CustomRoles.NiceWorkaholic,
            () => OptionCanVent.GetBool() ? RoleTypes.Engineer : RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            (int)Options.offsetId.Hamo + 33700,
            SetupOptionItem,
            "nwh",
            "#008b8b",
            introSound: () => ShipStatus.Instance?.CommonTasks
                .FirstOrDefault(t => t.TaskType == TaskTypes.FixWiring)
                ?.MinigamePrefab.OpenSound
        );

    public NiceWorkaholic(PlayerControl player)
    : base(RoleInfo, player, () => HasTask.ForRecompute)
    {
        ventCooldown = OptionVentCooldown.GetFloat();
        CanWinAtDeath = OptionWinAtDeath.GetBool();
    }

    private static OptionItem OptionCanVent;
    private static OptionItem OptionVentCooldown;
    private static OptionItem OptionWinAtDeath;

    enum OptionName { NiceWorkaholicCanWinAtDeath }

    private static bool CanWinAtDeath;
    private static float ventCooldown;

    private static void SetupOptionItem()
    {
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, GeneralOption.CanVent, false, false);
        OptionVentCooldown = FloatOptionItem.Create(RoleInfo, GeneralOption.Cooldown,
            new(0f, 180f, 2.5f), 0f, false, OptionCanVent)
            .SetValueFormat(OptionFormat.Seconds);
        OptionWinAtDeath = BooleanOptionItem.Create(RoleInfo, OptionName.NiceWorkaholicCanWinAtDeath, false, false);
        Options.OverrideTasksData.Create(RoleInfo);
    }

    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.EngineerCooldown = ventCooldown;
        AURoleOptions.EngineerInVentMaxTime = 0;
    }

    public override bool OnCompleteTask()
    {
        if (!AmongUsClient.Instance.AmHost) return true;
        if (!IsTaskFinished) return true;
        if (!CanWinAtDeath && !Player.IsAlive()) return true;

        // 終わりを送り始めた後 (ほかの勝ちが先に決まった) は勝者を上書きしない
        if (GameEndChecker.EndingStarted) return true;
        CustomWinnerHolder.ResetAndSetWinner(CustomWinner.Crewmate);
        foreach (var pc in Main.AllPlayerControls)
        {
            if (pc.Is(CustomRoleTypes.Crewmate))
                CustomWinnerHolder.WinnerIds.Add(pc.PlayerId);
        }

        Utils.AddGameLog("NiceWorkaholic",
            $"{Player.ColoredName()} が全タスク完了！クルー陣営勝利！");

        GameManager.Instance.enabled = false;
        // 試合の終わりの流れ (結果の画面に向けた幽霊の役職の配りと蘇生) を通してから終わりを送る
        GameEndChecker.StartEndGame(GameOverReason.CrewmatesByTask);

        return true;
    }

    public override void OverrideProgressTextAsSeen(PlayerControl seer, ref bool enabled, ref string text)
    {
        seer ??= Player;
        if (!Player.IsAlive() && !CanWinAtDeath)
            text = "";
        if (Is(seer) || seer.Is(CustomRoles.GM)) return;
        text = $"(?/{MyTaskState.AllTasksCount})";
    }
}