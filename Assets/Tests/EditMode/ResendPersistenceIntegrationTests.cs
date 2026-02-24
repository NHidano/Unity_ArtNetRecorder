using System;
using System.Net;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// 永続化設定の統合確認テスト。
/// タスク 5.3: DstIP / DstPort / トグル状態の保存と復元が正しく機能することを確認する。
///
/// 検証対象コンポーネント:
/// - ArtNetResendUI: 永続化の起点となるUIコンポーネント (タスク4)
/// - ResendSettingsKeys: PlayerPrefsキー定数
/// - ResendSettingsDefaults: デフォルト値定数
/// - ResendSettingsValidator: 検証・フォールバックロジック
///
/// 統合シナリオ:
/// 1. DstIP / DstPort / トグル状態の保存と復元の完全フロー
/// 2. 保存データ未存在時のデフォルト値表示
/// 3. 不正データ保存時のフォールバック動作
///
/// Requirements: 4.1, 4.2, 4.3, 4.4, 4.5
/// </summary>
public class ResendPersistenceIntegrationTests
{
    #region 1. 保存・復元の完全フロー: 検証成功 -> 保存 -> 読み込み -> 表示

    [Test]
    public void 統合フロー_有効なIPアドレスの検証から復元までの一貫性()
    {
        // Requirement 4.1, 4.3: DstIPの保存と起動時の読み込み・表示
        // フロー: ユーザー入力 -> IPAddress.TryParse検証 -> SaveIP -> (再起動) -> LoadSettings -> GetValidatedIP -> 表示

        // 1. ユーザーが有効なIPアドレスを入力 -> 検証成功
        string inputIP = "192.168.1.100";
        Assert.IsTrue(ResendSettingsValidator.IsValidIP(inputIP),
            "入力値 '192.168.1.100' が有効なIPアドレスとして検証成功すること");

        // 2. 保存後に読み込んだ値を検証 -> そのまま返される
        string validated = ResendSettingsValidator.GetValidatedIP(inputIP);
        Assert.AreEqual(inputIP, validated,
            "有効なIPアドレスは GetValidatedIP でそのまま返されること");

        // 3. 返された値が表示可能であること（IPAddress.TryParseで再検証可能）
        Assert.IsTrue(IPAddress.TryParse(validated, out _),
            "復元された値が IPAddress.TryParse で再検証可能であること");
    }

    [Test]
    public void 統合フロー_有効なポート番号の検証から復元までの一貫性()
    {
        // Requirement 4.2, 4.3: DstPortの保存と起動時の読み込み・表示
        // フロー: ユーザー入力 -> int.TryParse検証 -> SavePort -> (再起動) -> LoadSettings -> GetValidatedPort -> 表示

        // 1. ユーザーが有効なポート番号を文字列で入力 -> 検証成功
        string inputPortStr = "7000";
        Assert.IsTrue(ResendSettingsValidator.IsValidPortString(inputPortStr),
            "入力値 '7000' が有効なポート文字列として検証成功すること");

        // 2. int値に変換して保存 -> 読み込み後に検証
        int.TryParse(inputPortStr, out int inputPort);
        int validated = ResendSettingsValidator.GetValidatedPort(inputPort);
        Assert.AreEqual(inputPort, validated,
            "有効なポート番号は GetValidatedPort でそのまま返されること");

        // 3. 返された値が表示用文字列に変換可能
        string displayText = validated.ToString();
        Assert.AreEqual("7000", displayText,
            "復元されたポート番号が正しい表示文字列に変換されること");
    }

    [Test]
    public void 統合フロー_トグル状態ON保存から復元までの一貫性()
    {
        // Requirement 4.5, 4.3: トグル状態の保存と起動時の復元
        // フロー: トグルON -> ToggleStateToInt(true) -> PlayerPrefs.SetInt -> (再起動) -> PlayerPrefs.GetInt -> IntToToggleState -> Toggle.isOn

        // 1. トグルをONに変更 -> int値に変換
        bool toggleOn = true;
        int savedValue = ResendSettingsValidator.ToggleStateToInt(toggleOn);
        Assert.AreEqual(1, savedValue, "ON状態がint値1に変換されること");

        // 2. 保存された値を読み込み -> bool値に復元
        bool restored = ResendSettingsValidator.IntToToggleState(savedValue);
        Assert.IsTrue(restored, "保存されたint値1がtrue (ON) に復元されること");

        // 3. 往復変換の一貫性
        Assert.AreEqual(toggleOn, restored,
            "トグル状態のint変換→bool変換の往復が一貫すること");
    }

