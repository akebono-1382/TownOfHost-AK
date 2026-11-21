using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using UnityEngine;

namespace TownOfHostL.SettingsUI;

// ゲームの頁の右の欄に出す、本編の設定の意味を見せる動く図。字は使わない。
// 移動速度 = 本編で撮った 1 倍の歩きの映像を、値の倍率で再生する / 視界 = 本編で撮った値ごとの止め絵 (いちばん近い値) /
// キルのクールダウン = 本編のキルボタンが暗くなって秒を数え (10 秒を 1 秒に縮める)、明けたら本編で撮ったキルの映像を流す /
// キルの距離 = 本編で撮った、相手が届く距離のすぐ内と外に立つ止め絵を交互に出し、届く範囲の円 (撮った時の縮尺どおり) と本編のキルボタンの点き消えを重ねる。
// 映像が読めない時は自前の図: 移動速度 = 基準 (1 倍・灰) と今の速さのクルーが並んで走る / 視界 = 暗い中のクルーとインポスターの明かりの大きさ (半径は値の平方根) /
// キルのクールダウン = インポスターの周りの目盛りが埋まる速さ (10 秒で 1 周を 1 秒に縮めて見せる)。
// どの図を出すかは、最後に当てた・変えた本編の設定で決める (かくれんぼの設定など図の無い項目では変えない)。
// 図はすべて作った時に用意しておき、出し入れだけをする (後から作ると描く順が付かないため)。
internal sealed class GamePreview : Widget
{
    private enum Scene { Speed, Vision, KillCooldown, KillRange }

    private static readonly Color Panel = new(0.06f, 0.07f, 0.1f, 1f);
    private static readonly Color Lane = new(0.22f, 0.24f, 0.3f, 1f);
    private static readonly Color RefCrew = new(0.5f, 0.52f, 0.58f, 1f);
    private static readonly Color CrewColor = new(0.35f, 0.9f, 1f, 1f);
    private static readonly Color ImpColor = new(1f, 0.25f, 0.25f, 1f);
    private static readonly Color LightColor = new(1f, 0.95f, 0.75f, 1f);
    private static readonly Color DotOff = new(0.25f, 0.26f, 0.32f, 1f);

    private const int Dots = 24;
    private const float CrewH = 0.34f;
    private const float RefCross = 2.2f;     // 1 倍の速さで欄を横切る秒数
    private const float CooldownScale = 0.1f; // クールダウンの 1 秒を何秒で見せるか
    // キルの距離の映像: 縦 6 マスを 144 画素で撮った (1 マス = 24 画素)。止め絵は値ごとに 2 枚 (内・外)
    private const float RangePxPerUnit = 24f;
    private const float RangeIn = 1.4f, RangeOut = 1.0f; // 内・外の絵を出す秒数
    private const float RangeCrewH = 0.2f;

    // 最後に選んだ図 (頁を作り直しても残す)
    private static Scene _scene = Scene.Speed;
    private static string _focusKey;   // 図を出している設定 (null = まだ何も当てていない)
    private static bool _focusImpostor;

    public readonly float Width, Height;

    private SpriteRenderer _back;
    private readonly List<(SpriteRenderer r, int order, float alpha)> _parts = new();
    private int _appliedBase = int.MinValue;
    private GameObject _speedRoot, _visionRoot, _killRoot;
    private GameObject _speedDrawn, _visionDrawn;
    private SpriteRenderer _speedFoot, _visionFoot;
    private float _speedT;
    private int _speedFrame = -1;
    private float _crewValue = 1f, _impValue = 1.5f;

    private Transform _refCrew, _curCrew;
    private SpriteRenderer _crewGlow, _crewLight, _crewBody, _impGlow, _impLight, _impBody;
    private readonly SpriteRenderer[] _dots = new SpriteRenderer[Dots];
    private SpriteRenderer _victim;
    private GameObject _killDrawn;
    private SpriteRenderer _killFoot, _killBtn;
    private Label _killNum;
    private int _killShown = -2, _killFrame = -1;
    private float _killT;
    private static readonly int PercentId = Shader.PropertyToID("_Percent");
    private static Material _killMat;
    private static readonly int DesatId = Shader.PropertyToID("_Desat");
    private float _killPercent = -1f;

