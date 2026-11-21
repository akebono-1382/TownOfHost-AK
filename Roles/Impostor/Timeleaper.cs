using System;
using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using TownOfHost.Attributes;
using TownOfHost.Patches.ISystemType;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Timeleaper : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Timeleaper),
            player => new Timeleaper(player),
            CustomRoles.Timeleaper,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            28400,
            SetupOptionItem,
            "tml",
            OptionSort: (6, 16),
            from: From.TownOfHost_Pko
        );
    public Timeleaper(PlayerControl player)
        : base(RoleInfo, player)
    {
        Cooldown = OptionCooldown.GetFloat();
        NowCooldown = Cooldown;
        RecTime = OptionRecTime.GetFloat();
        leapspeed = OptionleapSpeed.GetInt();

        CustomRoleManager.OnFixedUpdateOthers.Add(OnFixedUpdateOthers);
    }

    enum Leapstatus
    {
        /// <summary>通常</summary>
        None,
        /// <summary>録画中</summary>
        Recording,
        /// <summary>巻き戻し中</summary>
        Leaping,
    }

    static OptionItem OptionCooldown; static float Cooldown; //クールダウン
    static OptionItem OptionRecTime; static float RecTime; //録画できる時間
    static OptionItem OptionleapSpeed; static int leapspeed; //巻き戻す速さ
    float NowCooldown;

    static Leapstatus Nowstatus = Leapstatus.None;
    static readonly Dictionary<byte, List<Vector2>> posinfos = new(); //録画した位置(古い順)
    static readonly Dictionary<byte, int> leapWait = new(); //巻き戻し対象と、TP送信までの待機tick
    static readonly Dictionary<byte, float> savedSpeed = new(); //巻き戻し前の移動速度
    static readonly List<SystemTypes> savedSabotages = new(); //録画開始時に発生していたサボタージュ
    static int leapIndex; //今どこまで戻っているか
    static float RecordTimer; //残り録画時間
    static byte Usingid = byte.MaxValue; //録画している人
    const int Teleportwait = 15;

    static readonly SystemTypes[] SabotageTypes =
    {
        SystemTypes.Reactor,
        SystemTypes.Laboratory,
        SystemTypes.HeliSabotage,
        SystemTypes.LifeSupp,
        SystemTypes.Comms,
        SystemTypes.Electrical,
    };

    [GameModuleInitializer]
    public static void Init()
    {
        posinfos.Clear();
        leapWait.Clear();
        savedSpeed.Clear();
        savedSabotages.Clear();
        leapIndex = 0;
        RecordTimer = 999f;
        Usingid = byte.MaxValue;
        Nowstatus = Leapstatus.None;
    }
    static void SetupOptionItem()
    {
        OptionCooldown = FloatOptionItem.Create(RoleInfo, 10, "Cooldown", OptionBaseCoolTime, 30f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionRecTime = FloatOptionItem.Create(RoleInfo, 11, "TimeleaperRecTime", OptionBaseCoolTime, 10f)
            .SetValueFormat(OptionFormat.Seconds);
        OptionleapSpeed = IntegerOptionItem.Create(RoleInfo, 12, "TimeleaperleapSpeed", new(1, 10, 1), 1)
            .SetValueFormat(OptionFormat.Multiplier);
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = NowCooldown;
    }
    bool IUsePhantomButton.IsresetAfterKill => false;
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = true;
        if (Nowstatus is not Leapstatus.None || Usingid is not byte.MaxValue)
        {
            ResetCooldown = false;
            return;
        }

        posinfos.Clear();
        leapWait.Clear();
        savedSpeed.Clear();
        savedSabotages.Clear();

        foreach (var type in SabotageTypes)
        {
            if (Utils.IsActive(type)) savedSabotages.Add(type);
        }

        Usingid = Player.PlayerId;
        Nowstatus = Leapstatus.Recording;

        NowCooldown = RecTime;
        RecordTimer = RecTime;
        Player.SyncSettings();

        Logger.Info($"{Player.GetRealName()}が録画を開始 (サボタージュ{savedSabotages.Count}件記録)", "Timeleaper");
    }
    public override void OnStartMeeting()
    {
        NowCooldown = Cooldown;
        if (Nowstatus is Leapstatus.None) return;

        Logger.Info("会議開始のためタイムリープを中断", "Timeleaper");
        ResetState();
    }
    public static void OnFixedUpdateOthers(PlayerControl player)
    {
        if (Nowstatus is Leapstatus.None) return;

        var owner = PlayerCatch.GetPlayerById(Usingid);
        if ((owner == null || !owner.IsAlive()))
        {
            Logger.Info("録画者が不在のためタイムリープを中断", "Timeleaper");
            ResetState();
            return;
        }

        var isOwner = player.PlayerId == Usingid;

        switch (Nowstatus)
        {
            case Leapstatus.Recording:
                if (player.IsAlive())
                {
                    if (!posinfos.TryGetValue(player.PlayerId, out var list))
                    {
                        list = new List<Vector2>();
                        posinfos[player.PlayerId] = list;
                    }
                    list.Add(player.transform.position);
                }
                if (isOwner)
                {
                    RecordTimer -= Time.fixedDeltaTime;
                    if (RecordTimer <= 0f) StartLeaping(owner);
                }
                break;

            case Leapstatus.Leaping:
                if (isOwner)
                {
                    leapIndex -= leapspeed;
                    if (leapIndex <= 0)
                    {
                        FinishLeaping(owner);
                        return;
                    }
                }

                if (!player.IsAlive() || !leapWait.TryGetValue(player.PlayerId, out var wait)) return;
                wait++;
                if (wait >= Teleportwait)
                {
                    wait = 0;
                    SnapTo(player, leapIndex);
                }
                leapWait[player.PlayerId] = wait;
                break;
        }
    }

    static void StartLeaping(PlayerControl owner)
    {
        Logger.Info($"{owner.GetRealName()}の録画が終了、巻き戻しを開始", "Timeleaper");

        Nowstatus = Leapstatus.Leaping;
        RecordTimer = 999f;
        leapIndex = posinfos.Count == 0 ? 0 : posinfos.Values.Max(l => l.Count) - 1;

        leapWait.Clear();
        savedSpeed.Clear();
        foreach (var pc in PlayerCatch.AllAlivePlayerControls)
        {
            if (!posinfos.ContainsKey(pc.PlayerId)) continue;

            Logger.Info($"{pc.GetRealName()}を巻き戻し対象に登録", "Timeleaper");
            leapWait[pc.PlayerId] = 0;

            //巻き戻し中は移動不可にする
            savedSpeed[pc.PlayerId] = Main.AllPlayerSpeed[pc.PlayerId];
            Main.AllPlayerSpeed[pc.PlayerId] = Main.MinSpeed;
            pc.MarkDirtySettings();
        }
    }

    static void FinishLeaping(PlayerControl owner)
    {
        Logger.Info("巻き戻し完了", "Timeleaper");

        //最後に録画開始位置へ戻す
        foreach (var id in leapWait.Keys.ToList())
        {
            var pc = PlayerCatch.GetPlayerById(id);
            if (pc != null && pc.IsAlive()) SnapTo(pc, 0);
        }

        var sabotages = savedSabotages.ToList();
        var role = owner.GetRoleClass() as Timeleaper;

        ResetState();

        if (role != null)
        {
            role.NowCooldown = Cooldown;
            owner.SyncSettings();
            owner.RpcResetAbilityCooldown();
        }

        //録画開始時に発生していたサボタージュを再度起こす
        foreach (var type in sabotages)
        {
            if (Utils.IsActive(type)) continue;
            Logger.Info($"{type}を再発生", "Timeleaper");
            ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Sabotage, (byte)type);
        }
        foreach (var pc in PlayerCatch.AllPlayerControls)
        {
            RPC.PlaySoundRPC(pc.PlayerId, Sounds.TaskComplete);
        }
    }

    static void SnapTo(PlayerControl pc, int index)
    {
        if (!posinfos.TryGetValue(pc.PlayerId, out var list) || list.Count == 0) return;
        if (pc.onLadder) return;
        if (pc.inVent)
        {
            pc.MyPhysics.RpcBootFromVent(VentilationSystemUpdateSystemPatch.NowVentId.TryGetValue(pc.PlayerId, out var ventid) ? ventid : 0);
            pc.inVent = false;
        }

        pc.RpcSnapToForced(list[Math.Clamp(index, 0, list.Count - 1)]);
    }
    static void ResetState()
    {
        foreach (var (id, speed) in savedSpeed)
        {
            Main.AllPlayerSpeed[id] = speed;
            PlayerCatch.GetPlayerById(id)?.MarkDirtySettings();
        }
        savedSpeed.Clear();
        posinfos.Clear();
        leapWait.Clear();
        savedSabotages.Clear();
        leapIndex = 0;
        RecordTimer = 999f;
        Usingid = byte.MaxValue;
        Nowstatus = Leapstatus.None;
    }
}