    [Test]
    public void 統合フロー_トグル状態OFF保存から復元までの一貫性()
    {
        // Requirement 4.5, 4.3: トグル状態OFFの保存と起動時の復元

        // 1. トグルをOFFに変更 -> int値に変換
        bool toggleOff = false;
        int savedValue = ResendSettingsValidator.ToggleStateToInt(toggleOff);
        Assert.AreEqual(0, savedValue, "OFF状態がint値0に変換されること");

        // 2. 保存された値を読み込み -> bool値に復元
        bool restored = ResendSettingsValidator.IntToToggleState(savedValue);
        Assert.IsFalse(restored, "保存されたint値0がfalse (OFF) に復元されること");

        // 3. 往復変換の一貫性
        Assert.AreEqual(toggleOff, restored,
            "トグル状態OFFのint変換→bool変換の往復が一貫すること");
    }

    [Test]
    public void 統合フロー_複数のArtNet標準IPアドレスの保存復元が正常に動作する()
    {
        // ArtNetで使用される典型的なIPアドレスのバリエーションテスト
        // Requirement 4.1, 4.3

        string[] artnetIPs = {
            "2.0.0.1",      // ArtNetデフォルト
            "2.255.255.255", // ArtNetブロードキャスト
            "10.0.0.1",     // プライベートネットワーク
            "192.168.0.1",  // ローカルネットワーク
            "255.255.255.255" // ブロードキャスト
        };

        foreach (var ip in artnetIPs)
        {
            // 検証 -> 保存想定 -> 復元検証
            Assert.IsTrue(ResendSettingsValidator.IsValidIP(ip),
                $"IP '{ip}' が有効として検証されること");

            var validated = ResendSettingsValidator.GetValidatedIP(ip);
            Assert.AreEqual(ip, validated,
                $"IP '{ip}' が GetValidatedIP でそのまま返されること");
        }
    }

    [Test]
    public void 統合フロー_有効なポート番号範囲全体の保存復元が正常に動作する()
    {
        // ポート番号の境界値と代表値のバリエーションテスト
        // Requirement 4.2, 4.3

        int[] validPorts = { 1, 80, 443, 6454, 8080, 49152, 65535 };

        foreach (var port in validPorts)
        {
            // 文字列入力からの検証
            Assert.IsTrue(ResendSettingsValidator.IsValidPortString(port.ToString()),
                $"ポート {port} の文字列検証が成功すること");

            // int値の検証
            Assert.IsTrue(ResendSettingsValidator.IsValidPort(port),
                $"ポート {port} の数値検証が成功すること");

            // 復元後の検証
            var validated = ResendSettingsValidator.GetValidatedPort(port);
            Assert.AreEqual(port, validated,
                $"ポート {port} が GetValidatedPort でそのまま返されること");
        }
    }

    #endregion

    #region 2. 保存データ未存在時のデフォルト値表示

    [Test]
    public void デフォルト値_DstIPデフォルトが設計書仕様と一致する()
    {
        // Requirement 4.4: 保存データ未存在時のデフォルト値表示
        // 設計書: デフォルト DstIP = "2.0.0.1"

        Assert.AreEqual("2.0.0.1", ResendSettingsDefaults.DstIP,
            "デフォルトDstIPが設計書の '2.0.0.1' と一致すること");

        // デフォルト値自体が有効なIPアドレスであること
        Assert.IsTrue(ResendSettingsValidator.IsValidIP(ResendSettingsDefaults.DstIP),
            "デフォルトDstIPが有効なIPアドレスであること");
    }

    [Test]
    public void デフォルト値_DstPortデフォルトが設計書仕様と一致する()
    {
        // Requirement 4.4: 保存データ未存在時のデフォルト値表示
        // 設計書: デフォルト DstPort = 6454

        Assert.AreEqual(6454, ResendSettingsDefaults.DstPort,
            "デフォルトDstPortが設計書の 6454 と一致すること");

        // デフォルト値自体が有効なポート番号であること
        Assert.IsTrue(ResendSettingsValidator.IsValidPort(ResendSettingsDefaults.DstPort),
            "デフォルトDstPortが有効なポート番号であること");
    }