    private GameObject _rangeRoot;
    private SpriteRenderer _rangeFoot, _rangeDisk, _rangeRing, _rangeBtn, _rangeHost, _rangeTarget;
    private static Material _rangeMat;
    private int _rangeIndex = 1, _rangeFrame = -1;
    private float _rangeT;

    private float _speed = 1f;
    private float _crewR, _impR, _crewTarget, _impTarget;
    private float _cooldown = 25f;
    private float _last = -1f;
    private float _lightMax = 5f;
    private const float VisionCrewH = 0.2f;

    private float LightRadius(float v) => Math.Min(Height * 0.46f, Width * 0.25f) * MathF.Sqrt(Math.Max(v, 0f) / _lightMax);

    public GamePreview(float width, float height)
    {
        Width = width;
        Height = height;
    }

    // ---- どの設定に図があるか ----

    private static Scene? SceneOf(MenuOpt opt) => opt is VanillaMenuOpt v ? v.SettingTitle switch
    {
        StringNames.GamePlayerSpeed => Scene.Speed,
        StringNames.GameCrewLight or StringNames.GameImpostorLight => Scene.Vision,
        StringNames.GameKillCooldown => Scene.KillCooldown,
        StringNames.GameKillDistance => Scene.KillRange,
        _ => null,
    } : null;

    public static bool Has(MenuOpt opt) => SceneOf(opt) != null;

    // 今の図の元の設定か (まだ何も当てていなければ、最初に出す図の設定)
    public static bool Shows(MenuOpt opt) => _focusKey != null ? opt.Key == _focusKey : SceneOf(opt) == _scene;

    private static VanillaMenuOpt Find(StringNames title)
    {
        foreach (var sec in VanillaGame.All)
            foreach (var o in sec.Opts)
                if (o.SettingTitle == title) return o;
        return null;
    }

    private static float Value(VanillaMenuOpt o, float fallback) => o?.ValueAt(o.Index) is double d ? (float)d : fallback;
    private static float Max(VanillaMenuOpt o, float fallback) => o != null && o.Count > 0 && o.ValueAt(o.Count - 1) is double d ? (float)d : fallback;

    // 当てた・変えた設定の図にする (図の無い設定なら今の図のまま)
    public void Focus(MenuOpt opt)
    {
        var s = SceneOf(opt);
        if (s == null) return;
        bool changed = s != _scene;
        _scene = s.Value;
        _focusKey = opt.Key;
        if (opt is VanillaMenuOpt v) _focusImpostor = v.SettingTitle == StringNames.GameImpostorLight;
        Refresh();
        if (changed) ShowScene();
    }

    // 値を読み直す (変えた時・外で変わった時)。毎フレームは読まない
    public void Refresh()
    {
        _speed = Math.Max(Value(Find(StringNames.GamePlayerSpeed), 1f), 0.05f);
        var crew = Find(StringNames.GameCrewLight);
        var imp = Find(StringNames.GameImpostorLight);
        // 明かりの半径は値の平方根に比べる (最大でも欄に入り、小さい値でもクルーより大きく見えるように。段ごとの差は残る)
        _lightMax = Math.Max(Math.Max(Max(crew, 5f), Max(imp, 5f)), 0.01f);
        _crewValue = Value(crew, 1f);
        _impValue = Value(imp, 1.5f);
        _crewTarget = LightRadius(_crewValue);
        _impTarget = LightRadius(_impValue);
        _cooldown = Value(Find(StringNames.GameKillCooldown), 25f);
        var dist = Find(StringNames.GameKillDistance);
        _rangeIndex = dist != null ? Math.Clamp(dist.Index, 0, NormalGameOptionsV12.KillDistances.Length - 1) : 1;
        _rangeFrame = -1;
        LayoutRange();
        ApplyFocusAlpha();
        ShowVisionStill();
    }

    // ---- 映像 ----

