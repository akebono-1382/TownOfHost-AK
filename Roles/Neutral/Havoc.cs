using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using Il2CppMono.Security.Cryptography;
using MS.Internal.Xml.XPath;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Neutral;

public sealed class Havoc : RoleBase, ILNKiller, ISchrodingerCatOwner, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Havoc),
            player => new Havoc(player),
            CustomRoles.Havoc,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Neutral,
            25500,
            SetupOptionItem,
            "hvc",
            "#b54200",
            (2, 0),
            true,
            from: From.NebulaontheShip,
            countType: CountTypes.Havoc,
            Desc:
            () =>
                string.Format(GetString("HavocDesc"),
                    OptDuration.GetFloat(),
                    OptToboeDis.GetFloat(),
                    OptArrowCount.GetInt(),
                    OptEatKillCool.GetFloat() > 0 ? string.Format(GetString("HavocDescKillCool"), OptEatKillCool.GetFloat()) : "",
                    OptionCanVent.GetBool() ? string.Format(GetString("HavocDescVent"), OptionVentCount.GetInt()) : ""
                    )
        );
    public Havoc(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        Havocs.Add(player.PlayerId);
        KillCooldown = OptionKillCooldown.GetFloat();
        CanVent = OptionCanVent.GetBool();
        Toboedis = OptToboeDis.GetFloat();
        EatCount = 0;
        Eatmode = true;
        VentCount = OptionVentCount.GetInt();
        Cooltimer = OptCooldown.GetFloat();
        EatCooltimer = OptEatCooldown.GetFloat();
        Duration = OptDuration.GetFloat();
        EatKillCool = OptEatKillCool.GetFloat();
        MaxEat = OptMaxEat.GetInt();
        ArrowCount = OptArrowCount.GetInt();
        Arrow = false;
        Nowuse = false;

        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        CustomRoleManager.LowerOthers.Add(GetLowerTextOthers);
    }

    [GameModuleInitializer]
    public static void Init()
    {
        toboe = false;
        EatedPlayers.Clear();
        ToboePlayers.Clear();
        Havocs.Clear();
    }
    static HashSet<byte> EatedPlayers = new();
    static OptionItem OptionKillCooldown; float KillCooldown;
    static OptionItem OptionCanVent; static bool CanVent;
    static OptionItem OptionVentCount; static int VentCount;
    static OptionItem OptCooldown;
    static OptionItem OptEatCooldown;
    static OptionItem OptDuration; static float Duration;
    static OptionItem OptEatKillCool; static float EatKillCool;
    static OptionItem OptMaxEat; int MaxEat;
    static OptionItem OptToboeDis; static float Toboedis;
    static OptionItem OptImpVision;
    static OptionItem OptArrowCount; static int ArrowCount;
    bool Arrow;
    int EatCount;   
    bool Eatmode;
    float EatCooltimer;
    float Cooltimer;
    static bool toboe;
    bool Nowuse;
    /// <summary>
    /// 遠吠えが聞こえるプレイヤー
    /// 入っていると!!が名前の横に表示
    /// </summary>
    public static Dictionary<byte, float> ToboePlayers = new();
    public static HashSet<byte> Havocs = new();
    enum OptionName
    {
        HavocCanVentCount,
        HavocCoolDown,
        HavoceatCooldown,
        HavocDuration,
        HavocEatKillCool,
        HavocCanEatCount,
        HavocToboeDis,
        HavocArrowCount

    }
    private static void SetupOptionItem()
    {
        SoloWinOption.Create(RoleInfo, 9, defo: 1);
        OptionKillCooldown = FloatOptionItem.Create(RoleInfo, 10, GeneralOption.KillCooldown, OptionBaseCoolTime, 40f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptionCanVent = BooleanOptionItem.Create(RoleInfo, 11, GeneralOption.CanVent, true, false);
        OptImpVision = BooleanOptionItem.Create(RoleInfo, 19, GeneralOption.ImpostorVision, true, false);

        OptEatKillCool = FloatOptionItem.Create(RoleInfo, 16, OptionName.HavocEatKillCool, OptionBaseCoolTime, 5f, false)
            .SetValueFormat(OptionFormat.Seconds).SetZeroNotation(OptionZeroNotation.Off);
        OptEatCooldown = FloatOptionItem.Create(RoleInfo, 14, OptionName.HavoceatCooldown, OptionBaseCoolTime, 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptMaxEat = IntegerOptionItem.Create(RoleInfo, 17, OptionName.HavocCanEatCount, new(1, 13, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);

        OptCooldown = FloatOptionItem.Create(RoleInfo, 13, OptionName.HavocCoolDown, OptionBaseCoolTime, 20f, false)
            .SetValueFormat(OptionFormat.Seconds);
        OptDuration = FloatOptionItem.Create(RoleInfo, 15, OptionName.HavocDuration, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Seconds);

        OptionVentCount = IntegerOptionItem.Create(RoleInfo, 12, OptionName.HavocCanVentCount, new(1, 13, 1), 2, false)
            .SetValueFormat(OptionFormat.Times)
            .SetEnabled(() => OptionCanVent.GetBool());
        OptArrowCount = IntegerOptionItem.Create(RoleInfo, 20, OptionName.HavocArrowCount, new(0, 13, 1), 3, false)
            .SetValueFormat(OptionFormat.Times);
        OptToboeDis = FloatOptionItem.Create(RoleInfo, 18, OptionName.HavocToboeDis, OptionBaseCoolTime, 10f, false)
            .SetValueFormat(OptionFormat.Multiplier);

        RoleAddAddons.Create(RoleInfo, 21, NeutralKiller: true);
    }
    public override bool CanClickUseVentButton => (CanVent && EatCount >= VentCount) || !toboe;
    public ISchrodingerCatOwner.TeamType SchrodingerCatChangeTo => ISchrodingerCatOwner.TeamType.Havoc;
    public float CalculateKillCooldown() => KillCooldown;
    public bool CanUseImpostorVentButton() => (CanVent && EatCount >= VentCount) || !toboe;
    public bool IsresetAfterKill => false;
    public override void Add()
    {
        KillCooldown = OptionKillCooldown.GetFloat();
        EatKillCool = OptEatKillCool.GetFloat();
        MaxEat = OptMaxEat.GetInt();
        Toboedis = OptToboeDis.GetFloat();
        EatCount = 0;
        Eatmode = true;
        VentCount = OptionVentCount.GetInt();
        Cooltimer = OptCooldown.GetFloat();
        EatCooltimer = OptEatCooldown.GetFloat();
        Duration = OptDuration.GetFloat();
        ArrowCount = OptArrowCount.GetInt();
        Arrow = ArrowCount == 0;
        Nowuse = false;

        _ = new LateTask(SyncHavocAllies, Main.LagTime, "HavocSync", true);
    }

    public override void AfterMeetingTasks()
    {
        SyncHavocAllies();
    }

    public static void SyncHavocAllies()
    {
        foreach (var a in Havocs)
            foreach (var b in Havocs)
            {
                if (a == b) continue;
                var pa = PlayerCatch.GetPlayerById(a);
                var pb = PlayerCatch.GetPlayerById(b);
                if (pa == null || pb == null || !pa.IsAlive() || !pb.IsAlive()) continue;
                var ida = pa.GetClientId();
                if (ida == -1) continue;
                // aのクライアントに「bはインポスター」と伝える
                pb.RpcSetRoleDesync(RoleTypes.Impostor, ida);
            }
    }

    public void OnCheckMurderAsKiller(MurderInfo info)
    {
        var (killer, target) = info.AttemptTuple;

        if (!Is(killer)) return;
        if (target.Is(CustomRoles.Havoc)) info.DoKill = false;
    }

    public override void OnFixedUpdate(PlayerControl player)
    {
        if (AmongUsClient.Instance.AmHost is false) return;

        var havocfirst = Havocs.FirstOrDefault();
        if (player.PlayerId == havocfirst)
        {
            foreach (var pid in ToboePlayers.Keys)
            {
                ToboePlayers[pid] -= Time.fixedDeltaTime;
            }
        }

        if (player.IsAlive() is false) return;

        if (EatCooltimer > 0f) EatCooltimer -= Time.fixedDeltaTime;
        if (Cooltimer > 0f) Cooltimer -= Time.fixedDeltaTime;
    }
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;

        if (Eatmode)
        {
            var body = GetNearestEatableBody();
            if (body == null || MaxEat <= 0) return;

            ResetCooldown = true;
            EatedPlayers.Add(body.ParentId);
            EatCount++;
            if (EatCount >= ArrowCount)
            {
                Arrow = true;
            }
            MaxEat--;
            EatCooltimer = OptEatCooldown.GetFloat();
            KillCooldown -= EatKillCool;
            Player.RpcSnapToForced(body.TruePosition);
            SendRPC();

            Protect();
        }
        else
        {
            if (toboe) return;
            AURoleOptions.PhantomCooldown = Duration;
            toboe = true;
            ResetCooldown = true;
            Eatmode = true;
            Nowuse = true;
            Cooltimer = OptCooldown.GetFloat();

            var tmpSpeed = Main.AllPlayerSpeed[Player.PlayerId];
            Main.AllPlayerSpeed[Player.PlayerId] = 0f;

            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[Player.PlayerId] = tmpSpeed;
            }, 1f);

            foreach (var pc in PlayerCatch.AllAlivePlayerControls)
            {
                if (Is(pc)) continue;

                var dis = Vector2.Distance(Player.GetTruePosition(), pc.GetTruePosition());
                if (dis > Toboedis) continue;

                ToboePlayers[pc.PlayerId] = 5f;
            }

            _ = new LateTask(() =>
            {
                toboe = false;
                Protect();
                ReportDeadBodyPatch.DontReport.Clear();
                Nowuse = false;
            }, Duration);
            Protect();
        }
    }
    void Protect()
    {
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
    public override void OnStartMeeting()
    {
        toboe = false;
        EatedPlayers.Clear();
    }
    public override void OnSpawn(bool initialState = false)
    {
        Cooltimer = OptCooldown.GetFloat();
        EatCooltimer = OptEatCooldown.GetFloat();
    }
    sealed class DeadBodyInfo
    {
        public byte ParentId; public Vector2 TruePosition; public float Distance;
        public DeadBodyInfo(byte id, Vector2 pos, float d) { ParentId = id; TruePosition = pos; Distance = d; }
    }
    DeadBodyInfo GetNearestEatableBody()
    {
        var EatRange = 5f;
        DeadBodyInfo nearest = null;
        var myPos = Player.GetTruePosition();
        foreach (var db in Object.FindObjectsOfType<DeadBody>())
        {
            var id = db.ParentId;
            if (EatedPlayers.Contains(id)) continue;
            var pos = db.TruePosition;
            var dist = Vector2.Distance(myPos, pos);
            if (dist > EatRange) continue;
            if (nearest == null || dist < nearest.Distance)
                nearest = new DeadBodyInfo(id, pos, dist);
        }
        return nearest;
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        opt.SetVision(OptImpVision?.GetBool() ?? true);
        AURoleOptions.PhantomCooldown = Eatmode ? EatCooltimer : Cooltimer;
    }
    public override bool CancelReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo target, ref DontReportreson reason)
    {
        if (!toboe) return false;

        reason = DontReportreson.Havoc;
        return true;
    }
    public override bool OnEnterVent(PlayerPhysics physics, int ventId)
    {
        if (AmongUsClient.Instance.AmHost is false) return CanVent && EatCount >= VentCount;

        Eatmode = !Eatmode;
        SendRPC();
        AURoleOptions.PhantomCooldown = Eatmode ? EatCooltimer : Cooltimer;
        Player.SyncSettings();
        Player.RpcResetAbilityCooldown();

        return CanVent && EatCount >= VentCount;
    }
    public override string GetSuffix(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        //seenが省略の場合seer
        seen ??= seer;

        if (!toboe || !Arrow) return "";
        if (!Is(seer) || !Is(seen)) return "";

        if (isForMeeting) return "";

        var targets = new List<byte>();

        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            targets.Add(pc.PlayerId);
            TargetArrow.Add(Player.PlayerId, pc.PlayerId);
        }

        return Utils.ColorString(RoleInfo.RoleColor, TargetArrow.GetArrows(seer, targets.ToArray()));
    }
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        var mode = Eatmode ? "[食い荒らし]" : "[遠吠え]";
        return Utils.ColorString(RoleInfo.RoleColor, $"({EatCount}){mode}");
    }

    public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
    {
        seen ??= seer;

        if (seen != seer) return "";

        var eat = "";
        var toboe = "";

        if (EatedPlayers.Contains(seen.PlayerId)) eat = Utils.ColorString(RoleInfo.RoleColor, "×");
        if (ToboePlayers.ContainsKey(seen.PlayerId)) toboe = Utils.ColorString(RoleInfo.RoleColor, "!!");

        return toboe + eat;
    }

    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        return $"<{RoleInfo.RoleColorCode}>{GetString("HavocLowerText")}</color>";
    }

    public static string GetLowerTextOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (seen != seer || (seer.GetRoleClass() is Havoc havoc && havoc.Nowuse) || !toboe) return "";

        return $"<{RoleInfo.RoleColorCode}>{GetString("HavocOtherLowerText")}</color>";
    }

    public override bool OverrideAbilityButton(out string text)
    {
        text = Eatmode ? "DeadBodyEat_Ability" : "Havoc_Ability";
        return true;
    }

    public bool OverrideImpVentButton(out string text)
    {
        text = "Mode_change";
        return true;
    }

    private void SendRPC()
    {
        using var sender = CreateSender();
        sender.Writer.Write(Eatmode);
        sender.Writer.Write(EatCount);

        var playerList = EatedPlayers.ToList();
        sender.Writer.Write(playerList.Count);

        for (int i = 0; i < playerList.Count; i++)
        {
            sender.Writer.Write(playerList[i]);
        }
    }
    public override void ReceiveRPC(MessageReader reader)
    {
        Eatmode = reader.ReadBoolean();
        EatCount = reader.ReadInt32();

        EatedPlayers.Clear();
        var count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            EatedPlayers.Add(reader.ReadByte());
        }
    }
}