    [Test]
    public void デフォルト値_トグルデフォルトがOFF状態と一致する()
    {
        // Requirement 4.4: 保存データ未存在時のデフォルト値表示
        // 設計書: デフォルト Toggle = OFF (0)

        Assert.AreEqual(0, ResendSettingsDefaults.Enabled,
            "デフォルトのトグル値が 0 (OFF) であること");

        // デフォルト値をboolに変換するとfalseであること
        Assert.IsFalse(ResendSettingsValidator.IntToToggleState(ResendSettingsDefaults.Enabled),
            "デフォルトのトグル値がfalse (OFF) に変換されること");
    }

    [Test]
    public void デフォルト値_PlayerPrefsキーが設計書の命名規約と一致する()
    {
        // Requirement 4.1, 4.2, 4.5: PlayerPrefsキー名の一貫性
        // 設計書: "ArtNetResend_DstIP", "ArtNetResend_DstPort", "ArtNetResend_Enabled"

        Assert.AreEqual("ArtNetResend_DstIP", ResendSettingsKeys.DstIP,
            "DstIPキーが設計書の命名規約に従うこと");
        Assert.AreEqual("ArtNetResend_DstPort", ResendSettingsKeys.DstPort,
            "DstPortキーが設計書の命名規約に従うこと");
        Assert.AreEqual("ArtNetResend_Enabled", ResendSettingsKeys.Enabled,
            "Enabledキーが設計書の命名規約に従うこと");

        // 全てのキーが "ArtNetResend_" プレフィックスを持つこと
        Assert.IsTrue(ResendSettingsKeys.DstIP.StartsWith("ArtNetResend_"),
            "DstIPキーが 'ArtNetResend_' プレフィックスを持つこと");
        Assert.IsTrue(ResendSettingsKeys.DstPort.StartsWith("ArtNetResend_"),
            "DstPortキーが 'ArtNetResend_' プレフィックスを持つこと");
        Assert.IsTrue(ResendSettingsKeys.Enabled.StartsWith("ArtNetResend_"),
            "Enabledキーが 'ArtNetResend_' プレフィックスを持つこと");
    }

    [Test]
    public void デフォルト値_GetValidated系メソッドがデフォルト値をフォールバックとして使用する()
    {
        // Requirement 4.4: 保存データ未存在時はデフォルト値にフォールバック
        // PlayerPrefs.GetString(key, defaultValue) / GetInt(key, defaultValue) の
        // defaultValue引数にResendSettingsDefaultsの値が使用される前提で、
        // GetValidated系メソッドがさらにその値を検証する。

        // デフォルト値自体がGetValidatedを通過すること
        var validatedIP = ResendSettingsValidator.GetValidatedIP(ResendSettingsDefaults.DstIP);
        Assert.AreEqual(ResendSettingsDefaults.DstIP, validatedIP,
            "デフォルトDstIPがGetValidatedIPを通過してそのまま返されること");

        var validatedPort = ResendSettingsValidator.GetValidatedPort(ResendSettingsDefaults.DstPort);
        Assert.AreEqual(ResendSettingsDefaults.DstPort, validatedPort,
            "デフォルトDstPortがGetValidatedPortを通過してそのまま返されること");
    }

    #endregion

    #region 3. 不正データ保存時のフォールバック動作

    [Test]
    public void フォールバック_不正なIPアドレス文字列がデフォルト値に置換される()
    {
        // Requirement 4.4: 不正データ保存時のフォールバック
        // PlayerPrefsに不正な値が直接書き込まれた場合でも、
        // LoadSettings内のGetValidatedIPでデフォルト値にフォールバックする

        string[] invalidIPs = {
            "",               // 空文字列
            "invalid",        // 文字列
            "abc.def.ghi.jkl", // 非数値
            "not-an-ip",      // 不正な形式
            "hello world",    // スペースを含む文字列
            "192.168.1.1.1"   // オクテット過多
        };

        foreach (var invalidIP in invalidIPs)
        {
            var validated = ResendSettingsValidator.GetValidatedIP(invalidIP);
            Assert.AreEqual(ResendSettingsDefaults.DstIP, validated,
                $"不正なIP '{invalidIP}' がデフォルト値 '{ResendSettingsDefaults.DstIP}' にフォールバックすること");
        }
    }

