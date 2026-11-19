using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Linq;
using InnerNet;
using Mathf = UnityEngine.Mathf;

using TownOfHostL.Roles.Core;
using TownOfHostL.Roles.Neutral;
using TownOfHostL.Roles.Crewmate;
using TownOfHostL.Roles.AddOns.Common;
using TownOfHostL.Roles.AddOns;

namespace TownOfHostL.Modules
{
    public class PlayerGameOptionsSender : GameOptionsSender
    {
        public static void SetDirty(PlayerControl player) => SetDirty(player.PlayerId);
        public static void SetDirty(byte playerId) =>
            AllSenders.OfType<PlayerGameOptionsSender>()
            .Where(sender => sender.player.PlayerId == playerId)
            .ToList().ForEach(sender => sender.SetDirty());
        public static void SetDirtyToAll() =>
            AllSenders.OfType<PlayerGameOptionsSender>()
            .ToList().ForEach(sender => sender.SetDirty());

        private IGameOptions cachedGameOptions = null;
        public override IGameOptions BasedGameOptions =>
            Main.RealOptionsData.Restore(
                cachedGameOptions ??= new NormalGameOptionsV12(new UnityLogger().Cast<ILogger>()).Cast<IGameOptions>()
            );
        public override bool IsDirty { get; protected set; }

        public PlayerControl player;
        // 最後に送った設定のバイト列。同じなら送らない。会議中に送った物は覚えない (会議の後に必ず送り直す)
        byte[] lastSent;

        public PlayerGameOptionsSender(PlayerControl player)
        {
            this.player = player;
        }
        public void SetDirty() => IsDirty = true;

        public override void SendGameOptions()
        {
            var opt = BuildGameOptions();
            if (opt == null) return;
            if (player.AmOwner)
            {
                foreach (var com in GameManager.Instance.LogicComponents)
                {
                    if (com.TryCast<LogicOptions>(out var lo))
                    {
                        lo.SetGameOptions(opt);
                        lo.ClearDirtyFlag();
                    }
                }
                GameOptionsManager.Instance.CurrentGameOptions = opt;
            }
            else
            {
                SendBuiltOptions(opt);
            }
        }

        protected override bool ShouldSend(Il2CppStructArray<byte> optionArray)
        {
            int n = optionArray.Length;
            bool same = lastSent != null && lastSent.Length == n;
            if (same)
            {
                for (int i = 0; i < n; i++)
                    if (lastSent[i] != optionArray[i]) { same = false; break; }
            }
            if (same) return false;
            if (GameStates.IsMeeting) lastSent = null;
            else
            {
                if (lastSent == null || lastSent.Length != n) lastSent = new byte[n];
                for (int i = 0; i < n; i++) lastSent[i] = optionArray[i];
            }
            return true;
        }

