using AmongUs.GameOptions;
using HarmonyLib;

namespace TownOfHost
{
    [HarmonyPatch(typeof(GameOptionsManager), nameof(GameOptionsManager.SwitchGameMode))]
    class SwitchGameModePatch
    {
        public static void Postfix(GameModes gameMode)
        {
            GameOptionsManager.Instance.currentGameMode = GameModes.Normal;
            Main.HnSFlag = gameMode is GameModes.HideNSeek or GameModes.SeekFools;

            //これ作成画面で無効化食らうから良い対処法見つかるまで封印
            /*if (gameMode == GameModes.HideNSeek)
            {
                Main.HnSFlag = true;
                /*if (!DebugModeManager.AmDebugger)
                {
                    ErrorText.Instance.HnSFlag = true;
                    ErrorText.Instance.AddError(ErrorCode.HnsUnload);
                    Harmony.UnpatchAll();
                    Main.Instance.Unload();
                }
            }*/
        }
    }
}