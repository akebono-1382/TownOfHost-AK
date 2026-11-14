using System.Collections.Generic;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;

namespace TownOfHost.Roles.Impostor;

public sealed class EvilChainer : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(EvilChainer),
            player => new EvilChainer(player),
            CustomRoles.EvilChainer,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            27200,
            SetupOptionItem,
            "Ecr",
            OptionSort: (2, 9)
        );
    public EvilChainer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        NowLimit = OptUse.GetInt() is 0 ? 99 : OptUse.GetInt();
    }
    enum Op
    {
        EvilChainerChainMax,
    }
    // Key: プレイヤーID Value:残りターン
    Dictionary<byte, int> Chain = new();
    static OptionItem KillCoolDown;
    static OptionItem CoolDown;
    static OptionItem OneChainMax;
    static OptionItem OptUse;
    int NowLimit;
    bool IUsePhantomButton.IsPhantomRole =>
        (Chain.Count < OneChainMax.GetInt() || OneChainMax.GetInt() == 0) &&
        NowLimit > 0
        ;
    private static void SetupOptionItem()
    {
        KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptUse = IntegerOptionItem.Create(RoleInfo, 5, GeneralOption.OptionCount, new(0, 14, 1), 5)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OneChainMax = IntegerOptionItem.Create(RoleInfo, 6, Op.EvilChainerChainMax, new(0, 14, 1), 3)
            .SetValueFormat(OptionFormat.Players).SetZeroNotation(OptionZeroNotation.Infinity);
    }
    public float CalculateKillCooldown() => KillCoolDown.GetFloat();
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = CoolDown.GetFloat();
    }
    public override void AfterMeetingTasks()
    {
        foreach (var a in Chain.Keys)
        {
            Chain[a]--;
        }
    }
    void IKiller.OnMurderPlayerAsKiller(MurderInfo info)
    {
        var target = info.AttemptTarget;
        if (Chain.ContainsKey(target.PlayerId))
        {
            Chain.Remove(target.PlayerId);
            foreach (var p in Chain.Keys)
            {
                var pc = PlayerCatch.GetPlayerById(p);
                if (pc == null || !pc.IsAlive() || pc.Is(CustomRoleTypes.Impostor))
                {
                    Chain.Remove(p);
                    continue;
                }

                CustomRoleManager.OnCheckMurder(Player, pc, pc, pc, true, Killpower: 2, deathReason: CustomDeathReason.Revenge);
            }
        }
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = false;
        ResetCooldown = false;

        var target = Player.GetKillTarget(true);
        if (target is null
            || target.Is(CustomRoleTypes.Impostor)
            || NowLimit <= 0
            || (Chain.Count >= OneChainMax.GetInt() && OneChainMax.GetInt() != 0)) return;

        ResetCooldown = true;

        if (NowLimit < 99)
        {
            NowLimit--;
        }

        Chain.Add(target.PlayerId, 3);
        Player.SetKillCooldown();
    }

    public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        if (Is(seen) || !Chain.ContainsKey(seen.PlayerId)) return "";
        return Utils.ColorString(RoleInfo.RoleColor, "■");
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (NowLimit <= 0 && (Chain.Count >= OneChainMax.GetInt() && OneChainMax.GetInt() != 0))
        {
            return "";
        }
        string max = $"({NowLimit})";
        string now = $"[{OneChainMax.GetInt() - Chain.Count}]";
        string a = NowLimit < 99 ? max : "";
        string b = OneChainMax.GetInt() != 0 ? now : "";
        a = Utils.ColorString(Palette.ImpostorRed, a);
        b = Utils.ColorString(Palette.ImpostorRed, b);

        if (NowLimit <= 0 || (Chain.Count >= OneChainMax.GetInt() && OneChainMax.GetInt() != 0))
        {
            a = Utils.ColorString(Palette.DisabledGrey, a);
            b = Utils.ColorString(Palette.DisabledGrey, b);
        }

        return $"{a}{b}";
    }

    /*public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        if (isForMeeting) return "";
        bool a = Chain.Count >= OneChainMax.GetInt() && OneChainMax.GetInt() != 0;
        if (a is false || NowLimit <= 0) return "";

        return Utils.ColorString(
            Palette.ImpostorRed,
            GetString("BloodlustAbilityExposed")
            );
    }*/
}