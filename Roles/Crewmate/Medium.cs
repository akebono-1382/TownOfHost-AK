using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;

using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;
using static TownOfHost.Modules.SelfVoteManager;

namespace TownOfHost.Roles.Crewmate;

public sealed class Medium : RoleBase, ISelfVoter
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Medium),
            player => new Medium(player),
            CustomRoles.Medium,
            () => RoleTypes.Crewmate,
            CustomRoleTypes.Crewmate,
            28300,
            SetupOptionItem,
            "Mdm",
            "#7fffd4",
            (3, 11),
            introSound: () => GetIntrosound(RoleTypes.Crewmate)
        );
    public Medium(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
        use = OptionMaximum.GetInt();
        if (use == 0) use = 999;
        onemeeting = Option1MeetingMaximum.GetInt();
        if (onemeeting == 0) onemeeting = 999;

        nowselect = 0;
        seerole.Clear();
    }
    #region 変数
    Dictionary<byte, byte> TellSelct = new();
    bool Canuseability() => GameStates.AlreadyDied && use > 0 && onemeeting > 0 && MyTaskState.HasCompletedEnoughCountOfTasks(OptionCanTaskcount.GetInt());
    int nowselect;
    Dictionary<byte, CustomRoles> seerole = new();
    public override CustomRoles Misidentify() => MyTaskState.HasCompletedEnoughCountOfTasks(OptionCanTaskcount.GetInt()) || OptAwakening.GetBool() is false ? CustomRoles.NotAssigned : CustomRoles.Crewmate;
    #endregion
    #region Option
    enum Option
    {
        TellMaximum,
        TellerCanSeeRolename, //占った相手の名前の上に占い結果を表示するかの設定
        TellRole, //占い時役職を表示するか、陣営を表示するかの設定
    }
    public static OptionItem OptionMaximum; int use;
    public static OptionItem Option1MeetingMaximum; int onemeeting;
    public static OptionItem Optionrolename;
    public static OptionItem OptionRole;
    public static OptionItem OptionCanTaskcount;
    public static OptionItem OptAwakening;
    private static void SetupOptionItem()
    {
        OptionMaximum = IntegerOptionItem.Create(RoleInfo, 10, Option.TellMaximum, new(0, 14, 1), 1, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        Option1MeetingMaximum = IntegerOptionItem.Create(RoleInfo, 15, GeneralOption.MeetingMaxTime, new(0, 14, 1), 0, false)
            .SetValueFormat(OptionFormat.Times).SetZeroNotation(OptionZeroNotation.Infinity);
        Optionrolename = BooleanOptionItem.Create(RoleInfo, 12, Option.TellerCanSeeRolename, true, false);
        OptionRole = BooleanOptionItem.Create(RoleInfo, 13, Option.TellRole, true, false);
        OptionCanTaskcount = IntegerOptionItem.Create(RoleInfo, 14, GeneralOption.cantaskcount, new(0, 99, 1), 5, false);
        OptAwakening = BooleanOptionItem.Create(RoleInfo, 16, GeneralOption.AbilityAwakening, false, false);
    }
    #endregion
    #region Text
    public override string GetProgressText(bool comms = false, bool GameLog = false)
    {
        string usestr = use == 999 ? "" : $"({use})";
        string onestr = onemeeting == 999 ? "" : $"[{onemeeting}]";

        // 能力が使えない状態か
        bool taskNotDone = OptionCanTaskcount == null
            || MyTaskState == null
            || !MyTaskState.HasCompletedEnoughCountOfTasks(OptionCanTaskcount.GetInt());

        var useColor = RoleInfo.RoleColor;
        var oneColor = RoleInfo.RoleColor;

        if (taskNotDone || use is 0)
        {
            useColor = Palette.DisabledGrey;
            oneColor = Palette.DisabledGrey;
        }
        else if (onemeeting is 0)
        {
            oneColor = Palette.DisabledGrey;
        }

        return Utils.ColorString(useColor, usestr) + Utils.ColorString(oneColor, onestr);
    }
    public override void OnStartMeeting()
    {
        nowselect = 0;
        TellSelct.Clear();
        onemeeting = Option1MeetingMaximum.GetInt();
        if (onemeeting == 0) onemeeting = 999;
    }
    public override string GetLowerText(PlayerControl seer, PlayerControl seen = null, bool isForMeeting = false, bool isForHud = false)
    {
        seen ??= seer;
        if (isForMeeting && Player.IsAlive() && GameStates.AlreadyDied && seer.PlayerId == seen.PlayerId && Canuseability())
        {
            var mes = $"<color={RoleInfo.RoleColorCode}>{GetString("SelfVoteRoleInfoMeg")}</color>";
            return isForHud ? mes : $"<size=40%>{mes}</size>";
        }
        return "";
    }
    public override void OverrideDisplayRoleNameAsSeer(PlayerControl seen, ref bool enabled, ref Color roleColor, ref string roleText, ref bool addon)
    {
        if (!seerole.TryGetValue(seen.PlayerId, out var role)) return;
        if (Optionrolename.GetBool() is false) return;

        enabled = true;
        addon = false;

        if (OptionRole.GetBool())
        {
            roleColor = UtilsRoleText.GetRoleColor(role);
            roleText = UtilsRoleText.GetRoleName(role);
        }
        else
        {
            switch (role.GetCustomRoleTypes())
            {
                case CustomRoleTypes.Impostor:
                    roleColor = UtilsRoleText.GetRoleColor(CustomRoles.Impostor);
                    roleText = UtilsRoleText.GetRoleName(CustomRoles.Impostor);
                    break;
                case CustomRoleTypes.Madmate:
                    roleColor = UtilsRoleText.GetRoleColor(CustomRoles.Madmate);
                    roleText = UtilsRoleText.GetRoleName(CustomRoles.Madmate);
                    break;
                case CustomRoleTypes.Crewmate:
                    roleColor = UtilsRoleText.GetRoleColor(CustomRoles.Crewmate);
                    roleText = UtilsRoleText.GetRoleName(CustomRoles.Crewmate);
                    break;
                case CustomRoleTypes.Neutral:
                    roleColor = ModColors.NeutralGray;
                    roleText = GetString("Neutral");
                    break;
            }
        }
    }
    #endregion
    #region Tell
    public override bool CheckVoteAsVoter(byte votedForId, PlayerControl voter)
    {
        if (!Canuseability()) return true;
        if (CheckSelfVoteMode(Player, votedForId, out var status))
        {
            bool keepMode = status is VoteStatus.Self;
            if (status is VoteStatus.Self)
            {
                nowselect = 0;
                MakeVoteSelect();
            }
            if (status is VoteStatus.Skip)
                Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            if (status is VoteStatus.Vote)
                keepMode = UseTellAbility(votedForId); // 次のタブならtrue
            SetMode(Player, keepMode);
            return false;
        }
        return true;
    }

    bool UseTellAbility(byte votefor)
    {
        if (!TellSelct.TryGetValue(votefor, out var deadId)) return false;

        // 次のタブ
        if (deadId == byte.MaxValue)
        {
            var deadCount = PlayerCatch.AllPlayerControls.Count(p => !p.IsAlive());
            var slotCount = PlayerCatch.AllAlivePlayerControls.Count(p => p.PlayerId != Player.PlayerId);
            nowselect += slotCount - 1;
            if (nowselect >= deadCount) nowselect = 0;
            MakeVoteSelect();
            return true;
        }

        var targetpc = PlayerCatch.GetPlayerById(deadId);
        if (targetpc == null || targetpc.IsAlive()) return true;
        if (seerole.ContainsKey(targetpc.PlayerId)) // 既に霊媒済み
        {
            return true;
        }
        if (use != 999)
            use--;
        if (onemeeting != 999)
            onemeeting--;

        Utils.SendMessage(
            string.Format(GetString("Medium.Result"),
                targetpc.GetPlayerColor(),
                UtilsRoleText.GetRoleColorAndtext(targetpc.GetCustomRole())),
            Player.PlayerId, Utils.ColorString(RoleInfo.RoleColor, GetString("Message.MsgTitle")));
        nowselect = 0;
        TellSelct.Clear();
        return false;
    }

    void MakeVoteSelect()
    {
        TellSelct.Clear();

        var dead = PlayerCatch.AllPlayerControls.Where(p => !p.IsAlive()).ToList();
        // 自投票はモード切替に使うので、自分以外の生存者をスロットにする
        var slots = PlayerCatch.AllAlivePlayerControls.Where(p => p.PlayerId != Player.PlayerId).ToList();

        if (dead.Count == 0 || slots.Count == 0)
        {
            Utils.SendMessage(GetString("VoteSkillFin"), Player.PlayerId);
            return;
        }

        // スロットが足りない時だけ、最後のスロットを「次のタブ」にする
        bool needPaging = dead.Count > slots.Count && slots.Count > 1;
        int perPage = needPaging ? slots.Count - 1 : slots.Count;

        if (nowselect >= dead.Count) nowselect = 0;

        var lines = new List<string>();
        for (int i = 0; i < perPage; i++)
        {
            int deadIndex = nowselect + i;
            if (deadIndex >= dead.Count) break;
            TellSelct[slots[i].PlayerId] = dead[deadIndex].PlayerId;
            lines.Add(string.Format(GetString("Medium.Votemsg"), slots[i].GetPlayerColor(), dead[deadIndex].GetPlayerColor()));
        }

        // 次のタブ(まだ表示していない死亡者が残っている場合)
        if (needPaging)
        {
            var nextSlot = slots[perPage];
            TellSelct[nextSlot.PlayerId] = byte.MaxValue;
            lines.Add(string.Format(GetString("Medium.NextTab"), nextSlot.GetPlayerColor()));
        }

        Utils.SendMessage(
            string.Format(GetString("SkillMode"),
            GetString("Mode.Divied"),
            GetString("VoteSkillMode"))
            + string.Join("\n", lines),
            Player.PlayerId);
    }
    #endregion
}