    // 映像の欄 (幅いっぱい・縦横比は映像のまま)。映像の無い時は隠す
    private SpriteRenderer FootagePart(Transform parent)
    {
        var sr = Part(parent, "Footage", null, Color.white, 1);
        float w = Width - 0.08f;
        sr.transform.localScale = new Vector3(w, w, 1f);
        sr.gameObject.SetActive(false);
        return sr;
    }

    // 今の図に映像があれば出し、自前の図を隠す
    private void UseFootage()
    {
        Footage.KeepOnly(_scene switch { Scene.Speed => "speed", Scene.Vision => "vision", Scene.KillRange => "range", _ => "kill" });
        // 捨てた映像のコマを握ったままにしない (次に出す時に今の映像のコマを入れ直す)
        _speedFrame = _killFrame = _rangeFrame = -1;
        var range = _scene == Scene.KillRange ? Footage.Get("range") : null;
        bool hasRange = range != null && _rangeFoot;
        if (_rangeFoot) _rangeFoot.gameObject.SetActive(hasRange);
        if (_rangeHost) _rangeHost.gameObject.SetActive(!hasRange);
        if (_rangeTarget) _rangeTarget.gameObject.SetActive(!hasRange);
        var speed = _scene == Scene.Speed ? Footage.Get("speed") : null;
        bool hasSpeed = speed != null && _speedFoot;
        if (_speedFoot) _speedFoot.gameObject.SetActive(hasSpeed);
        if (_speedDrawn) _speedDrawn.SetActive(!hasSpeed);
        if (hasSpeed && _speedFrame < 0) { _speedFrame = 0; _speedFoot.sprite = speed.Frame(0); }

        var kill = _scene == Scene.KillCooldown ? Footage.Get("kill") : null;
        // 本編のキルボタンを借りられない時 (ロビーの外で頁を組んだ時など) は、待ち時間を見せられないので自前の図にする
        bool hasKill = kill != null && _killFoot && _killBtn;
        if (_killFoot) _killFoot.gameObject.SetActive(hasKill);
        if (_killBtn) _killBtn.gameObject.SetActive(hasKill);
        if (_killNum?.Built) _killNum.Built.gameObject.SetActive(hasKill);
        if (_killDrawn) _killDrawn.SetActive(!hasKill);
        if (hasKill && _killFrame < 0) { _killFrame = 0; _killFoot.sprite = kill.Frame(0); }

        var vision = _scene == Scene.Vision ? Footage.Get("vision") : null;
        bool hasVision = vision != null && _visionFoot;
        if (_visionFoot) _visionFoot.gameObject.SetActive(hasVision);
        if (_visionDrawn) _visionDrawn.SetActive(!hasVision);
        ShowVisionStill();
    }

    // 視界の止め絵 = 当てた方 (クルーかインポスター) の値にいちばん近い値の絵
    private void ShowVisionStill()
    {
        if (_scene != Scene.Vision || !_visionFoot) return;
        var vision = Footage.Get("vision");
        if (vision == null) return;
        _visionFoot.sprite = vision.Frame(vision.Nearest(_focusImpostor ? _impValue : _crewValue));
    }

    // ---- 組み立て ----

