using System.Collections.Generic;
using System.Text;
using TownOfHostL.Attributes;

namespace TownOfHostL.Modules;

/// <summary>
/// 1 人宛ての送信で、宛先の接続 (クライアント ID) が引けなかった時の記録。
/// 宛先 -1 は全員宛ての意味なので、引けなかった送信は送らずに捨て、ここで原因を追える情報を残す。
/// 同じ人・同じ送信の記録は試合ごとに 1 回だけ。
/// </summary>
public static class NoClientWarning
{
    private static readonly HashSet<(byte, string)> reported = new();

    [GameModuleInitializer]
    public static void Init() => reported.Clear();

    public static void Report(PlayerControl pc, string what)
    {
        byte id = pc == null ? byte.MaxValue : pc.PlayerId;
        if (!reported.Add((id, what))) return;
        var caller = new System.Diagnostics.StackFrame(2, false).GetMethod();
        var sb = new StringBuilder();
        sb.Append($"宛先の接続が見つからないので送りません: {what} 宛先={pc?.Data?.PlayerName}({id}) ");
        sb.Append($"Data.ClientId={pc?.Data?.ClientId} 切断={pc?.Data?.Disconnected} 呼び出し元={caller?.DeclaringType?.Name}.{caller?.Name} 接続:");
        try
        {
            var clients = AmongUsClient.Instance?.allClients;
            if (clients != null)
                foreach (var cd in clients)
                    sb.Append($" [{cd?.Id} {(cd?.Character != null ? cd.Character.PlayerId.ToString() : "-")}]");
        }
        catch { }
        Logger.Warn(sb.ToString(), "NoClient");
    }
}