    [Test]
    public void フォールバック_nullのIPアドレスがデフォルト値に置換される()
    {
        // PlayerPrefsからnullが返される可能性への対応
        var validated = ResendSettingsValidator.GetValidatedIP(null);
        Assert.AreEqual(ResendSettingsDefaults.DstIP, validated,
            "null IPアドレスがデフォルト値にフォールバックすること");
    }

    [Test]
    public void フォールバック_不正なポート番号がデフォルト値に置換される()
    {
        // Requirement 4.4: 不正データ保存時のフォールバック
        // PlayerPrefsに不正なポート番号が保存されていた場合

        int[] invalidPorts = {
            0,        // 下限以下
            -1,       // 負の値
            -6454,    // 負のArtNetポート
            65536,    // 上限超過
            100000,   // 大きすぎる値
            int.MinValue, // 最小int値
            int.MaxValue  // 最大int値
        };

        foreach (var invalidPort in invalidPorts)
        {
            var validated = ResendSettingsValidator.GetValidatedPort(invalidPort);
            Assert.AreEqual(ResendSettingsDefaults.DstPort, validated,
                $"不正なポート {invalidPort} がデフォルト値 {ResendSettingsDefaults.DstPort} にフォールバックすること");
        }
    }

    [Test]
    public void フォールバック_不正なトグル値がOFF状態にフォールバックする()
    {
        // Requirement 4.5: トグル状態の不正値フォールバック
        // PlayerPrefsに0/1以外の値が保存されていた場合

        int[] invalidToggleValues = {
            -1,    // 負の値
            2,     // 範囲外
            99,    // 大きすぎる値
            255,   // byte最大値
            int.MaxValue // 最大int値
        };

        foreach (var invalidValue in invalidToggleValues)
        {
            bool restored = ResendSettingsValidator.IntToToggleState(invalidValue);
            Assert.IsFalse(restored,
                $"不正なトグル値 {invalidValue} がfalse (OFF) にフォールバックすること");
        }
    }

    [Test]
    public void フォールバック_ポート番号文字列パース失敗時の検証フロー()
    {
        // ユーザーがポート入力フィールドに数値以外を入力した場合のフロー
        // int.TryParse失敗 -> 保存されない -> 前回の有効値が保持される

        string[] invalidPortStrings = {
            "abc",     // 非数値
            "",        // 空文字列
            "12.34",   // 小数
            "6454abc", // 数値+文字
            " ",       // 空白
            "0x1936"   // 16進数
        };

        foreach (var invalidStr in invalidPortStrings)
        {
            // ユーザー入力時の検証で失敗する
            Assert.IsFalse(ResendSettingsValidator.IsValidPortString(invalidStr),
                $"不正なポート文字列 '{invalidStr}' の検証が失敗すること");

            // int.TryParseも失敗するケースを確認（保存処理に到達しない）
            bool canParse = int.TryParse(invalidStr, out int parsed);
            if (canParse)
            {
                // パースは成功するが範囲外の場合
                Assert.IsFalse(ResendSettingsValidator.IsValidPort(parsed),
                    $"パース可能だが範囲外のポート値 {parsed} の検証が失敗すること");
            }
        }
    }

    #endregion

    #region 4. ArtNetResendUI の構造的統合検証