    public override GameObject Build(Transform parent, BuildCtx ctx, float maxWidth, out Vector2 size)
    {
        var go = Node("GamePreview", parent, ctx.Layer);
        size = new Vector2(Width, Height);
        _parts.Clear();
        _appliedBase = int.MinValue;

        _back = Node(UiScreen.BackName, go.transform, ctx.Layer).AddComponent<SpriteRenderer>();
        _back.sprite = MenuArt.Round(0.1f);
        _back.drawMode = SpriteDrawMode.Sliced;
        _back.size = size;
        _back.color = Panel;

        var crew = CrewMeter.CrewSprite();
        var circle = UiAssets.Circle;
        int layer = ctx.Layer;

        // 移動速度: 上の段 = 1 倍 (灰)・下の段 = 今の速さ
        _speedRoot = Node("Speed", go.transform, layer);
        _speedFoot = FootagePart(_speedRoot.transform);
        _speedDrawn = Node("Drawn", _speedRoot.transform, layer);
        float laneY = Height * 0.22f;
        foreach (float y in new[] { laneY, -laneY })
        {
            var line = Part(_speedDrawn.transform, "Lane", MenuArt.Round(0.015f), Lane, 1);
            line.drawMode = SpriteDrawMode.Sliced;
            line.size = new Vector2(Width - 0.2f, 0.03f);
            line.transform.localPosition = new Vector3(0f, y - CrewH / 2f, 0f);
        }
        _refCrew = Figure(_speedDrawn.transform, crew, RefCrew, laneY, 3).transform;
        _curCrew = Figure(_speedDrawn.transform, crew, CrewColor, -laneY, 3).transform;

        // 視界: 左 = クルー・右 = インポスター
        _visionRoot = Node("Vision", go.transform, layer);
        _visionFoot = FootagePart(_visionRoot.transform);
        _visionDrawn = Node("Drawn", _visionRoot.transform, layer);
        float side = Width * 0.25f;
        (_crewGlow, _crewLight, _crewBody) = Lit(_visionDrawn.transform, crew, circle, CrewColor, -side);
        (_impGlow, _impLight, _impBody) = Lit(_visionDrawn.transform, crew, circle, ImpColor, side);

        // キルの距離: 映像 (無い時は自前のクルー 2 人) に、届く範囲の円と本編のキルボタン
        _rangeRoot = Node("Range", go.transform, layer);
        _rangeFoot = FootagePart(_rangeRoot.transform);
        _rangeDisk = Part(_rangeRoot.transform, "Reach", circle, new Color(ImpColor.r, ImpColor.g, ImpColor.b, 0.16f), 2);
        _rangeRing = Part(_rangeRoot.transform, "ReachEdge", Ring, new Color(ImpColor.r, ImpColor.g, ImpColor.b, 0.85f), 2);
        _rangeHost = Figure(_rangeRoot.transform, crew, ImpColor, 0f, 3, RangeCrewH);
        _rangeTarget = Figure(_rangeRoot.transform, crew, CrewColor, 0f, 3, RangeCrewH);
        _rangeTarget.flipX = true;
        // ボタンは左下 (右には遠い相手が立つ)
        _rangeBtn = BorrowKillButton(_rangeRoot.transform, ref _rangeMat, "TohlPreviewRangeButton", left: true);

        // キルのクールダウン: 映像と本編のキルボタン (無い時はインポスターの周りの目盛り・右に狙われるクルー)
        _killRoot = Node("Kill", go.transform, layer);
        _killFoot = FootagePart(_killRoot.transform);
        BuildKillButton(ctx);
        _killDrawn = Node("Drawn", _killRoot.transform, layer);
        float cx = -Width * 0.18f, ring = Math.Min(Height * 0.4f, Width * 0.24f);
        for (int i = 0; i < Dots; i++)
        {
            float a = MathF.PI / 2f - i * MathF.PI * 2f / Dots;
            var d = Part(_killDrawn.transform, "Dot", circle, DotOff, 1);
            d.transform.localScale = new Vector3(0.07f, 0.07f, 1f);
            d.transform.localPosition = new Vector3(cx + MathF.Cos(a) * ring, MathF.Sin(a) * ring, 0f);
            _dots[i] = d;
        }
        Figure(_killDrawn.transform, crew, ImpColor, 0f, 3).transform.localPosition = new Vector3(cx, 0f, 0f);
        _victim = Figure(_killDrawn.transform, crew, CrewColor, 0f, 3);
        _victim.transform.localPosition = new Vector3(Width * 0.3f, 0f, 0f);
        // 相手を左向きに (インポスターの方を向く)
        _victim.flipX = true;

        Refresh();
        _crewR = _crewTarget;
        _impR = _impTarget;
        ShowScene();
        return go;
    }

