using System.Collections.Generic;
using System.Linq;
using System.Text;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor
{
    public class TargetData(byte playerId, float timer)
    {
        public byte PlayerId { get; } = playerId;
        public float Timer { get; set; } = timer;
    }

    enum ButtonText
    {
        None,
        Ueru,
        Mizuyari
    }

    public sealed class Florist : RoleBase, IImpostor, IUsePhantomButton
    {
        public static readonly SimpleRoleInfo RoleInfo =
            SimpleRoleInfo.Create(
                typeof(Florist),
                player => new Florist(player),
                CustomRoles.Florist,
                () => RoleTypes.Phantom,
                CustomRoleTypes.Impostor,
                26100,
                SetupOptionItem,
                "Flr",
                OptionSort: (3, 14),
                Desc: () =>
                    string.Format(GetString("FloristDesc"),
                        OptUsePoint.GetInt(),
                        OptTurnPoint.GetInt(),
                        OptDeadPoint.GetInt(),
                        OptFlowerTime.GetFloat(),
                        OptKnowTarget.GetBool() ? GetString("FloristDesctarget") : ""
                        )
            );
        public Florist(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            DeadPoint = OptDeadPoint.GetInt();
            nowCooldown = CoolDown.GetFloat();
            TurnPoint = OptTurnPoint.GetInt();
            UsePoint = OptUsePoint.GetInt();
            KnowTarget = OptKnowTarget.GetBool();
            FlowerTime = OptFlowerTime.GetFloat();
            dis = OptDistance.GetFloat();
            Datas.Clear();
            InstanceFlower.Clear();
            Max = OptMax.GetInt();
            NowText = ButtonText.Ueru;
            Changed = false;

            CustomRoleManager.MarkOthers.Add(GetMarkOthers);
        }
        public static HashSet<byte> KnowPlayers = new();
        public static Dictionary<byte, int> FlowerPlayers = new();
        public static HashSet<byte> InstanceFlower = new();
        public List<TargetData> Datas = new();
        [GameModuleInitializer]
        public static void Init()
        {
            FlowerPlayers.Clear();
            KnowPlayers.Clear();
        }

        static OptionItem KillCoolDown;
        static OptionItem CoolDown;
        static OptionItem OptFlowerTime; static float FlowerTime;
        static OptionItem OptDistance; static float dis;
        static OptionItem OptDeadPoint; static int DeadPoint;
        static OptionItem OptTurnPoint; static int TurnPoint;
        static OptionItem OptUsePoint; static int UsePoint;
        static OptionItem OptKnowTarget; static bool KnowTarget;
        static OptionItem OptMax;
        int Max;
        ButtonText NowText;
        bool Changed;
        float nowCooldown;
        enum Op
        {
            FloristDeadPoint,
            FloristTurnPoint,
            FloristUsePoint,
            FloristKnowTarget,
            FloristDis,
            FloristTime,
            FlorlistMax
        }

        private static void SetupOptionItem()
        {
            KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            CoolDown = FloatOptionItem.Create(RoleInfo, 7, GeneralOption.Cooldown, OptionBaseCoolTime, 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptMax = IntegerOptionItem.Create(RoleInfo, 11, Op.FlorlistMax, new(1, 14, 1), 2, false)
                .SetValueFormat(OptionFormat.Times);
            OptFlowerTime = FloatOptionItem.Create(RoleInfo, 9, Op.FloristTime, OptionBaseCoolTime, 2f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptDistance = FloatOptionItem.Create(RoleInfo, 10, Op.FloristDis, new(0.25f, 3f, 0.25f), 1.5f, false)
                .SetValueFormat(OptionFormat.Multiplier);

            OptDeadPoint = IntegerOptionItem.Create(RoleInfo, 4, Op.FloristDeadPoint, new(1, 99, 1), 5, false)
                .SetValueFormat(OptionFormat.Times);
            OptTurnPoint = IntegerOptionItem.Create(RoleInfo, 5, Op.FloristTurnPoint, new(1, 99, 1), 1, false)
                .SetValueFormat(OptionFormat.Times);
            OptUsePoint = IntegerOptionItem.Create(RoleInfo, 6, Op.FloristUsePoint, new(1, 99, 1), 2, false)
                .SetValueFormat(OptionFormat.Times);
            OptKnowTarget = BooleanOptionItem.Create(RoleInfo, 8, Op.FloristKnowTarget, true, false);
        }
        public float CalculateKillCooldown() => KillCoolDown.GetFloat();
        bool IUsePhantomButton.IsPhantomRole => Max > 0 || InstanceFlower.Count > 0;
        public override RoleTypes? AfterMeetingRole => Max > 0 || InstanceFlower.Count > 0 ? RoleTypes.Phantom : RoleTypes.Impostor;
        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = nowCooldown;
        }

        public static string GetGrowthRateCharactor(PlayerControl player)
        {
            if (player.GetCustomRole().IsImpostor()) return "\u2581";
            if (!FlowerPlayers.TryGetValue(player.PlayerId, out var r)) return "\u2581";

            var rate = (float)r / DeadPoint * 100;

            int index = (int)(rate / 12.5);
            if (index < 0) index = 0;
            if (index > 7) index = 7;

#pragma warning disable CS8509
            return index switch
            {
                0 => "\u2581", //  (1/8)
                1 => "\u2582", // ▂ (2/8)
                2 => "\u2583", // ▃ (3/8)
                3 => "\u2584", // ▄ (4/8)
                4 => "\u2585", // ▅ (5/8)
                5 => "\u2586", // ▆ (6/8)
                6 => "\u2587", // ▇ (7/8)
                7 => "\u2588", // █ (フル)
            };
#pragma warning restore CS8509
        }

        public override void OnFixedUpdate(PlayerControl player)
        {
            if (!AmongUsClient.Instance.AmHost) return;
            var old = NowText;
            var target = Player.GetKillTarget(true);

            if (target != null)
            {
                if (FlowerPlayers.ContainsKey(target.PlayerId))
                {
                    NowText = ButtonText.Mizuyari;
                }
                else
                {
                    NowText = ButtonText.Ueru;
                }
            }
            else
            {
                NowText = ButtonText.None;
            }

            if (old != NowText && (Max > 0 || InstanceFlower.Count > 0))
            {
                SendRPC();
                CustomButtonHud.BottonHud();
            }

            foreach (var data in Datas.ToArray())
            {
                var pc = PlayerCatch.GetPlayerById(data.PlayerId);
                if (pc == null || !pc.IsAlive())
                {
                    Datas.Remove(data);
                    if (FlowerPlayers.ContainsKey(pc.PlayerId))
                    {
                        FlowerPlayers.Remove(pc.PlayerId);
                    }
                    continue;
                }

                var targetdis = Vector2.Distance(pc.GetTruePosition(), Player.GetTruePosition());
                if (targetdis > dis)
                {
                    nowCooldown = 0f;
                    Player.SyncSettings();

                    Player.RpcResetAbilityCooldown();
                    Datas.Remove(data);
                    continue;
                }

                data.Timer += Time.fixedDeltaTime;

                if (data.Timer >= FlowerTime)
                {
                    Datas.Remove(data);
                    RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);

                    if (!FlowerPlayers.ContainsKey(pc.PlayerId))
                    {
                        FlowerPlayers[pc.PlayerId] = 0;
                        Max--;
                        InstanceFlower.Add(pc.PlayerId);
                        SendRPC();
                    }
                    else
                    {
                        FlowerPlayers[pc.PlayerId] += UsePoint;
                        if (FlowerPlayers[pc.PlayerId] >= DeadPoint)
                        {
                            InstanceFlower.Remove(pc.PlayerId);
                            SendRPC();

                            //死因他にいいのありそうではある
                            if (CustomRoleManager.OnCheckMurder(Player, pc, pc, pc, Killpower: 3, deathReason: CustomDeathReason.Suffocation))
                            {
                                RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
                            }                       
                        }
                    }
                    nowCooldown = CoolDown.GetFloat();
                    Player.SyncSettings();

                    Player.RpcResetAbilityCooldown();
                }
            }
        }

        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = true;
            ResetCooldown = false;

            if (Max <= 0 && InstanceFlower.Count <= 0) return;

            var target = Player.GetKillTarget(true);
            if (target == null || target.GetCustomRole().IsImpostor()) return;
            if (Max <= 0 && !FlowerPlayers.ContainsKey(target.PlayerId)) return;

            ResetCooldown = true;

            nowCooldown = FlowerTime;
            Player.SyncSettings();

            var data = new TargetData(target.PlayerId, 0f);
            Datas.Add(data);
        }
        public override void OnReportDeadBody(PlayerControl reporter, NetworkedPlayerInfo _)
        {
            Datas.Clear();
            nowCooldown = CoolDown.GetFloat();
        }

        public static void Kougosei()
        {
            foreach (var p in new List<byte>(FlowerPlayers.Keys))
            {
                FlowerPlayers[p] += TurnPoint;
            }
        }

        public override void AfterMeetingTasks()
        {
            foreach (var p in FlowerPlayers.Keys)
            {
                KnowPlayers.Add(p);
            }
        }

        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;

            var str = new StringBuilder(40);
            str.Append($"<color={RoleInfo.RoleColorCode}>");
            foreach (var player in PlayerCatch.AllPlayerControls)
            {
                //一ターン前に死んでる場合
                if (!PlayerCatch.AllAlivePlayerControls.Contains(player) && !PlayerCatch.OldAlivePlayerControles.Contains(player))
                if (!player.Is(CustomRoles.Florist))
                    str.Append(GetGrowthRateCharactor(player));
            }
            str.Append("</color>");
            return str.ToString();
        }

        public static string GetMarkOthers(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
        {
            seen ??= seer;
            if ((seer != seen || !KnowTarget) && !seer.GetCustomRole().IsImpostor()) return "";

            if (FlowerPlayers.TryGetValue(seen.PlayerId, out var a))
            {
                return Utils.ColorString(
                    UtilsRoleText.GetRoleColor(CustomRoles.Florist), $"▼({a})");
            }

            return "";
        }

        public override string GetProgressText(bool comms = false, bool GameLog = false)
            => Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Florist), $"({Max})");

        private void SendRPC()
        {
            using var sender = CreateSender();
            sender.Writer.Write((byte)NowText);
            sender.Writer.WritePacked(InstanceFlower.Count);
            var count = InstanceFlower.Count;
            var list = InstanceFlower.ToArray();
            for (int i = 0; i < count; i++)
            {
                sender.Writer.Write(list[i]);
            }
        }

        public override void ReceiveRPC(MessageReader reader)
        {
            NowText = (ButtonText)reader.ReadByte();
            InstanceFlower.Clear();
            var count = reader.ReadPackedInt32();
            for (int i = 0; i < count; i++)
            {
                InstanceFlower.Add(reader.ReadByte());
            }

            CustomButtonHud.BottonHud();
        }

        public override string GetAbilityButtonText()
            => NowText is ButtonText.Ueru or ButtonText.None && Max > 0 ? GetString("FloristUeru") :  GetString("FloristWater");

        public override bool OverrideAbilityButton(out string text)
        {
            if (Max <= 0)
            {
                text = "Florist_Phantom_2";
                return true;
            }
            if (NowText is ButtonText.Ueru)
            {
                text = "Florist_Phantom_1";
            }
            else if (NowText is ButtonText.Mizuyari)
            {
                text = "Florist_Phantom_2";
            }
            else
            {
                text = "Florist_Phantom_1";
            }
            return true;
        }
    }
}