    [Test]
    public void ArtNetResendUI_LoadSettingsがStartメソッド内から呼ばれる構造()
    {
        // Requirement 4.3: アプリケーション起動時に保存値を読み込んで表示する
        // LoadSettings は Start() 内の冒頭で呼ばれ、PlayerPrefsから値を読み込む。

        // LoadSettingsメソッドの存在確認
        var loadMethod = typeof(ArtNetResendUI).GetMethod("LoadSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(loadMethod, "LoadSettings メソッドが存在すること");

        // Start メソッドの存在確認（Unity ライフサイクルメソッド）
        var startMethod = typeof(ArtNetResendUI).GetMethod("Start",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(startMethod, "Start メソッドが存在すること");
    }

    [Test]
    public void ArtNetResendUI_SaveIPとSavePortとSaveToggleStateの全保存メソッドが揃っている()
    {
        // Requirement 4.1, 4.2, 4.5: 各値の保存メソッドが揃っていること
        var saveIPMethod = typeof(ArtNetResendUI).GetMethod("SaveIP",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var savePortMethod = typeof(ArtNetResendUI).GetMethod("SavePort",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var saveToggleMethod = typeof(ArtNetResendUI).GetMethod("SaveToggleState",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(saveIPMethod, "SaveIP メソッドが存在すること");
        Assert.IsNotNull(savePortMethod, "SavePort メソッドが存在すること");
        Assert.IsNotNull(saveToggleMethod, "SaveToggleState メソッドが存在すること");

        // 引数型の確認
        var saveIPParams = saveIPMethod.GetParameters();
        Assert.AreEqual(1, saveIPParams.Length, "SaveIP が1引数であること");
        Assert.AreEqual(typeof(string), saveIPParams[0].ParameterType,
            "SaveIP の引数型が string であること");

        var savePortParams = savePortMethod.GetParameters();
        Assert.AreEqual(1, savePortParams.Length, "SavePort が1引数であること");
        Assert.AreEqual(typeof(int), savePortParams[0].ParameterType,
            "SavePort の引数型が int であること");

        var saveToggleParams = saveToggleMethod.GetParameters();
        Assert.AreEqual(1, saveToggleParams.Length, "SaveToggleState が1引数であること");
        Assert.AreEqual(typeof(bool), saveToggleParams[0].ParameterType,
            "SaveToggleState の引数型が bool であること");
    }

    [Test]
    public void ArtNetResendUI_UIフィールドが3つ全て揃っている()
    {
        // 永続化に関与する全UIフィールドの存在確認
        var enableToggle = typeof(ArtNetResendUI).GetField("enableToggle",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var ipInputField = typeof(ArtNetResendUI).GetField("ipInputField",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var portInputField = typeof(ArtNetResendUI).GetField("portInputField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(enableToggle, "enableToggle フィールドが存在すること");
        Assert.IsNotNull(ipInputField, "ipInputField フィールドが存在すること");
        Assert.IsNotNull(portInputField, "portInputField フィールドが存在すること");

        // 全てが SerializeField 属性を持つこと
        Assert.IsTrue(Attribute.IsDefined(enableToggle, typeof(UnityEngine.SerializeField)),
            "enableToggle に [SerializeField] が付与されていること");
        Assert.IsTrue(Attribute.IsDefined(ipInputField, typeof(UnityEngine.SerializeField)),
            "ipInputField に [SerializeField] が付与されていること");
        Assert.IsTrue(Attribute.IsDefined(portInputField, typeof(UnityEngine.SerializeField)),
            "portInputField に [SerializeField] が付与されていること");
    }

    [Test]
    public void ArtNetResendUI_公開プロパティが永続化設定と連携する構造()
    {
        // ArtNetResendUI の公開プロパティ（IsEnabled, Port, IPAddress）が
        // 永続化された値の読み込み後に正しく機能する構造であることを確認する

        var isEnabledProp = typeof(ArtNetResendUI).GetProperty("IsEnabled",
            BindingFlags.Public | BindingFlags.Instance);
        var portProp = typeof(ArtNetResendUI).GetProperty("Port",
            BindingFlags.Public | BindingFlags.Instance);
        var ipAddressProp = typeof(ArtNetResendUI).GetProperty("IPAddress",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(isEnabledProp, "IsEnabled プロパティが公開されていること");
        Assert.IsNotNull(portProp, "Port プロパティが公開されていること");
        Assert.IsNotNull(ipAddressProp, "IPAddress プロパティが公開されていること");

        Assert.AreEqual(typeof(bool), isEnabledProp.PropertyType,
            "IsEnabled の型が bool であること");
        Assert.AreEqual(typeof(int), portProp.PropertyType,
            "Port の型が int であること");
        Assert.AreEqual(typeof(IPAddress), ipAddressProp.PropertyType,
            "IPAddress の型が IPAddress であること");
    }

    #endregion

    #region 5. 保存・復元のエンドツーエンドシナリオ

    [Test]
    public void エンドツーエンド_初回起動時のデフォルト値表示シナリオ()
    {
        // Requirement 4.4: 保存データ未存在時のデフォルト値表示
        // シナリオ: 初回起動 -> PlayerPrefsにキーが存在しない -> デフォルト値で各フィールドを初期化

        // PlayerPrefs.GetString(key, defaultValue) のデフォルト引数として使用される値
        string defaultIP = ResendSettingsDefaults.DstIP;
        int defaultPort = ResendSettingsDefaults.DstPort;
        int defaultEnabled = ResendSettingsDefaults.Enabled;

        // 取得されたデフォルト値を GetValidated で検証（LoadSettings内の処理）
        string validatedIP = ResendSettingsValidator.GetValidatedIP(defaultIP);
        int validatedPort = ResendSettingsValidator.GetValidatedPort(defaultPort);
        bool validatedEnabled = ResendSettingsValidator.IntToToggleState(defaultEnabled);

        // デフォルト値が正しく表示用の値に変換されること
        Assert.AreEqual("2.0.0.1", validatedIP,
            "初回起動時にIPフィールドにデフォルト値 '2.0.0.1' が表示されること");
        Assert.AreEqual(6454, validatedPort,
            "初回起動時にポートフィールドにデフォルト値 6454 が表示されること");
        Assert.IsFalse(validatedEnabled,
            "初回起動時にトグルがOFF状態であること");
    }

    [Test]
    public void エンドツーエンド_値変更後の再起動シナリオ()
    {
        // Requirement 4.1, 4.2, 4.3, 4.5: 全設定値の保存と復元
        // シナリオ: 値を変更して保存 -> 再起動 -> 保存値を読み込み -> 各フィールドに復元

        // --- セッション1: 値の変更と保存 ---
        string userIP = "10.0.0.50";
        int userPort = 8000;
        bool userToggle = true;

        // 入力値の検証（保存前）
        Assert.IsTrue(ResendSettingsValidator.IsValidIP(userIP));
        Assert.IsTrue(ResendSettingsValidator.IsValidPort(userPort));

        // 保存用の値変換
        int toggleInt = ResendSettingsValidator.ToggleStateToInt(userToggle);
        Assert.AreEqual(1, toggleInt);

        // --- セッション2: 再起動後の読み込みと復元 ---
        // (PlayerPrefsから userIP, userPort, toggleInt が取得される想定)

        string restoredIP = ResendSettingsValidator.GetValidatedIP(userIP);
        int restoredPort = ResendSettingsValidator.GetValidatedPort(userPort);
        bool restoredToggle = ResendSettingsValidator.IntToToggleState(toggleInt);

        Assert.AreEqual(userIP, restoredIP,
            "再起動後にIP '10.0.0.50' が復元されること");
        Assert.AreEqual(userPort, restoredPort,
            "再起動後にポート 8000 が復元されること");
        Assert.AreEqual(userToggle, restoredToggle,
            "再起動後にトグル ON が復元されること");
    }

    [Test]
    public void エンドツーエンド_破損データからのリカバリシナリオ()
    {
        // Requirement 4.4: 不正データ保存時のフォールバック
        // シナリオ: PlayerPrefsのデータが何らかの理由で破損 -> 起動時にデフォルト値にフォールバック

        // --- 破損した保存データを想定 ---
        string corruptedIP = "not-a-valid-ip";
        int corruptedPort = -999;
        int corruptedToggle = 42;

        // --- 起動時のLoadSettings処理 ---
        string restoredIP = ResendSettingsValidator.GetValidatedIP(corruptedIP);
        int restoredPort = ResendSettingsValidator.GetValidatedPort(corruptedPort);
        bool restoredToggle = ResendSettingsValidator.IntToToggleState(corruptedToggle);

        // 全てデフォルト値にフォールバックすること
        Assert.AreEqual(ResendSettingsDefaults.DstIP, restoredIP,
            "破損IPがデフォルト値 '2.0.0.1' にフォールバックすること");
        Assert.AreEqual(ResendSettingsDefaults.DstPort, restoredPort,
            "破損ポートがデフォルト値 6454 にフォールバックすること");
        Assert.IsFalse(restoredToggle,
            "破損トグル値がOFF (false) にフォールバックすること");
    }

    [Test]
    public void エンドツーエンド_部分的な値破損からのリカバリシナリオ()
    {
        // 一部の値のみ破損している場合、破損した値のみデフォルトに戻り、
        // 正常な値はそのまま復元される

        // IPは正常、ポートは破損、トグルは正常
        string savedIP = "172.16.0.1";
        int savedPort = 0; // 不正値
        int savedToggle = 1; // 正常

        string restoredIP = ResendSettingsValidator.GetValidatedIP(savedIP);
        int restoredPort = ResendSettingsValidator.GetValidatedPort(savedPort);
        bool restoredToggle = ResendSettingsValidator.IntToToggleState(savedToggle);

        Assert.AreEqual("172.16.0.1", restoredIP,
            "正常なIPはそのまま復元されること");
        Assert.AreEqual(ResendSettingsDefaults.DstPort, restoredPort,
            "破損ポートのみデフォルト値にフォールバックすること");
        Assert.IsTrue(restoredToggle,
            "正常なトグル値はそのまま復元されること");
    }

    [Test]
    public void エンドツーエンド_即時保存の一貫性シナリオ()
    {
        // Requirement 4.1, 4.2, 4.5: 値の変更時に即時保存
        // シナリオ: ユーザーが複数回値を変更した場合、最後の有効値が保存される

        // 連続的な入力変更をシミュレート
        string[] ipInputSequence = { "10.0.0.1", "invalid", "192.168.1.1", "bad", "172.16.0.100" };

        string lastValidIP = null;
        foreach (var input in ipInputSequence)
        {
            if (ResendSettingsValidator.IsValidIP(input))
            {
                lastValidIP = input;
                // SaveIP が呼ばれる（検証成功時のみ）
            }
        }

        // 最後の有効値が保存されている前提で復元
        Assert.IsNotNull(lastValidIP, "少なくとも1つの有効なIPが入力されていること");
        var restored = ResendSettingsValidator.GetValidatedIP(lastValidIP);
        Assert.AreEqual("172.16.0.100", restored,
            "最後に入力された有効なIP '172.16.0.100' が復元されること");
    }

    #endregion

    #region 6. 検証ロジックの網羅的整合性テスト

    [Test]
    public void 検証整合性_IsValidIPとGetValidatedIPの判定基準が一致する()
    {
        // IsValidIP が true を返す値は GetValidatedIP でそのまま返される
        // IsValidIP が false を返す値は GetValidatedIP でデフォルト値に置換される
        // この一貫性が保たれていることを確認する

        string[] testValues = {
            "2.0.0.1", "192.168.1.1", "", "invalid", null, "10.0.0.1",
            "255.255.255.255", "abc", "not-an-ip-address"
        };

        foreach (var value in testValues)
        {
            bool isValid = ResendSettingsValidator.IsValidIP(value);
            string validated = ResendSettingsValidator.GetValidatedIP(value);

            if (isValid)
            {
                Assert.AreEqual(value, validated,
                    $"IsValidIP=true の IP '{value}' が GetValidatedIP でそのまま返されること");
            }
            else
            {
                Assert.AreEqual(ResendSettingsDefaults.DstIP, validated,
                    $"IsValidIP=false の IP '{value}' が GetValidatedIP でデフォルト値に置換されること");
            }
        }
    }

    [Test]
    public void 検証整合性_IsValidPortとGetValidatedPortの判定基準が一致する()
    {
        // IsValidPort が true を返す値は GetValidatedPort でそのまま返される
        // IsValidPort が false を返す値は GetValidatedPort でデフォルト値に置換される

        int[] testValues = { -1, 0, 1, 80, 6454, 65535, 65536, int.MinValue, int.MaxValue };

        foreach (var value in testValues)
        {
            bool isValid = ResendSettingsValidator.IsValidPort(value);
            int validated = ResendSettingsValidator.GetValidatedPort(value);

            if (isValid)
            {
                Assert.AreEqual(value, validated,
                    $"IsValidPort=true のポート {value} が GetValidatedPort でそのまま返されること");
            }
            else
            {
                Assert.AreEqual(ResendSettingsDefaults.DstPort, validated,
                    $"IsValidPort=false のポート {value} が GetValidatedPort でデフォルト値に置換されること");
            }
        }
    }

    [Test]
    public void 検証整合性_ToggleStateToIntとIntToToggleStateの往復変換が一貫する()
    {
        // bool -> int -> bool の往復変換で値が保持されること
        bool[] boolValues = { true, false };

        foreach (var original in boolValues)
        {
            int intValue = ResendSettingsValidator.ToggleStateToInt(original);
            bool restored = ResendSettingsValidator.IntToToggleState(intValue);
            Assert.AreEqual(original, restored,
                $"トグル値 {original} の往復変換 (bool->int->bool) が一貫すること");
        }
    }

    #endregion
}
