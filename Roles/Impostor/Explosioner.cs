using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Explosioner : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Explosioner),
            player => new Explosioner(player),
            CustomRoles.Explosioner,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            25900,
            SetupOptionItem,
            "exp",
            OptionSort: (3, 13)
        );
    public Explosioner(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        a = false;
        Explosioned = false;
        NowExplosionMode = false;
        KillCount = OpKillCount.GetInt();
    }
    static OptionItem KillCoolDown;
    static OptionItem CoolDown;
    static OptionItem Range;
    static OptionItem OpKillCount;
    static OptionItem OpDead;
    static OptionItem KnowRole;
    static OptionItem KnowTask;
    bool NowExplosionMode;
    int KillCount;
    bool Explosioned;
    bool a;
    enum Op
    {
        ExplosionerKillCount,
        ExplosionerDead,
        blastrange,
        ExPlosionerKnowRole,
        ExplosionerKnowTask
    }

    private static void SetupOptionItem()
    {
        KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 40f, false)
            .SetValueFormat(OptionFormat.Seconds);
        Range = FloatOptionItem.Create(RoleInfo, 5, Op.blastrange, new(0.25f, 10f, 0.25f), 3f, false)
            .SetValueFormat(OptionFormat.Multiplier);
        OpKillCount = IntegerOptionItem.Create(RoleInfo, 6, Op.ExplosionerKillCount, new(1, 14, 1), 2, false);
        OpDead = BooleanOptionItem.Create(RoleInfo, 7, Op.ExplosionerDead, false, false);
        KnowRole = BooleanOptionItem.Create(RoleInfo, 8, Op.ExPlosionerKnowRole, false, false);
        KnowTask = BooleanOptionItem.Create(RoleInfo, 9, Op.ExplosionerKnowTask, false, false, KnowRole)
            .SetEnabled(() => KnowRole.GetBool() && !OpDead.GetBool());
    }
    public float CalculateKillCooldown() => KillCoolDown.GetFloat();
    bool IUsePhantomButton.IsPhantomRole => !a;
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = CoolDown.GetFloat();
    }

    void IKiller.OnMurderPlayerAsKiller(MurderInfo info)
    {
        if (NowExplosionMode && !info.IsFakeSuicide && !info.IsSuicide)
        {
            KillCount--;
            if (KillCount <= 0) Explosion();
        }
    }

    void Explosion()
    {
        if (!Player.IsAlive()) return;

        Explosioned = true;

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            var dis = Vector2.Distance(pc.GetTruePosition(), Player.GetTruePosition());

            if (dis > Range.GetFloat()) continue;

            CustomRoleManager.OnCheckMurder(Player, pc, pc, pc, true, deathReason: CustomDeathReason.Bombed);
        }

        if (OpDead.GetBool())
        {
            CustomRoleManager.OnCheckMurder(Player, Player, Player, Player, true, Killpower: 10, deathReason: CustomDeathReason.Bombed);
        }

        Utils.AllPlayerKillFlash();
    }

    public override void OverrideDisplayRoleNameAsSeen(PlayerControl seer, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!Explosioned || !KnowRole.GetBool()) return;
        if (!KnowTask.GetBool() && !GameStates.IsMeeting && !GameStates.CalledMeeting) return;

        enabled = true;
        addon = false;
        roleText = UtilsRoleText.GetRoleName(CustomRoles.Explosioner);
        roleColor = UtilsRoleText.GetRoleColor(CustomRoles.Explosioner);
    }

    public override void AfterMeetingTasks()
    {
        NowExplosionMode = false;
    }

    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = null;

        NowExplosionMode = true;
        a = true;
    }
}