        public override void SendOptionsArray(Il2CppStructArray<byte> optionArray)
        {
            for (byte i = 0; i < GameManager.Instance.LogicComponents.Count; i++)
            {
                if (GameManager.Instance.LogicComponents[i].TryCast<LogicOptions>(out _))
                {
                    SendOptionsArray(optionArray, i, player.GetClientId());
                }
            }
        }
        public static void RemoveSender(byte playerId)
        {
            var sender = AllSenders.OfType<PlayerGameOptionsSender>()
            .FirstOrDefault(sender => sender.player != null && sender.player.PlayerId == playerId);
            if (sender == null) return;
            sender.player = null;
            AllSenders.Remove(sender);
        }
        public override IGameOptions BuildGameOptions()
        {
            if (Main.RealOptionsData == null)
            {
                Main.RealOptionsData = new OptionBackupData(GameOptionsManager.Instance.CurrentGameOptions);
            }

            var opt = BasedGameOptions;
            AURoleOptions.SetOpt(opt);
            if (Roles.Impostor.NormalViper.RoleInfo.IsEnable)
                AURoleOptions.ViperDissolveTime = Roles.Impostor.NormalViper.DissolveTime;
            var state = PlayerState.GetByPlayerId(player.PlayerId);
            if (state.IsBlackOut)
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, 0f);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0f);
            }
            else
            {
                opt.SetFloat(FloatOptionNames.CrewLightMod, Main.DefaultCrewmateVision);
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, Main.DefaultImpostorVision);
            }

            CustomRoles role = player.GetCustomRole();
            switch (role.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    AURoleOptions.ShapeshifterCooldown = Options.DefaultShapeshiftCooldown.GetFloat();
                    break;
                case CustomRoleTypes.Madmate:
                    AURoleOptions.EngineerCooldown = Options.MadmateVentCooldown.GetFloat();
                    AURoleOptions.EngineerInVentMaxTime = Options.MadmateVentMaxTime.GetFloat();
                    if (Options.MadmateHasImpostorVision.GetBool())
                        opt.SetVision(true);
                    if (Options.MadmateCanSeeOtherVotes.GetBool())
                        opt.SetBool(BoolOptionNames.AnonymousVotes, false);
                    break;
            }
            if (role.IsCCCatRoles())
            {
                CatchCat.CatPlayer.ApplyGameOptions(player, opt);
            }

            var roleClass = player.GetRoleClass();
            roleClass?.ApplyGameOptions(opt);
            var noisemaker = Main.AllPlayerControls.FirstOrDefault(pc =>
                pc.GetCustomRole().GetRoleTypes() == RoleTypes.Noisemaker);
            noisemaker?.GetRoleClass()?.ApplyGameOptions(opt);
            foreach (var subRole in player.GetCustomSubRoles())
                AddOnList.Get(subRole)?.ApplyGameOptions?.Invoke(opt);
            Blinder.ApplyGameOptionsByOther(player.PlayerId, opt);
            Streaming.Audience.ApplyGameOptions(player.PlayerId, opt);

            if (Main.AllPlayerKillCooldown.TryGetValue(player.PlayerId, out var killCooldown))
            {
                AURoleOptions.KillCooldown = Mathf.Max(0f, killCooldown);
            }

            AURoleOptions.PlayerSpeedMod = Mathf.Clamp(PlayerSpeed.Get(player.PlayerId), Main.MinSpeed, 3f);

            state.taskState.hasTasks = Utils.HasTasks(player.Data, false);
            if (!Options.GhostCantSeeOtherVotes.GetBool() && player.Data.IsDead)
                opt.SetBool(BoolOptionNames.AnonymousVotes, false);
            if (Options.AdditionalEmergencyCooldown.GetBool() &&
                Options.AdditionalEmergencyCooldownThreshold.GetInt() <= Utils.AllAlivePlayersCount)
            {
                opt.SetInt(
                    Int32OptionNames.EmergencyCooldown,
                    Options.AdditionalEmergencyCooldownTime.GetInt());
            }
            if (Options.SyncButtonMode.GetBool() && Options.SyncedButtonCount.GetValue() <= Options.UsedButtonCount)
            {
                opt.SetInt(Int32OptionNames.EmergencyCooldown, 3600);
            }
            if (Core.Modes.ModeCatalog.Current.IsHideAndSeek && Options.HideAndSeekKillDelayTimer > 0)
            {
                opt.SetFloat(FloatOptionNames.ImpostorLightMod, 0f);
                if (player.Is(CountTypes.Impostor))
                {
                    AURoleOptions.PlayerSpeedMod = Main.MinSpeed;
                }
            }
            MeetingTimeManager.ApplyGameOptions(opt);

            AURoleOptions.ShapeshifterCooldown = Mathf.Max(1f, AURoleOptions.ShapeshifterCooldown);
            AURoleOptions.ProtectionDurationSeconds = 0f;

            return opt;
        }

        public override bool AmValid()
        {
            return base.AmValid() && player != null && !player.Data.Disconnected && Main.RealOptionsData != null;
        }
    }
}