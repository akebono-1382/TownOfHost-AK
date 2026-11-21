using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Crewmate;

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
            OptionSort: (2, 9),
            assignInfo: new RoleAssignInfo(CustomRoles.EvilChainer, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1)
            }
        );
    public EvilChainer(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        NowLimit = OptUse.GetInt() is 0 ? 99 : OptUse.GetInt();

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        CustomRoleManager.OnMurderPlayerOthers.Add(OnMurderPlayerOthers);
    }

    // Key: プレイヤーID Value:残りターン
    Dictionary<byte, int> Chain = new();
    static OptionItem KillCoolDown;
    static OptionItem CoolDown;
    static OptionItem OneChainMax;
    static OptionItem OptUse;
    static OptionItem Reset;
    static OptionItem ChainTurn;
    static OptionItem CanSeeChain;
    static OptionItem OtherKill;
    static OptionItem OnlyImp;
    int NowLimit;
    bool IUsePhantomButton.IsPhantomRole => (Chain.Count < OneChainMax.GetInt() || OneChainMax.GetInt() == 0) && NowLimit > 0;
    bool IUsePhantomButton.IsresetAfterKill => false;
    enum Op
    {
        EvilChainerChainMax,
        EvilChainerReset,
        EvilChainerChainTurn,
        EvilChainerCanseeChain,
        EvilChainerOtherKill,
        EvilChainerOnlyImp,
    }
    private static void SetupOptionItem()
    {
        KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f)
            .SetValueFormat(OptionFormat.Seconds);
        CoolDown = FloatOptionItem.Create(RoleInfo, 4, GeneralOption.Cooldown, OptionBaseCoolTime, 30f)
            .SetValueFormat(OptionFormat.Seconds);
        Reset = BooleanOptionItem.Create(RoleInfo, 7, Op.EvilChainerReset, false);
        OptUse = IntegerOptionItem.Create(RoleInfo, 5, GeneralOption.OptionCount, new(0, 14, 1), 5)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        OneChainMax = IntegerOptionItem.Create(RoleInfo, 6, Op.EvilChainerChainMax, new(0, 14, 1), 3)
            .SetValueFormat(OptionFormat.Players).SetZeroNotation(OptionZeroNotation.Infinity);
        ChainTurn = IntegerOptionItem.Create(RoleInfo, 8, Op.EvilChainerChainTurn, new(1, 99, 1), 3);
        CanSeeChain = BooleanOptionItem.Create(RoleInfo, 9, Op.EvilChainerCanseeChain, false);
        OtherKill = BooleanOptionItem.Create(RoleInfo, 10, Op.EvilChainerOtherKill, false);
        OnlyImp = BooleanOptionItem.Create(RoleInfo, 11, Op.EvilChainerOnlyImp, false, false, OtherKill);
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
    public static void OnMurderPlayerOthers(MurderInfo info)
    {
        //鎖の持ち主だけ通す
        var fir = PlayerCatch.AllPlayerControls
            .FirstOrDefault(p => p.GetRoleClass() is EvilChainer eccl && eccl.Chain.ContainsKey(info.AttemptTarget.PlayerId));
        byte id = fir?.PlayerId ?? 255;

        if (info.AttemptKiller.PlayerId != id && OtherKill.GetBool() is false) return;
        if (info.AttemptKiller.Is(CustomRoleTypes.Impostor) is false && OnlyImp.GetBool()) return;

        var target = info.AttemptTarget;
        foreach (var pc in PlayerCatch.AllPlayerControls.Where(p => p.Is(CustomRoles.EvilChainer)))
        {
            var ecclass = (EvilChainer)pc.GetRoleClass();
            if (ecclass is null) continue;

            if (ecclass.Chain.ContainsKey(target.PlayerId))
            {
                ecclass.Chain.Remove(target.PlayerId);

                var victims = ecclass.Chain.Keys.ToArray();
                ecclass.Chain.Clear();

                if (pc.IsAlive())
                {
                    foreach (var p in victims)
                    {
                        var a = PlayerCatch.GetPlayerById(p);
                        if (a == null || !a.IsAlive()) continue;
                        bool seerisimp =
                            a.Is(CustomRoleTypes.Impostor) &&
                            (a.GetRoleClass() is not Amnesiac || (a.GetRoleClass() is Amnesiac am && am.Realized))
                            && !a.Is(CustomRoles.OneWolf);
                        if (seerisimp) continue;

                        CustomRoleManager.OnCheckMurder(ecclass.Player, a, a, a, true, Killpower: 2, deathReason: CustomDeathReason.Revenge);
                    }
                }
                ecclass.Player.RpcResetAbilityCooldown();
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

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        bool seerisimp =
            seer.Is(CustomRoleTypes.Impostor) &&
            (seer.GetRoleClass() is not Amnesiac || (seer.GetRoleClass() is Amnesiac am && am.Realized))
            && !seer.Is(CustomRoles.OneWolf);

        if (!seerisimp) return "";

        //相方も見える場合は共有
        if (CanSeeChain.GetBool())
        {
            foreach (var pc in PlayerCatch.AllPlayerControls.Where(p => p.Is(CustomRoles.EvilChainer)))
            {
                if (pc.GetRoleClass() is not EvilChainer evilChainer) continue;
                if (evilChainer.Chain.ContainsKey(seen.PlayerId)) return Utils.ColorString(RoleInfo.RoleColor, "■");
            }
            return "";
        }

        if (seer.GetRoleClass() is EvilChainer ec)
        {
            if (ec.Is(seen) || !ec.Chain.ContainsKey(seen.PlayerId)) return "";
            return Utils.ColorString(RoleInfo.RoleColor, "■");
        }
        return "";
    }

    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        if (NowLimit <= 0 && Chain.Count >= OneChainMax.GetInt() && OneChainMax.GetInt() != 0)
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