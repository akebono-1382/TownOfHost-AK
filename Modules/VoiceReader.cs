using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Runtime.InteropServices;
using TownOfHostL.Attributes;

namespace TownOfHostL
{
    class VoiceReader
    {
        private enum Mode { Log, Boyomi, VoiceVox }
        private static Mode ReadMode = Mode.Boyomi;
        private static bool InitFinished = false;

        private static string UrlBoyomi = "http://localhost:50080";
        private static string UrlVoiceVox = "http://localhost:50021";
        private static Dictionary<int, int> VoiceType = new();
        private static Dictionary<int, string> VoiceName = new();
        private static Dictionary<int, int> VoiceVolume = new();
        private static Dictionary<int, int> VoiceSpeed = new();

        public static OptionItem VoiceReaderMode;
        public static OptionItem VoiceReaderHost;
        // 本編の ColorId の順。設定の鍵は "VoiceReader" + この名前
        private static readonly string[] ColorKeys =
        {
            "Red", "Blue", "Green", "Pink", "Orange", "Yellow", "Black", "White", "Purple",
            "Brown", "Cyan", "Lime", "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral",
        };
        private static readonly OptionItem[] ColorVoices = new OptionItem[ColorKeys.Length];

        [PluginModuleInitializer]
        public static void Init()
        {
            LoadVoiceList();
        }
        public static void SetupCustomOption()
        {
            if (!InitFinished) return;
            if (VoiceType.Count == 0) return;

            VoiceReaderMode = BooleanOptionItem.Create("VoiceReaderMode", false, TabGroup.ModMainSettings, true)
                .SetColor(Palette.CrewmateBlue)
                .SetGameMode(CustomGameMode.All);
            VoiceReaderHost = StringOptionItem.Create("VoiceReaderHost", VoiceName.Values.ToArray(), 0, TabGroup.ModMainSettings, true).SetParent(VoiceReaderMode)
                .SetGameMode(CustomGameMode.All);
            for (int i = 0; i < ColorKeys.Length; i++)
                ColorVoices[i] = StringOptionItem.Create("VoiceReader" + ColorKeys[i], VoiceName.Values.ToArray(), 0, TabGroup.ModMainSettings, true).SetParent(VoiceReaderMode)
                    .SetGameMode(CustomGameMode.All);
        }
        private static readonly string VOICE_LIST_PATH = @"./TOHL_DATA/VoiceList.txt";
        public static void LoadVoiceList()
        {
            try
            {
                Directory.CreateDirectory("TOHL_DATA");
                if (!File.Exists(VOICE_LIST_PATH))
                {
                    Logger.Info($"CreateListText", "LoadVoiceList");
                    return;
                }
                using StreamReader sr = new(VOICE_LIST_PATH);
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line == null) continue;
                    line = line.Trim();
                    if (line == "") continue;
                    if (line.ToLower().StartsWith("mode=") && line.Length > 5)
                    {
                        line = line.Substring(5, line.Length - 5);
                        if (line.ToLower() == "voicevox") ReadMode = Mode.VoiceVox;
                        Logger.Info($"modeSet", "LoadVoiceList");
                    }
                    else if (line.ToLower().StartsWith("url=") && line.Length > 4)
                    {
                        line = line.Substring(4, line.Length - 4);
                        UrlBoyomi = line;
                        UrlVoiceVox = line;
                        Logger.Info($"urlSet", "LoadVoiceList");
                    }
                    else if (line.ToLower().StartsWith("voice=") && line.Length > 6)
                    {
                        line = line.Substring(6, line.Length - 6);
                        var lines = line.Split(",");
                        if (lines.Length < 2) continue;
                        if (!int.TryParse(lines[0], out int voiceType)) continue;
                        {
                            var voiceNo = VoiceType.Count;          //voiceNoは0始まりインデックス
                            var voiceName = lines[1];
                            VoiceType.TryAdd(voiceNo, voiceType);
                            VoiceName.TryAdd(voiceNo, lines[1]);

                            Dictionary<int, string> dic = new();
                            foreach (int langId in Enum.GetValues(typeof(SupportedLangs)))
                                dic[langId] = voiceName;
                            Translator.translateMaps.TryAdd(voiceName, dic);

                            if (lines.Length >= 3 && int.TryParse(lines[2], out int volume))
                                VoiceVolume.TryAdd(voiceNo, volume);
                            if (lines.Length >= 4 && int.TryParse(lines[3], out int speed))
                                VoiceSpeed.TryAdd(voiceNo, speed);
                            Logger.Info($"setVoiceNo", "LoadVoiceList");
                        }
                    }
                }
                InitFinished = true;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LoadVoiceList");
            }
        }
        public static void Read(string chat, PlayerControl speaker, string tag)
        {
            if (!InitFinished) return;
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return;
            if (chat == null || chat.TrimEnd() == "") return;

            if (chat.TrimStart().StartsWith("/")) return;

            if (tag == "SendChatAlive" && speaker != null)
                ReadChat(chat, ResolveVoiceIdx(speaker));
        }
        public static void ReadHost(string chat, string tag)
        {
            if (!InitFinished) return;
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return;
            if (chat == null || chat.TrimEnd() == "") return;

            if (chat.TrimStart().StartsWith("/")) return;

            if (tag == "SendChatHost" && PlayerControl.LocalPlayer != null)
                ReadChat(chat, ResolveVoiceIdx(PlayerControl.LocalPlayer));
        }
        public static string SetVoiceNo(int colorId, int voiceNo)
        {
            if (!InitFinished) return "";
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return "";
            if (voiceNo < 0 || voiceNo >= VoiceType.Count) return "";
            var item = GetOptionItem(colorId);
            if (item == null) return "";
            item.SetValue(voiceNo);
            Logger.Info($"SetVoice color {colorId} => voiceNo: {voiceNo}, max: {VoiceType.Count - 1}", "SetVoiceNo");
            return VoiceName[voiceNo];
        }
        public static string SetHostVoiceNo(int voiceNo)
        {
            if (!InitFinished) return "";
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return "";
            if (voiceNo < 0 || voiceNo >= VoiceType.Count) return "";
            VoiceReaderHost.SetValue(voiceNo);
            Logger.Info($"SetVoice Host => voiceNo: {voiceNo}, max: {VoiceType.Count - 1}", "SetVoiceNo");
            return VoiceName[voiceNo];
        }
        public static string GetVoiceName(int colorId)
        {
            if (!InitFinished) return "";
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return "";
            var item = GetOptionItem(colorId);
            if (item == null) return "";
            return VoiceName.GetValueOrDefault(item.GetInt(), "");
        }
        public static void ResetVoiceNo()
        {
            if (!InitFinished) return;
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return;
            foreach (var item in ColorVoices) item?.SetValue(0);
        }
        public static void SetRandomVoiceNo()
        {
            if (!InitFinished) return;
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return;
            List<int> list = new(VoiceType.Keys);
            var exclusive = list.Count >= ColorVoices.Length;       //色の数だけ声があるなら重複なし
            foreach (var item in ColorVoices) item?.SetValue(GetRandIndex(list, exclusive));
        }
        private static int GetRandIndex(List<int> list, bool exclusive = false)
        {
            if (list.Count == 0) return 0;
            var rIdx = IRandom.Instance.Next(list.Count);
            var voiceNo = list[rIdx];
            if (exclusive) list.RemoveAt(rIdx);
            return voiceNo;
        }
        public static string GetVoiceIdxMsg()
        {
            if (!InitFinished) return "";
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return "";
            StringBuilder sb = new();
            sb.Append($"声の設定（書式 /vo [No])\n[No一覧]\n");
            foreach (var voiceNo in VoiceName.Keys.ToArray())
            {
                sb.Append($"{voiceNo}: {VoiceName[voiceNo]}\n");
            }
            return sb.ToString();
        }
        private static OptionItem GetOptionItem(int colorId)
            => colorId >= 0 && colorId < ColorVoices.Length ? ColorVoices[colorId] : null;
        // 声は人ではなく見た目に付ける。変身・迷彩・見た目の入れ替えの間は、その見た目の持ち主の声で読み、声で正体が分からないようにする
        private static int ResolveVoiceIdx(PlayerControl speaker)
        {
            var owner = ApparentOwner(speaker, out int apparentColor);
            // 誰の物でもない見た目 (持ち主のいない迷彩の色など) は、その色の声。着ている人が誰でも同じ声になる
            if (owner == null) return GetVoiceIdx(apparentColor);
            if (owner.AmOwner) return VoiceReaderHost.GetInt();
            return GetVoiceIdx(OwnColor(owner));
        }
        /// <summary>その人の本来の色。試合中は開始時に控えた外見の色 (迷彩などは既定の外見も書き換えるため)</summary>
        public static int OwnColor(PlayerControl pc)
            => GameStates.IsInGame && Camouflage.PlayerSkins.TryGetValue(pc.PlayerId, out var outfit) ? outfit.ColorId : pc.Data.DefaultOutfit.ColorId;
        private static PlayerControl ApparentOwner(PlayerControl speaker, out int apparentColor)
        {
            apparentColor = OwnColor(speaker);
            if (!GameStates.IsInGame) return speaker;
            int current = speaker.CurrentOutfit?.ColorId ?? apparentColor;
            if (current == apparentColor) return speaker;
            apparentColor = current;
            PlayerControl found = null;
            foreach (var pc in Main.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || OwnColor(pc) != current) continue;
                // 本来の色が重なる時 (色をそろえる遊び方など) は持ち主を決められないので、本人の声のまま
                if (found != null) return speaker;
                found = pc;
            }
            return found;
        }
        private static void ReadChat(string chat, int voiceNo)
        {
            if (!InitFinished) return;
            if (VoiceReaderMode == null || !VoiceReaderMode.GetBool()) return;
            var voiceType = GetVoiceNo(voiceNo);
            if (!VoiceVolume.TryGetValue(voiceNo, out var volume)) volume = -1;
            if (!VoiceSpeed.TryGetValue(voiceNo, out var speed)) speed = -1;
            chat = Uri.EscapeDataString(chat);

            switch (ReadMode)
            {
                case Mode.VoiceVox:
                    VoiceVoxChatSender.ReadChat(UrlVoiceVox, chat, voiceType, volume, speed);
                    break;
                default:
                    BoyomiChatSender.ReadChat(UrlBoyomi, chat, voiceType, volume, speed);
                    break;
            }
        }
        private static int GetVoiceNo(int voiceTypeIdx)
        {
            try
            {
                var value = VoiceType.Values.ToArray().GetValue(voiceTypeIdx);
                return (int)value;
            }
            catch
            {
                return 0;
            }
        }
        private static int GetVoiceIdx(int colorId)
        {
            var item = GetOptionItem(colorId);
            if (item == null) return 0;
            return item.GetInt();
        }
    }
    class BoyomiChatSender
    {
        private static HttpClient client = null;
        private static HttpClient GetHttpInstance()
        {
            if (client == null) client = new();
            return client;
        }
        public static void ReadChat(string url, string chat, int voiceType, int volume = -1, int speed = -1)
        {
            HTTPRequest(url, chat, voiceType, volume, speed);
        }
        private static async void HTTPRequest(string url, string text, int voiceType, int volume = -1, int speed = -1)
        {
            var send = $"{url}/talk?text={text}&voice={voiceType}&volume={volume}&speed={speed}";
            try
            {
                var result = await GetHttpInstance().GetAsync(send);
                //Logger.Info($"send: {send}, result: {result}", $"VoiceHTTPRequest");
                Logger.Info($"ChatRead voiceType: {voiceType}, text: {text}, volume: {volume}, speed: {speed}", $"VoiceHTTPRequest");
            }
            catch (Exception ex)
            {
                Logger.Info($"sendTalkError: {send}, exception: {ex}", $"VoiceHTTPRequest");
            }
        }
    }
    class VoiceVoxChatSender
    {
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        private static extern bool PlaySound(byte[] sound, IntPtr hMod, PlaySoundFlags flags);

        [System.Flags]
        private enum PlaySoundFlags : int
        { SND_MEMORY = 0x0004, }

        // 発言が続いても前の声を遮らないよう、1 本の作業で順に合成して鳴らす。溜まりすぎたら古い物から捨てる
        private const int MaxQueue = 10;
        private static readonly Queue<(string url, string text, int voiceType)> queue = new();
        private static bool working;

        private static HttpClient client = null;
        private static HttpClient GetHttpInstance()
        {
            if (client == null) client = new();
            return client;
        }
        public static void ReadChat(string url, string chat, int voiceType, int volume = -1, int speed = -1)
        {
            lock (queue)
            {
                queue.Enqueue((url, chat, voiceType));
                while (queue.Count > MaxQueue) queue.Dequeue();
                if (working) return;
                working = true;
            }
            System.Threading.Tasks.Task.Run(Work);
        }
        private static async System.Threading.Tasks.Task Work()
        {
            while (true)
            {
                (string url, string text, int voiceType) item;
                lock (queue)
                {
                    if (queue.Count == 0) { working = false; return; }
                    item = queue.Dequeue();
                }
                var wav = await Synthesize(item.url, item.text, item.voiceType);
                if (wav == null || wav.Length == 0) continue;
                // SND_ASYNC を付けないので、鳴り終わるまでここで待つ (次の声と重ならない)
                try { PlaySound(wav, IntPtr.Zero, PlaySoundFlags.SND_MEMORY); }
                catch (Exception e) { Logger.Info($"playError, exception: {e}", "VoiceVoxHTTPRequest"); }
            }
        }
        private static async System.Threading.Tasks.Task<byte[]> Synthesize(string url, string text, int voiceType)
        {
            try
            {
                using var queryRequest = new HttpRequestMessage(HttpMethod.Post, $"{url}/audio_query?text={text}&speaker={voiceType}");
                queryRequest.Headers.TryAddWithoutValidation("accept", "application/json");
                queryRequest.Content = new StringContent("");
                using var queryResponse = await GetHttpInstance().SendAsync(queryRequest);
                if (!queryResponse.IsSuccessStatusCode) return null;
                var query = await queryResponse.Content.ReadAsStringAsync();

                using var synthRequest = new HttpRequestMessage(HttpMethod.Post, $"{url}/synthesis?speaker={voiceType}&enable_interrogative_upspeak=true");
                synthRequest.Headers.TryAddWithoutValidation("accept", "audio/wav");
                synthRequest.Content = new StringContent(query, Encoding.UTF8, "application/json");
                using var synthResponse = await GetHttpInstance().SendAsync(synthRequest);
                if (!synthResponse.IsSuccessStatusCode) return null;
                return await synthResponse.Content.ReadAsByteArrayAsync();
            }
            catch (Exception e)
            {
                Logger.Info($"synthesisError: speaker {voiceType}, exception: {e}", "VoiceVoxHTTPRequest");
                return null;
            }
        }
    }
    //チャット用ログ
    class ChatLogger
    {
        private static ChatLogger singleton = null;

        private readonly string logFilePath = null;
        private readonly object lockObj = new();
        private StreamWriter stream = null;
        public bool close = true;

        private static ChatLogger GetInstance()
        {
            if (singleton == null) singleton = new();
            return singleton;
        }
        public ChatLogger()
        {
            this.logFilePath = @"D:\Program Files\Steam\steamapps\common\Among Us\BepInEx\" + @"ChatLog.log";
        }
        private void CreateLogfile(FileInfo logFile)
        {
            if (!Directory.Exists(logFile.DirectoryName))
            {
                Directory.CreateDirectory(logFile.DirectoryName);
            }

            this.stream = new StreamWriter(logFile.FullName, true, Encoding.UTF8)
            {
                AutoFlush = true
            };
        }
        public static void Log(string msg, string tag, bool fileClose = false)
        {
            GetInstance().OutputLog(msg, tag, fileClose);
        }
        private void OutputLog(string msg, string tag, bool fileClose = false)
        {
            int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;

            string t = DateTime.Now.ToString("HH:mm:ss");
            string log_text = $"[{t}][{tag}]{msg}";

            if (close) { CreateLogfile(new FileInfo(logFilePath)); close = false; }

            lock (this.lockObj)
            {
                this.stream.WriteLine(log_text);
                if (fileClose) { this.stream.Close(); close = true; }
            }
        }
    }
}
