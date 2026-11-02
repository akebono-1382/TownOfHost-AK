using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using TownOfHost.Attributes;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor
{
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
                OptionSort: (3, 14)
            );
        public Florist(PlayerControl player)
        : base(
            RoleInfo,
            player
        )
        {
            DeadPoint = OptDeadPoint.GetInt();
            TurnPoint = OptTurnPoint.GetInt();
            UsePoint = OptUsePoint.GetInt();
        }

        public static Dictionary<byte, int> FlowerPlayers = new();

        [GameModuleInitializer]
        public static void Init()
        {
            FlowerPlayers.Clear();
        }

        static OptionItem KillCoolDown;
        static OptionItem OptDeadPoint; static int DeadPoint;
        static OptionItem OptTurnPoint; static int TurnPoint;
        static OptionItem OptUsePoint; static int UsePoint;
        enum Op
        {
            FloristDeadPoint,
            FloristTurnPoint,
            FloristUsePoint,
        }

        private static void SetupOptionItem()
        {
            KillCoolDown = FloatOptionItem.Create(RoleInfo, 3, GeneralOption.KillCooldown, new(0, 180, 0.5f), 30f, false)
                .SetValueFormat(OptionFormat.Seconds);
            OptDeadPoint = IntegerOptionItem.Create(RoleInfo, 4, Op.FloristDeadPoint, new(1, 99, 1), 5, false);
            OptTurnPoint = IntegerOptionItem.Create(RoleInfo, 5, Op.FloristTurnPoint, new(1, 99, 1), 1, false);
            OptUsePoint = IntegerOptionItem.Create(RoleInfo, 6, Op.FloristUsePoint, new(1, 99, 1), 2, false);
        }
        public float CalculateKillCooldown() => KillCoolDown.GetFloat();

        public override void ApplyGameOptions(IGameOptions opt)
        {
            AURoleOptions.PhantomCooldown = 0.1f;
        }

        public static string GetGrowthRateCharactor(PlayerControl player)
        {
            if (player.GetCustomRole().IsImpostor()) return "\u2581";
            if (!FlowerPlayers.TryGetValue(player.PlayerId, out var r)) return "\u2581";

            // 【重要】(float) をつけて、小数点以下まで正しく計算する
            var rate = (float)r / DeadPoint * 100;

            // 前回の「なめらか（8段階）コード」に正しくパーセンテージを渡します
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



        void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
        {
            AdjustKillCooldown = true;
            ResetCooldown = false;

            var target = Player.GetKillTarget(true);
            if (target == null || target.GetCustomRole().IsImpostor()) return;

            ResetCooldown = true;
            if (!FlowerPlayers.ContainsKey(target.PlayerId))
            {
                FlowerPlayers[target.PlayerId] = 0;
                Protect();
            }
            else
            {
                FlowerPlayers[target.PlayerId] += UsePoint;
                if (FlowerPlayers[target.PlayerId] >= DeadPoint)
                {
                    //死因他にいいのありそうではある
                    if (CustomRoleManager.OnCheckMurder(Player, target, target, target, Killpower: 3, deathReason: CustomDeathReason.Suffocation))
                        RPC.PlaySoundRPC(Player.PlayerId, Sounds.KillSound);
                }
                else
                {
                    Protect();
                }
            }
        }

        void Protect()
        {
            return;
# pragma warning disable CS0162

            IUsePhantomButton.IPPlayerKillCooldown.TryGetValue(Player.PlayerId, out float TurnTimer);
            Main.AllPlayerKillCooldown.TryGetValue(Player.PlayerId, out var killcool);
            if (MeetingStates.FirstMeeting && !Options.FixFirstKillCooldown.GetBool() && killcool > 10 &&
            (PlayerState.GetByPlayerId(Player.PlayerId)?.Is10secKillButton == true))
                killcool = 10;
            float cooldown = killcool - TurnTimer;
            if (cooldown <= 1) cooldown = 0.005f;

            Player.SetKillCooldown(cooldown);
#pragma warning restore CS0162
        }

        public override void AfterMeetingTasks()
        {
            foreach (var p in FlowerPlayers.Keys)
            {
                var target = PlayerCatch.GetPlayerById(p);
                if (target is null) return;

                FlowerPlayers[target.PlayerId] += TurnPoint;
            }
        }

        public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
        {
            seen ??= seer;

            var str = new StringBuilder(40);
            str.Append($"<color={RoleInfo.RoleColorCode}>");
            foreach (var player in PlayerCatch.AllAlivePlayerControls)
            {
                if (!player.Is(CustomRoles.Florist))
                    str.Append(GetGrowthRateCharactor(player));
            }
            str.Append("</color>");
            return str.ToString();
        }

        public override string GetMark(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false)
        {
            seen ??= seer;

            if (FlowerPlayers.TryGetValue(seen.PlayerId, out var a))
            {
                return Utils.ColorString(
                    UtilsRoleText.GetRoleColor(CustomRoles.Florist), $"({a})");
            }

            return "";
        }
    }
}