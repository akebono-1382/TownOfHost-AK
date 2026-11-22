using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using HarmonyLib;
using TownOfHostL.Attributes;
using TownOfHostL.Core.Modes;

namespace TownOfHostL
{
    class VoiceReader
    {
        private enum Mode { Log, Boyomi, VoiceVox }
        private static Mode ReadMode = Mode.Boyomi;
        private static bool InitFinished = false;

        private static string UrlBoyomi = "http://localhost:50080";
        // VOICEVOX は 127.0.0.1 でだけ待つ。localhost だと先に ::1 へ繋ぎに行って断られる分だけ遅れる
        private static string UrlVoiceVox = "http://127.0.0.1:50021";
        // VoiceList.txt に声の行が無い時は、VOICEVOX の声の一覧を取って使う
        private static bool AutoVoices;
        private static bool ModeSpecified;
        private static Dictionary<int, int> VoiceType = new();
        private static Dictionary<int, string> VoiceName = new();
        private static Dictionary<int, int> VoiceVolume = new();
        private static Dictionary<int, int> VoiceSpeed = new();

        public static OptionItem VoiceReaderMode;
        public static OptionItem VoiceReaderHost;
        /// <summary>読み上げを使える (声の一覧があり、設定で ON)</summary>
        public static bool Enabled => InitFinished && VoiceReaderMode != null && VoiceReaderMode.GetBool();
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
            if (VoiceType.Count == 0 && (!ModeSpecified || ReadMode == Mode.VoiceVox))
            {
                AutoVoices = true;
                ReadMode = Mode.VoiceVox;
                // 前回取った一覧で先に設定を作る (保存した声の番号が、読み込みの時に選択肢の数で切られないように)
                var cached = LoadSpeakerCache();
                if (cached.Count > 0) SetVoices(cached);
                Task.Run(FetchSpeakersLoop);
            }
        }
        public static void SetupCustomOption()
        {
            // 声が無くても設定は必ず作る。設定の数がホストと TOHL の客で違うと、客は TOHL の画面をやめてしまう
            RegisterVoiceNames();
            // 一覧がまだ無い時は仮の選択肢で作って隠し、VOICEVOX から一覧が届いたら差し替えて出す
            var names = VoiceType.Count > 0 ? VoiceName.Values.ToArray() : new[] { PlaceholderName };
            bool hidden = VoiceType.Count == 0;
            VoiceReaderMode = BooleanOptionItem.Create("VoiceReaderMode", false, TabGroup.ModMainSettings, true)
                .SetColor(Palette.CrewmateBlue)
                .SetGameMode(CustomGameMode.All)
                .SetHidden(hidden);
            // 既定では色ごとに別の声にする。自動の一覧はキャラごとに散らして並べてあるので、隣の色が同じキャラになりにくい
            VoiceReaderHost = StringOptionItem.Create("VoiceReaderHost", names, ColorKeys.Length, TabGroup.ModMainSettings, true).SetParent(VoiceReaderMode)
                .SetGameMode(CustomGameMode.All)
                .SetHidden(hidden);
            for (int i = 0; i < ColorKeys.Length; i++)
                ColorVoices[i] = StringOptionItem.Create("VoiceReader" + ColorKeys[i], names, i, TabGroup.ModMainSettings, true).SetParent(VoiceReaderMode)
                    .SetGameMode(CustomGameMode.All)
                    .SetHidden(hidden);
        }
        private const string PlaceholderName = "VOICEVOX";
        // 声の名前は訳さずそのまま出す
        private static void RegisterVoiceNames()
        {
            foreach (var voiceName in VoiceName.Values.Append(PlaceholderName))
            {
                if (Translator.translateMaps.ContainsKey(voiceName)) continue;
                Dictionary<int, string> dic = new();
                foreach (int langId in Enum.GetValues(typeof(SupportedLangs)))
                    dic[langId] = voiceName;
                Translator.translateMaps[voiceName] = dic;
            }
        }
        private static void SetVoices(List<(int id, string label)> voices)
        {
            VoiceType.Clear();
            VoiceName.Clear();
            for (int i = 0; i < voices.Count; i++)
            {
                VoiceType[i] = voices[i].id;
                VoiceName[i] = voices[i].label;
            }
            InitFinished = voices.Count > 0;
        }

