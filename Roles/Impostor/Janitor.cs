using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using TownOfHost.Roles.Neutral;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

namespace TownOfHost.Roles.Impostor;

public sealed class Janitor : RoleBase, IImpostor, IUsePhantomButton
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Janitor),
            player => new Janitor(player),
            CustomRoles.Janitor,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            26800,
            null,
            "Jtr",
            OptionSort: (0, 1),
            tab: TabGroup.Combinations,
            combination: CombinationRoles.GodfatherandJanitor,
            assignInfo: new RoleAssignInfo(CustomRoles.Janitor, CustomRoleTypes.Impostor)
            {
                AssignCountRule = new(1, 1, 1),
                AssignUnitRoles = new CustomRoles[2] { CustomRoles.Godfather, CustomRoles.Janitor }
            },
            from: From.TheOtherRoles
        );
    public Janitor(PlayerControl player)
    : base(
        RoleInfo,
        player
    )
    {
    }
    public override void ApplyGameOptions(IGameOptions opt)
    {
        AURoleOptions.PhantomCooldown = Godfather.Cooldown.GetFloat();
    }
    bool IUsePhantomButton.IsPhantomRole => true;
    bool IUsePhantomButton.IsresetAfterKill => false;
    void IUsePhantomButton.OnClick(ref bool AdjustKillCooldown, ref bool? ResetCooldown)
    {
        AdjustKillCooldown = true;
        ResetCooldown = false;
        var body = GetNearestEatableBody();
        if (body == null) return;
        ResetCooldown = true;
        Player.RpcProtectedMurderPlayer();
        RPC.PlaySoundRPC(Player.PlayerId, Sounds.TaskComplete);
        Vulture.staticEatedPlayers.Add(body.ParentId);
        SendRPC(body.ParentId);
    }
    public bool CanUseKillButton() => false;
    sealed class DeadBodyInfo
    {
        public byte ParentId;
        public Vector2 TruePosition;
        public float Distance;
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
            if (Vulture.staticEatedPlayers.Contains(id)) continue;
            var pos = db.TruePosition;
            var dist = Vector2.Distance(myPos, pos);
            if (dist > EatRange) continue;
            if (nearest == null || dist < nearest.Distance)
                nearest = new DeadBodyInfo(id, pos, dist);
        }
        return nearest;
    }

    void SendRPC(byte targetId)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        using var sender = CreateSender();
        sender.Writer.Write(targetId);
    }

    public override void ReceiveRPC(MessageReader reader)
    {
        Vulture.staticEatedPlayers.Add(reader.ReadByte());
    }
}