using HarmonyLib;

namespace TownOfHostL.Patches;

// 接続の時に「mod 入りのホスト」と名乗り、手元でもそう振る舞う。
// 公式サーバーは接続時の名乗りを覚えていて、mod 入りのホストなら PlayerInfo をホストが作り、そうでなければサーバーが作る。
// 名乗りと手元の振る舞いが食い違うと、ホストが自分のプレイヤーを作らなかったり、作ってはいけない PlayerInfo を作って切断される。
// そのため、かくれんぼの間に TOHL を休ませても外さないよう、属性ではなく常駐の Harmony (Core.VanillaHideAndSeek) から当てる。
public static class ConstantsPatch
{
    public static void Install(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Constants), nameof(Constants.GetBroadcastVersion)),
            postfix: new HarmonyMethod(typeof(ConstantsPatch), nameof(GetBroadcastVersionPostfix)));
        harmony.Patch(AccessTools.Method(typeof(Constants), nameof(Constants.IsVersionModded)),
            prefix: new HarmonyMethod(typeof(ConstantsPatch), nameof(IsVersionModdedPrefix)));
    }

    // 版番号の下 2 桁を 50 で割った余りが 25〜49 なら mod の部屋として扱われる。
    // オンライン (公式・独自のサーバー) へ出る時だけ付ける (フリープレイ・ローカルに付けると手元のサーバーが接続を受けず、黒い画面のまま止まった)。
    // 本編の版がもう 25〜49 の帯にある時は足さない (足すと次の版の帯へはみ出して部屋を立てられない)。
    // More-Roles-Plus (src/Net/ModdedVersion.cs) と同じ決め方。
    private static void GetBroadcastVersionPostfix(ref int __result)
    {
        var client = AmongUsClient.Instance;
        if (client == null || client.NetworkMode != NetworkModes.OnlineGame) return;
        if (__result % 50 < 25) __result += 25;
    }

    private static bool IsVersionModdedPrefix(ref bool __result)
    {
        __result = true;
        return false;
    }
}