        // ---- VOICEVOX の声の一覧 ----
        private static readonly string SPEAKER_CACHE_PATH = @"./TOHL_DATA/VoiceVoxSpeakers.txt";
        private static readonly HttpClient speakerClient = new() { Timeout = TimeSpan.FromSeconds(5) };
        private const int RetrySeconds = 60;
        // 裏の作業が取った一覧。設定とゲームの物はメインスレッドで触るので、受け渡しだけをここでする
        private static volatile List<(int id, string label)> fetchedSpeakers;

        private static async Task FetchSpeakersLoop()
        {
            for (int attempt = 0; ; attempt++)
            {
                var list = await FetchSpeakers();
                if (list.Count > 0)
                {
                    SaveSpeakerCache(list);
                    fetchedSpeakers = list;
                    Logger.Info($"VOICEVOX voices: {list.Count} ({UrlVoiceVox})", "VoiceReader");
                    return;
                }
                // VOICEVOX を後から立ち上げても拾えるよう、見つかるまで間を空けて取り直す
                if (attempt == 0) Logger.Info($"VOICEVOX not found ({UrlVoiceVox}), retry every {RetrySeconds}s", "VoiceReader");
                await Task.Delay(TimeSpan.FromSeconds(RetrySeconds));
            }
        }
        /// <summary>GET /speakers から話し声の型だけを取り、キャラごとの 1 番目の型・2 番目の型…の順に並べる</summary>
        private static async Task<List<(int id, string label)>> FetchSpeakers()
        {
            var result = new List<(int, string)>();
            try
            {
                using var resp = await speakerClient.GetAsync($"{UrlVoiceVox}/speakers");
                if (!resp.IsSuccessStatusCode) return result;
                var root = JsonNode.Parse(await resp.Content.ReadAsStringAsync())?.AsArray();
                if (root == null) return result;

                var perSpeaker = new List<List<(int, string)>>();
                foreach (var speaker in root)
                {
                    string name = speaker?["name"]?.GetValue<string>() ?? "";
                    var styles = speaker?["styles"]?.AsArray();
                    if (styles == null) continue;
                    var list = new List<(int, string)>();
                    foreach (var style in styles)
                    {
                        // 歌の型は読み上げに使えない。古いエンジンは type を持たないので話し声として扱う
                        string type = style?["type"]?.GetValue<string>() ?? "talk";
                        if (type != "talk") continue;
                        int id = style?["id"]?.GetValue<int>() ?? -1;
                        if (id < 0) continue;
                        list.Add((id, $"{name}（{style?["name"]?.GetValue<string>() ?? ""}）"));
                    }
                    if (list.Count > 0) perSpeaker.Add(list);
                }
                for (int round = 0; ; round++)
                {
                    bool any = false;
                    foreach (var list in perSpeaker)
                    {
                        if (round >= list.Count) continue;
                        result.Add(list[round]);
                        any = true;
                    }
                    if (!any) break;
                }
            }
            catch (Exception) { result.Clear(); }
            return result;
        }
        private static List<(int id, string label)> LoadSpeakerCache()
        {
            var result = new List<(int, string)>();
            try
            {
                if (!File.Exists(SPEAKER_CACHE_PATH)) return result;
                foreach (var line in File.ReadAllLines(SPEAKER_CACHE_PATH, Encoding.UTF8))
                {
                    int comma = line.IndexOf(',');
                    if (comma <= 0 || !int.TryParse(line[..comma], out int id)) continue;
                    result.Add((id, line[(comma + 1)..]));
                }
            }
            catch (Exception ex) { Logger.Exception(ex, "VoiceReader"); }
            return result;
        }
        // 次の起動で先に使う控え。VoiceList.txt に書く声の番号を調べるのにも使える
        private static void SaveSpeakerCache(List<(int id, string label)> list)
        {
            try
            {
                Directory.CreateDirectory("TOHL_DATA");
                File.WriteAllLines(SPEAKER_CACHE_PATH, list.Select(v => $"{v.id},{v.label}"), Encoding.UTF8);
            }
            catch (Exception ex) { Logger.Exception(ex, "VoiceReader"); }
        }
        /// <summary>届いた一覧を設定の選択肢へ入れる。保存してある声の番号はそのまま残す</summary>
        private static void ApplyFetchedSpeakers()
        {
            var list = fetchedSpeakers;
            fetchedSpeakers = null;
            if (list == null) return;
            bool same = list.Count == VoiceType.Count;
            for (int i = 0; same && i < list.Count; i++)
                same = VoiceType[i] == list[i].id && VoiceName[i] == list[i].label;
            if (same) return;

            SetVoices(list);
            if (VoiceReaderMode == null) return;   // 設定はまだ作られていない。作る時にこの一覧を使う
            RegisterVoiceNames();
            var names = VoiceName.Values.ToArray();
            VoiceReaderMode.SetHidden(false);
            foreach (var item in ColorVoices.Append(VoiceReaderHost))
            {
                if (item is not StringOptionItem opt) continue;
                opt.Selections = names;
                opt.Rule = (0, names.Length - 1, 1);
                opt.SetHidden(false);
            }
        }
        [PatchBundle(PatchBundles.Core)]
        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]
        private static class SpeakerPollPatch
        {
            public static void Postfix()
            {
                if (fetchedSpeakers != null) ApplyFetchedSpeakers();
            }
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
                        ModeSpecified = true;
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
            foreach (var item in ColorVoices) item?.SetValue(item.DefaultValue);
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
            return VoiceType.GetValueOrDefault(voiceTypeIdx, 0);
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
        private static readonly Queue<(string url, string text, int voiceType, int volume, int speed)> queue = new();
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
                queue.Enqueue((url, chat, voiceType, volume, speed));
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
                (string url, string text, int voiceType, int volume, int speed) item;
                lock (queue)
                {
                    if (queue.Count == 0) { working = false; return; }
                    item = queue.Dequeue();
                }
                var wav = await Synthesize(item.url, item.text, item.voiceType, item.volume, item.speed);
                if (wav == null || wav.Length == 0) continue;
                // SND_ASYNC を付けないので、鳴り終わるまでここで待つ (次の声と重ならない)
                try { PlaySound(wav, IntPtr.Zero, PlaySoundFlags.SND_MEMORY); }
                catch (Exception e) { Logger.Info($"playError, exception: {e}", "VoiceVoxHTTPRequest"); }
            }
        }
        // 音量と速さは声の一覧の 3・4 列目 (棒読みちゃんと同じく 100 が標準)。-1 はエンジンの既定のまま
        private static async System.Threading.Tasks.Task<byte[]> Synthesize(string url, string text, int voiceType, int volume, int speed)
        {
            try
            {
                using var queryRequest = new HttpRequestMessage(HttpMethod.Post, $"{url}/audio_query?text={text}&speaker={voiceType}");
                queryRequest.Headers.TryAddWithoutValidation("accept", "application/json");
                queryRequest.Content = new StringContent("");
                using var queryResponse = await GetHttpInstance().SendAsync(queryRequest);
                if (!queryResponse.IsSuccessStatusCode) return null;
                var query = await queryResponse.Content.ReadAsStringAsync();
                if (volume >= 0 || speed > 0)
                {
                    var node = JsonNode.Parse(query);
                    if (node != null)
                    {
                        if (volume >= 0) node["volumeScale"] = volume / 100f;
                        if (speed > 0) node["speedScale"] = speed / 100f;
                        query = node.ToJsonString();
                    }
                }

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
}
