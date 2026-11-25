using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Epic.OnlineServices.Presence;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Warlock : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Warlock),
            player => new Warlock(player),
            CustomRoles.Warlock,
            () => OptionOneClick.GetBool() ? RoleTypes.Phantom : RoleTypes.Shapeshifter,
            CustomRoleTypes.Impostor,
            5000,
            SetUpOptionItem,
            "wa",
            OptionSort: (4, 4),
            from: From.TheOtherRoles
        );
    public Warlock(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        KillCooldown = OptionKillCoolDown.GetFloat();
        CantMoveTime = OptionCantMoveTime.GetFloat();
        OneClick = OptionOneClick.GetBool();
        Cooldown = OptionCoolDown.GetFloat();
        Max = OptionMax.GetInt();
    }
    enum Op
    {
        WarlockCantMoveTime,
        WarlockOneClick,
    }
    public float CalculateKillCooldown() => KillCooldown;
    static OptionItem OptionKillCoolDown; static float KillCooldown;
    static OptionItem OptionCantMoveTime; static float CantMoveTime;
    static OptionItem OptionMax; int Max;
    static OptionItem OptionOneClick; static bool OneClick;
    static OptionItem OptionCoolDown; static float Cooldown;
    bool IUsePhantomButton.IsPhantomRole => OneClick;
    bool IUsePhantomButton.UseOneclickButton => OneClick;
    static void SetUpOptionItem()
    {
        OptionKillCoolDown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 30f)
            .SetValueFormat(OptionFormat.Seconds);
        /*OptionMax = IntegerOptionItem.Create(RoleInfo, 14, GeneralOption.OptionCount, new(1, 99, 1), 4)
            .SetValueFormat(OptionFormat.Times);
        OptionCantMoveTime = FloatOptionItem.Create(RoleInfo, 11, Op.WarlockCantMoveTime, OptionBaseCoolTime, 5f)
            .SetValueFormat(OptionFormat.Seconds);*/
        OptionOneClick = BooleanOptionItem.Create(RoleInfo, 12, Op.WarlockOneClick, true);
        OptionCoolDown = FloatOptionItem.Create(RoleInfo, 13, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, parent: OptionOneClick)
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void OnDestroy()
    {
        CursedPlayer = null;
    }

    PlayerControl CursedPlayer;
    bool Shapeshifting;
    public override void Add()
    {
        CursedPlayer = null;
        Shapeshifting = false;
    }
    public bool OverrideKillButtonText(out string text)
    {
        if (OneClick)
        {
            text = default;
            return false;
        }
        if (!Shapeshifting)
        {
            text = GetString("WarlockCurseButtonText");
            return true;
        }
        else
        {
            text = default;
            return false;
        }
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.ShapeshifterCooldown = 0.1f;
        AURoleOptions.PhantomCooldown = CursedPlayer != null ? 0.1f : Cooldown;
    }
    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (info.IsSuicide || CursedPlayer != null) return;

        var (killer, target) = info.AttemptTuple;
        if (!Shapeshifting)
        {
            if (CursedPlayer == null)
            {
                info.DoKill = false;
                CursedPlayer = target;
                //_ = new LateTask(() => killer.RpcResetAbilityCooldown(), 0.5f, "WarlockAbility", true);
            }
        }
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;
        var target = Player.GetKillTarget(true);
        if (target == null)
        {
            ResetCooldown = false;
            return;
        }

        if (CursedPlayer == null)
        {
            ResetCooldown = false;
            CursedPlayer = target;
        }
        else if (CursedPlayer != null && CursedPlayer.IsAlive())
        {
            Vector2 cpPos = CursedPlayer.transform.position;
            Dictionary<PlayerControl, float> candidateList = new();
            float distance;
            foreach (PlayerControl candidatePC in PlayerCatch.AllAlivePlayerControls)
            {
                if (candidatePC != CursedPlayer && !candidatePC.Is(CustomRoles.King))
                {
                    distance = Vector2.Distance(cpPos, candidatePC.transform.position);
                    candidateList.Add(candidatePC, distance);
                    Logger.Info($"{candidatePC?.Data?.GetLogPlayerName()}の位置{distance}", "Warlock");
                }
            }
            var nearest = candidateList.OrderBy(c => c.Value).FirstOrDefault();
            var killTarget = nearest.Key;
            if (CustomRoleManager.OnCheckMurder(Player, killTarget, CursedPlayer, killTarget, true, false, 2))
            {
                Logger.Info($"{killTarget.GetNameWithRole().RemoveHtmlTags()}was killed", "Warlock");
            }
            //Player.SetKillCooldown();
            CursedPlayer = null;
            Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
            if (killTarget.IsTeammate(Player))
            {
                Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            }
            return;
        }

        //アンフォーチュナーのやつ流用！(
        float TurnTimer = 0;
        IUsePhantomButton.IPPlayerKillCooldown.TryGetValue(Player.PlayerId, out TurnTimer);
        Main.AllPlayerKillCooldown.TryGetValue(Player.PlayerId, out var killcool);
        if (MeetingStates.FirstMeeting && !Options.FixFirstKillCooldown.GetBool() && killcool > 10 &&
        (PlayerState.GetByPlayerId(Player.PlayerId)?.Is10secKillButton == true))
            killcool = 10;
        float cooldown = killcool - TurnTimer;
        if (cooldown <= 1) cooldown = 0.005f;

        Player.SetKillCooldown(cooldown);
    }
    public override bool CheckShapeshift(PlayerControl target, ref bool shouldAnimate)
    {
        //さすがにアニメーションは弱い。
        shouldAnimate = false;
        return true;
    }
    public override void OnShapeshift(PlayerControl target)
    {
        Shapeshifting = !Is(target);

        if (!AmongUsClient.Instance.AmHost || OneClick) return;

        if (Shapeshifting)
        {
            ///変身時
            if (CursedPlayer != null && CursedPlayer.IsAlive())
            {
                //呪っていて対象がまだ生きていたら
                Vector2 cpPos = CursedPlayer.transform.position;
                Dictionary<PlayerControl, float> candidateList = new();
                float distance;
                foreach (PlayerControl candidatePC in PlayerCatch.AllAlivePlayerControls)
                {
                    if (candidatePC != CursedPlayer && !candidatePC.Is(CustomRoles.King))
                    {
                        distance = Vector2.Distance(cpPos, candidatePC.transform.position);
                        candidateList.Add(candidatePC, distance);
                        Logger.Info($"{candidatePC?.Data?.GetLogPlayerName()}の位置{distance}", "Warlock");
                    }
                }
                var nearest = candidateList.OrderBy(c => c.Value).FirstOrDefault();
                var killTarget = nearest.Key;
                if (CustomRoleManager.OnCheckMurder(Player, killTarget, CursedPlayer, killTarget, true, false, 2))
                {
                    Logger.Info($"{killTarget.GetNameWithRole().RemoveHtmlTags()}was killed", "Warlock");
                }
                Player.SetKillCooldown();
                CursedPlayer = null;
                Achievements.RpcCompleteAchievement(Player.PlayerId, 1, achievements[0]);
                if (killTarget.IsTeammate(Player))
                    Achievements.RpcCompleteAchievement(Player.PlayerId, 0, achievements[1]);
            }
        }
    }
    public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target)
    {
        CursedPlayer = null;
        Shapeshifting = false;
    }
    public override bool OverrideAbilityButton(out string text)
    {
        if (CursedPlayer != null && OneClick)
        {
            text = "Warlock_Ability_Cursed";
            return true;
        }
        if (CursedPlayer == null && OneClick)
        {
            text = "Warlock_Ability_Curse";
            return true;
        }
        text = "Warlock_Ability";
        return true;
    }
    public static Dictionary<int, Achievement> achievements = new();
    [Attributes.PluginModuleInitializer]
    public static void Load()
    {
        var n1 = new Achievement(RoleInfo, 0, 5, 0, 0);
        var sp1 = new Achievement(RoleInfo, 1, 1, 2, 2, true);
        achievements.Add(0, n1);
        achievements.Add(1, sp1);
    }
}