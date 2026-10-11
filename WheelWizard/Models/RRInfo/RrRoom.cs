using System.ComponentModel;
using WheelWizard.Localization;
using WheelWizard.WiiManagement.MiiManagement.Domain.Mii;

namespace WheelWizard.Models.RRInfo;

public class RrRoom : INotifyPropertyChanged
{
    public required string Id { get; set; }
    public required DateTime Created { get; set; }
    public required string Type { get; set; }
    public required bool Suspend { get; set; }
    public string? Rk { get; set; } // RK does not exists in private rooms
    public string TrackName { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public int? RaceNumber { get; set; }
    public int? CourseId { get; set; }
    public DateTime? RaceObservedAt { get; set; }
    public required List<RrPlayer> Players { get; set; }

    public int PlayerCount => Players.Count;

    public string TimeOnline => tTime(DateTime.UtcNow - Created);
    public bool IsPublic => Type != "private";

    public string GameModeAbbrev =>
        Rk switch
        {
            // Retro Rewind
            "vs_10" => "RR",
            "vs_11" => "TT",
            "vs_12" => "200",
            "vs_13" => "IR",
            "vs_14" => "BT",
            "vs_15" => "EBT",
            "vs_20" => "RR Ct",
            "vs_21" => "RR V",

            // CTGP_C
            "vs_668" => "CTGP-C",

            // Insane Kart Wii
            "vs_69" => "IKW",
            "vs_70" => "Ultras",
            "vs_71" => "Crazy",
            "vs_72" => "Bomb",
            "vs_73" => "Accel",
            "vs_74" => "Banana",
            "vs_75" => "RndItm",
            "vs_76" => "Unfair",
            "vs_77" => "Blue",
            "vs_78" => "Shroom",
            "vs_79" => "Bumper",
            "vs_80" => "Rampage",
            "vs_81" => "Rain",
            "vs_82" => "Break",
            "vs_83" => "Riibal",

            // Luminous
            "vs_666" => "Lumi",
            "vs_667" => "Lumi TT",

            // OptPack
            "vs_875" => "OP 150",
            "vs_876" => "OP TT",
            "vs_877" => "OP R1",
            "vs_878" => "OP R2",
            "vs_879" => "OP R3",
            "vs_880" => "OP R4",

            // WTP
            "vs_1312" => "WTP 150",
            "vs_1313" => "WTP 200",
            "vs_1314" => "WTP TT",
            "vs_1315" => "WTP IR",
            "vs_1316" => "STYD",

            // Generic Versus
            "vs_751" => "VS",
            "vs_-1" => "Reg",
            "vs" => "Reg",

            _ => IsPublic ? "??" : "Lock",
        };

    public string GameMode =>
        Rk switch
        {
            //Max Size:"----------------------"
            // Retro Rewind
            "vs_10" => "RR 150CC",
            "vs_11" => "RR Time Tr",
            "vs_12" => "RR 200CC",
            "vs_13" => "RR Item Rain",
            "vs_14" => "RR Battle",
            "vs_15" => "RR Elim Battle",
            "vs_20" => "RR 150CC CTs",
            "vs_21" => "RR Vanilla",

            // CTGP
            "vs_668" => "CTGP-C",

            // Insane Kart Wii
            "vs_69" => "Insane Kart",
            "vs_70" => "Ultras VS",
            "vs_71" => "Crazy Items",
            "vs_72" => "Bob-omb Blast",
            "vs_73" => "Inf Accel",
            "vs_74" => "Banan Slip",
            "vs_75" => "Rand Items",
            "vs_76" => "Unfair Items",
            "vs_77" => "Blue Madness",
            "vs_78" => "Mush Dash",
            "vs_79" => "Bumper Karts",
            "vs_80" => "Item Rampage",
            "vs_81" => "Item Rain",
            "vs_82" => "Shell Break",
            "vs_83" => "Riibalanced",

            // Luminous
            "vs_666" => "Luminous",
            "vs_667" => "Luminous TT",

            // OptPack
            "vs_875" => "OP 150",
            "vs_876" => "OP TT",
            "vs_877" => "OP R1",
            "vs_878" => "OP R2",
            "vs_879" => "OP R3",
            "vs_880" => "OP R4",

            // WTP
            "vs_1312" => "WTP 150CC",
            "vs_1313" => "WTP 200CC",
            "vs_1314" => "WTP Time Trial",
            "vs_1315" => "WTP Item Rain",
            "vs_1316" => "WTP STYD",

            // Generic
            "vs_751" => "Versus",
            "vs_-1" => "Regular",
            "vs" => "Regular",
            _ => IsPublic ? "Unknown Mode" : "Private Room",
        };

    public int AverageVr
    {
        get
        {
            var vrs = Players.Select(p => p.Vr).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            return vrs.Count == 0 ? 0 : (int)vrs.Average();
        }
    }

    public string AverageVrDisplay => Players.Any(player => player.Vr.HasValue) ? AverageVr.ToString("N0") : "—";

    public string DisplayGameMode => GameMode == "Unknown Mode" ? (IsPublic ? "Public Room" : "Private Room") : GameMode;

    public string TrackDisplay => string.IsNullOrWhiteSpace(TrackName) ? t("text.waiting_for_next_race") : TranslateTrackName(TrackName);

    private static string TranslateTrackName(string trackName)
    {
        if (!string.Equals(LocalizationProvider.Current.CurrentLanguage, "ja", StringComparison.OrdinalIgnoreCase))
            return trackName;

        var translated = trackName;
        // Replace the longest names first so entries such as "Mario Circuit 3"
        // are not partially matched by the shorter "Mario Circuit" entry.
        foreach (var (english, japanese) in JapaneseTrackNames.OrderByDescending(pair => pair.Key.Length))
            translated = translated.Replace(english, japanese, StringComparison.OrdinalIgnoreCase);
        return translated;
    }

    private static readonly IReadOnlyDictionary<string, string> JapaneseTrackNames = new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        // Wii Mushroom Cup
        ["Luigi Circuit"] = "ルイージサーキット",
        ["Moo Moo Meadows"] = "モーモーカントリー",
        ["Mushroom Gorge"] = "キノコキャニオン",
        ["Toad's Factory"] = "キノピオファクトリー",

        // Wii Flower Cup
        ["Mario Circuit"] = "マリオサーキット",
        ["Coconut Mall"] = "ココナッツモール",
        ["DK Summit"] = "DKスノーボードクロス",
        ["Wario's Gold Mine"] = "ワリオこうざん",

        // Wii Star Cup
        ["Daisy Circuit"] = "デイジーサーキット",
        ["Koopa Cape"] = "ノコノコみさき",
        ["Maple Treeway"] = "メイプルツリーハウス",
        ["Grumble Volcano"] = "グラグラかざん",

        // Wii Special Cup
        ["Dry Dry Ruins"] = "カラカラいせき",
        ["Moonview Highway"] = "ムーンリッジ＆ハイウェイ",
        ["Bowser's Castle"] = "クッパキャッスル",
        ["Rainbow Road"] = "レインボーロード",

        // Retro Rewind / custom packs commonly use these English names.
        ["SNES Mario Circuit 3"] = "SFCマリオサーキット3",
        ["SNES Bowser Castle 2"] = "SFCクッパじょう2",
        ["SNES Donut Plains 3"] = "SFCドーナツへいや3",
        ["SNES Ghost Valley 2"] = "SFCおばけぬま2",
        ["SNES Choco Island 2"] = "SFCチョコレーとう2",
        ["SNES Vanilla Lake 1"] = "SFCバニラレイク1",
        ["SNES Rainbow Road"] = "SFCレインボーロード",
        ["N64 Mario Raceway"] = "N64マリオサーキット",
        ["N64 Royal Raceway"] = "N64ピーチサーキット",
        ["N64 Kalimari Desert"] = "N64カラカラさばく",
        ["N64 Sherbet Land"] = "N64シャーベットランド",
        ["N64 Yoshi Valley"] = "N64ヨッシーバレー",
        ["N64 DK's Jungle Parkway"] = "N64 DKジャングルパークウェイ",
        ["N64 Banshee Boardwalk"] = "N64ヒュードロいけ",
        ["N64 Wario Stadium"] = "N64ワリオスタジアム",
        ["N64 Choco Mountain"] = "N64チョコマウンテン",
        ["GBA Peach Circuit"] = "GBAピーチサーキット",
        ["GBA Cheese Land"] = "GBAチーズランド",
        ["GBA Cheep-Cheep Island"] = "GBAプクプクアイランド",
        ["GBA Cheep Cheep Island"] = "GBAプクプクアイランド",
        ["GBA Rainbow Road DX"] = "GBAレインボーロードDX",
        ["GBA Rainbow Road"] = "GBAレインボーロード",
        ["GBA Bowser Castle 3"] = "GBAクッパキャッスル3",
        ["GBA Shy Guy Beach"] = "GBAヘイホービーチ",
        ["GBA Sky Garden"] = "GBAスカイガーデン",
        ["GCN Peach Beach"] = "GCNピーチビーチ",
        ["GCN Waluigi Stadium"] = "GCNワルイージスタジアム",
        ["GCN Mario Circuit"] = "GCNマリオサーキット",
        ["GCN DK Mountain"] = "GCN DKマウンテン",
        ["DS Figure-8 Circuit"] = "DSエイトクロスサーキット",
        ["DS Yoshi Falls"] = "DSヨッシーフォールズ",
        ["DS Cheep Cheep Beach"] = "DSプクプクビーチ",
        ["DS Luigi's Mansion"] = "DSルイージマンション",
        ["DS Desert Hills"] = "DSサンサンさばく",
        ["DS Delfino Square"] = "DSモンテタウン",
        ["Delfino Square"] = "モンテタウン",
        ["DS Waluigi Pinball"] = "DSワルイージピンボール",
        ["DS Wario Stadium"] = "DSワリオスタジアム",
        ["DS Rainbow Road"] = "DSレインボーロード",
        ["DS Shroom Ridge"] = "DSキノコリッジウェイ",
        ["DS Tick-Tock Clock"] = "DSチクタクロック",
        ["DS Mario Circuit"] = "DSマリオサーキット",
        ["DS Airship Fortress"] = "DSキラーシップ",
        ["Wii U Sunshine Airport"] = "Wii Uサンシャインくうこう",
        ["Wii U Cheese Land"] = "Wii Uチーズランド",
        ["Wii U Royal Raceway"] = "Wii Uピーチサーキット",
        ["Wii U Wario Stadium"] = "Wii Uワリオスタジアム",
        ["Wii U Dolphin Shoals"] = "Wii Uドルフィンみさき",
        ["Wii U Electrodrome"] = "Wii Uエレクトロドリーム",
        ["Wii U Mount Wario"] = "Wii Uワリオスノーマウンテン",
        ["Wii U Cloudtop Cruise"] = "Wii Uスカイガーデン",
        ["Wii U Bone-Dry Dunes"] = "Wii Uホネホネさばく",
        ["Wii U Bowser Castle"] = "Wii Uクッパキャッスル",
        ["Wii U Rainbow Road"] = "Wii Uレインボーロード",
        ["Wii U Yoshi Circuit"] = "Wii Uヨッシーサーキット",
        ["Wii U Animal Crossing"] = "Wii Uどうぶつの森",
        ["Wii U Excitebike Arena"] = "Wii Uエキサイトバイク",
        ["Wii U Hyrule Circuit"] = "Wii Uハイラルサーキット",
        ["Wii U Sunset Wilds"] = "Wii Uサンセットこうや",
        ["SW2 Ghost Valley"] = "SW2おばけぬま",
        ["SW2 Dandelion Depths"] = "SW2ダンデライオンデプス",
        ["SW2 Mario Bros Circuit"] = "SW2マリオブラザーズサーキット",
        ["SW Kalimari Desert"] = "SWカラカラさばく",
        ["Great ? Block Ruins"] = "グレート？ブロック遺跡",
        ["Great Block Ruins"] = "グレートブロック遺跡",
        ["Block Ruins"] = "ブロック遺跡",
        ["Beta Delfino Square"] = "ベータ版モンテタウン",
        ["Beta Nokonoko Beach"] = "ベータ版ノコノコビーチ",
        ["Beta Waluigi Pinball"] = "ベータ版ワルイージピンボール",
        ["Madrid Drive"] = "マドリードグランデ",
        ["Tour Madrid Drive"] = "ツアーマドリードグランデ",
        ["Tour Cheep-Cheep Island"] = "ツアープクプクアイランド",
        ["Tour Cheep Cheep Island"] = "ツアープクプクアイランド",
        ["Tour DK Summit"] = "ツアーDKスノーボードクロス",
        ["Musical Cliff"] = "ミュージカルクリフ",
        ["Heavenly Sanctuary"] = "天空の聖域",
        ["Bridgey City"] = "ブリッジーシティ",
        ["3DS DK Jungle"] = "3DS DKジャングル",
        ["Overgrown Temple"] = "草深き神殿",
        ["Dark Matter Fortress"] = "ダークマター要塞",
    };

    public string RaceTimeDisplay => RaceObservedAt.HasValue ? tTime(DateTime.UtcNow - RaceObservedAt.Value) : t("text.waiting");

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshRaceTime() => PropertyChanged?.Invoke(this, new(nameof(RaceTimeDisplay)));

    public RrPlayer? HostPlayer => Players.FirstOrDefault(p => p.IsOpenHost);

    public Mii? HostMii => HostPlayer?.FirstMii;
}