    // 本編のキルボタンの絵と、待ち時間の暗い覆いの材質 (本編のボタンと同じ _Percent で暗くする) を借りる。秒の字は上に重ねる
    private void BuildKillButton(BuildCtx ctx)
    {
        _killBtn = BorrowKillButton(_killRoot.transform, ref _killMat, "TohlPreviewKillButton");
        if (!_killBtn) return;
        float size = Height * 0.42f;
        _killNum = new Label("", 3.2f) { Bold = true, Width = size, TextAlign = TMPro.TextAlignmentOptions.Center };
        var t = _killNum.Build(_killRoot.transform, ctx, size, out _);
        t.transform.localPosition = _killBtn.transform.localPosition + new Vector3(0f, 0f, -0.02f);
        t.SetActive(false);
    }

    // 本編のキルボタンの絵を右下 (left なら左下) に置く (材質は図ごとに 1 つを使い回す。頁を組み直すたびに作ると積み上がる)。借りられなければ null
    private SpriteRenderer BorrowKillButton(Transform parent, ref Material mat, string matName, bool left = false)
    {
        var src = HudManager.InstanceExists ? HudManager.Instance.KillButton : null;
        var g = src ? src.graphic : null;
        if (!g || !g.sprite) return null;
        var btn = Part(parent, "KillButton", g.sprite, Color.white, 3);
        if (!mat && g.sharedMaterial)
        {
            mat = new Material(g.sharedMaterial) { name = matName };
            mat.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            // 写した時のボタンの状態 (使えない時の灰色) を持ち込まない
            mat.SetFloat(DesatId, 0f);
            mat.SetFloat(PercentId, 0f);
        }
        if (mat) btn.sharedMaterial = mat;
        var b = g.sprite.bounds.size;
        float size = Height * 0.42f;
        float sc = size / Math.Max(Math.Max(b.x, b.y), 0.0001f);
        btn.transform.localScale = new Vector3(sc, sc, 1f);
        float x = Width / 2f - size / 2f - 0.08f;
        btn.transform.localPosition = new Vector3(left ? -x : x, -Height / 2f + size / 2f + 0.06f, 0f);
        btn.gameObject.SetActive(false);
        return btn;
    }

    // 直径 1 単位の細い輪 (届く範囲の縁)
    private static Sprite _ring;
    private static Sprite Ring => _ring ? _ring : _ring = BakeRing();

    private static Sprite BakeRing()
    {
        const int n = 256;
        const float w = 2.5f; // 線の太さ (画素)
        var px = new byte[n * n * 4];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float fx = x + 0.5f - n / 2f, fy = y + 0.5f - n / 2f;
            float d = MathF.Abs(MathF.Sqrt(fx * fx + fy * fy) - (n / 2f - w));
            float a = Math.Clamp(w / 2f + 0.5f - d, 0f, 1f);
            int i = (y * n + x) * 4;
            px[i] = px[i + 1] = px[i + 2] = 255;
            px[i + 3] = (byte)(a * 255f + 0.5f);
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "TohlPreviewRing", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.LoadRawTextureData(px);
        tex.Apply(false, true);
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
        var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return sp;
    }

    private SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color c, int order)
    {
        var sr = Node(name, parent, parent.gameObject.layer).AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        _parts.Add((sr, order, c.a));
        return sr;
    }

    private SpriteRenderer Figure(Transform parent, Sprite crew, Color c, float y, int order, float height = CrewH)
    {
        var sr = Part(parent, "Crew", crew, c, order);
        var b = crew ? crew.bounds.size : Vector3.one;
        float sc = height / Math.Max(b.y, 0.0001f);
        sr.transform.localScale = new Vector3(sc, sc, 1f);
        sr.transform.localPosition = new Vector3(0f, y, 0f);
        return sr;
    }

    private (SpriteRenderer glow, SpriteRenderer light, SpriteRenderer body) Lit(Transform parent, Sprite crew, Sprite circle, Color c, float x)
    {
        var glow = Part(parent, "Glow", circle, new Color(LightColor.r, LightColor.g, LightColor.b, 0.16f), 1);
        glow.transform.localPosition = new Vector3(x, 0f, 0f);
        var light = Part(parent, "Light", circle, new Color(LightColor.r, LightColor.g, LightColor.b, 0.42f), 2);
        light.transform.localPosition = new Vector3(x, 0f, 0f);
        var body = Figure(parent, crew, c, 0f, 3, VisionCrewH);
        body.transform.localPosition = new Vector3(x, 0f, 0f);
        return (glow, light, body);
    }

    private void ShowScene()
    {
        if (_speedRoot) _speedRoot.SetActive(_scene == Scene.Speed);
        if (_visionRoot) _visionRoot.SetActive(_scene == Scene.Vision);
        if (_killRoot) _killRoot.SetActive(_scene == Scene.KillCooldown);
        if (_rangeRoot) _rangeRoot.SetActive(_scene == Scene.KillRange);
        UseFootage();
    }

    // 視界の図: 当てた方を明るく、もう片方を暗く
    private void ApplyFocusAlpha()
    {
        if (!_crewBody) return;
        SetDim(_crewGlow, _crewLight, _crewBody, _focusImpostor);
        SetDim(_impGlow, _impLight, _impBody, !_focusImpostor);
    }

    private void SetDim(SpriteRenderer glow, SpriteRenderer light, SpriteRenderer body, bool dim)
    {
        float k = dim ? 0.35f : 1f;
        foreach (var (r, _, a) in _parts)
            if (r == glow || r == light || r == body)
            {
                var c = r.color;
                c.a = a * k;
                r.color = c;
            }
    }

    // ---- キルの距離 ----

    // 映像の 1 マスが欄の何単位か (映像は幅いっぱいに出している)
    private float RangeUnit => (Width - 0.08f) / 256f * RangePxPerUnit;

    // 届く範囲の円を今の値の大きさにする (中心 = 撮った時のカメラの中心 = 自分)
    private void LayoutRange()
    {
        if (!_rangeDisk) return;
        float r = NormalGameOptionsV12.KillDistances[_rangeIndex] * RangeUnit;
        _rangeDisk.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        _rangeRing.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
    }

    private void TickRange(float dt)
    {
        _rangeT += dt;
        if (_rangeT >= RangeIn + RangeOut) _rangeT -= (RangeIn + RangeOut) * MathF.Floor(_rangeT / (RangeIn + RangeOut));
        bool inside = _rangeT < RangeIn;
        int frame = _rangeIndex * 2 + (inside ? 0 : 1);
        if (frame == _rangeFrame) return;
        _rangeFrame = frame;
        if (_rangeFoot && _rangeFoot.gameObject.activeSelf)
        {
            var f = Footage.Get("range");
            if (f != null) _rangeFoot.sprite = f.Frame(frame);
        }
        else if (_rangeTarget)
        {
            // 映像の無い時: 撮った時と同じ距離 (内 = 0.06 手前・外 = 0.45 先) に相手を置く
            float d = NormalGameOptionsV12.KillDistances[_rangeIndex] + (inside ? -0.06f : 0.45f);
            _rangeTarget.transform.localPosition = new Vector3(d * RangeUnit, 0f, 0f);
        }
        // 本編のキルボタン: 届く相手がいる時は点き、いない時は灰色で薄い (本編の使えない時と同じ)
        if (_rangeBtn)
        {
            _rangeBtn.gameObject.SetActive(true);
            if (_rangeMat) _rangeMat.SetFloat(DesatId, inside ? 0f : 1f);
            var c = _rangeBtn.color;
            c.a = inside ? 1f : 0.3f;
            _rangeBtn.color = c;
        }
    }

    // ---- 毎フレーム (ゲームの頁が見えている間だけ) ----

    public void Tick()
    {
        if (!_back) return;
        // 描く順: 欄の地の順を起点に、図の部品を地より手前へ (地の順は画面を組んだ時に決まる)
        int baseOrder = _back.sortingOrder;
        if (baseOrder != _appliedBase)
        {
            _appliedBase = baseOrder;
            foreach (var (r, order, _) in _parts)
                if (r) r.sortingOrder = baseOrder + order;
            if (_killNum?.Built) _killNum.Built.sortingOrder = baseOrder + 4;
        }
        float now = Time.unscaledTime;
        float dt = _last < 0f ? 0f : Math.Min(now - _last, 0.1f);
        _last = now;
        switch (_scene)
        {
            case Scene.Speed: TickSpeed(now, dt); break;
            case Scene.Vision: TickVision(dt); break;
            case Scene.KillCooldown: TickKill(now, dt); break;
            case Scene.KillRange: TickRange(dt); break;
        }
    }

    private void TickSpeed(float now, float dt)
    {
        // 映像: 1 倍で撮った歩きを値の倍率で進める (歩幅と足の運びが速さに比べて変わるのは本編の速さと同じ)
        if (_speedFoot && _speedFoot.gameObject.activeSelf)
        {
            var f = Footage.Get("speed");
            if (f == null) return;
            _speedT += dt * _speed * f.Fps;
            if (_speedT >= f.Count) _speedT -= f.Count * MathF.Floor(_speedT / f.Count);
            int i = (int)_speedT;
            if (i != _speedFrame) { _speedFrame = i; _speedFoot.sprite = f.Frame(i); }
            return;
        }
        float margin = 0.2f, span = Width - margin * 2f;
        Run(_refCrew, now, 1f, margin, span, Height * 0.22f);
        Run(_curCrew, now, _speed, margin, span, -Height * 0.22f);
    }

    private void Run(Transform t, float now, float speed, float margin, float span, float y)
    {
        if (!t) return;
        float p = now * speed / RefCross;
        p -= MathF.Floor(p);
        // 歩くたびに小さく跳ねる (速いほど細かく)
        float bob = MathF.Abs(MathF.Sin(now * speed * 9f)) * 0.03f;
        t.localPosition = new Vector3(-Width / 2f + margin + p * span, y + bob, 0f);
    }

    private void TickVision(float dt)
    {
        float k = 1f - MathF.Exp(-dt * 12f);
        _crewR += (_crewTarget - _crewR) * k;
        _impR += (_impTarget - _impR) * k;
        SetLight(_crewGlow, _crewLight, _crewR);
        SetLight(_impGlow, _impLight, _impR);
    }

    private static void SetLight(SpriteRenderer glow, SpriteRenderer light, float r)
    {
        if (light) light.transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
        if (glow) glow.transform.localScale = new Vector3(r * 2.5f, r * 2.5f, 1f);
    }

    private void TickKill(float now, float dt)
    {
        // 映像: 待ち時間 (ボタンが暗く、秒を数える) → キルの映像を 1 回流す → 繰り返す
        if (_killFoot && _killFoot.gameObject.activeSelf)
        {
            var f = Footage.Get("kill");
            if (f == null) return;
            float wait = Math.Max(_cooldown * CooldownScale, 0.3f);
            float play = f.Count / Math.Max(f.Fps, 1f);
            _killT += dt;
            if (_killT >= wait + play) _killT -= (wait + play) * MathF.Floor(_killT / (wait + play));
            bool waiting = _killT < wait;
            int frame = waiting ? 0 : Math.Min((int)((_killT - wait) * f.Fps), f.Count - 1);
            if (frame != _killFrame) { _killFrame = frame; _killFoot.sprite = f.Frame(frame); }
            float left = waiting ? (1f - _killT / wait) : 0f;
            if (_killMat && left != _killPercent) { _killPercent = left; _killMat.SetFloat(PercentId, left); }
            // 秒の字は本編の秒で (縮めて見せるのは進む速さだけ)。変わった時だけ書く
            int secs = waiting ? (int)MathF.Ceiling(left * _cooldown) : -1;
            if (secs != _killShown && _killNum != null)
            {
                _killShown = secs;
                _killNum.SetText(secs > 0 ? secs.ToString() : "");
            }
            return;
        }
        float period = Math.Max(_cooldown * CooldownScale, 0.3f);
        float p = now / period;
        p -= MathF.Floor(p);
        int lit = (int)(p * Dots) + 1;
        for (int i = 0; i < Dots; i++)
            if (_dots[i]) _dots[i].color = i < lit ? ImpColor : DotOff;
        // 1 周の終わりに、狙われたクルーが一瞬消える
        if (_victim)
        {
            var c = _victim.color;
            c.a = p > 0.92f ? 0.15f : 1f;
            _victim.color = c;
        }
    }
}
