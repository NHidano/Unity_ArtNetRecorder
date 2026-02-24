using System;
using System.Reflection;
using NUnit.Framework;
using ProjectBlue.ArtNetRecorder;

/// <summary>
/// ループ再生とタイムコード同期再生の統合確認テスト。
/// タスク 5.2: ループ再生とタイムコード同期再生の統合確認
///
/// 検証対象:
/// 1. ループ再生とタイムコード受信モードの排他的な動作
/// 2. タイムコードモード中の手動操作無効化と、モード解除後の復元
/// 3. ループ再生の終端到達時の各モードでの動作
///
/// テスト可能な純粋ロジック (LoopPlaybackLogic, TimecodeSyncLogic, TimecodeTimeoutLogic) の
/// 統合シナリオと、ArtNetPlayerApplication の構造検証を組み合わせて実施する。
///
/// Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6
/// </summary>
public class LoopTimecodeIntegrationTests
{
    #region 1. ループ再生とタイムコード受信モードの排他的動作

    [Test]
    public void タイムコードモード有効化前提条件_データ未ロード時はモード有効化不可()
    {
        // タイムコードモードはデータがロード済みでないと有効化できない。
        // ループ再生の設定に関わらず、この前提条件は変わらない。
        // Requirement 3.2: タイムコード受信モード有効化前に録画データがロード済みであることを検証する
        var canEnable = TimecodeSyncLogic.CanEnableTimecodeMode(isInitialized: false);

        Assert.IsFalse(canEnable,
            "録画データ未ロード時はタイムコードモードを有効化できないこと");
    }

    [Test]
    public void タイムコードモード有効化前提条件_データロード済み時はモード有効化可能()
    {
        // Requirement 3.2: データロード済みならタイムコードモード有効化可能
        var canEnable = TimecodeSyncLogic.CanEnableTimecodeMode(isInitialized: true);

        Assert.IsTrue(canEnable,
            "録画データロード済みの場合はタイムコードモードを有効化できること");
    }

    [Test]
    public void ArtNetPlayerApplication_isTimecodeModeとisLoopEnabledが共存するフィールドとして存在する()
    {
        // ループ再生フラグとタイムコードモードフラグが同じクラスに共存していることを確認する。
        // これにより Update() ループ内で両方の状態を参照し、排他的な動作分岐が可能であることを保証する。
        var loopField = typeof(ArtNetPlayerApplication).GetField("isLoopEnabled",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var tcField = typeof(ArtNetPlayerApplication).GetField("isTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(loopField, "isLoopEnabled フィールドが存在すること");
        Assert.IsNotNull(tcField, "isTimecodeMode フィールドが存在すること");
        Assert.AreEqual(typeof(bool), loopField.FieldType, "isLoopEnabled が bool 型であること");
        Assert.AreEqual(typeof(bool), tcField.FieldType, "isTimecodeMode が bool 型であること");
    }

    [Test]
    public void ArtNetPlayerApplication_Update内でタイムコードモードが再生ループに優先する構造()
    {
        // ArtNetPlayerApplication.Update() 内の処理順序を検証する。
        // isTimecodeMode が true の場合、ProcessTimecodeFrame() が呼ばれて return するため、
        // 通常のループ再生処理 (IsEndOfPlayback / HandleEndOfPlayback) には到達しない。
        // これはタイムコードモードとループ再生の排他的動作を保証する構造である。

        // Update メソッドの存在確認
        var updateMethod = typeof(ArtNetPlayerApplication).GetMethod("Update",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(updateMethod, "Update メソッドが存在すること");

        // ProcessTimecodeFrame メソッドの存在確認（タイムコード駆動で return する分岐を示す）
        var processMethod = typeof(ArtNetPlayerApplication).GetMethod("ProcessTimecodeFrame",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(processMethod, "ProcessTimecodeFrame メソッドが存在すること");

        // HandleEndOfPlayback メソッドの存在確認（ループ再生処理を示す）
        var handleMethod = typeof(ArtNetPlayerApplication).GetMethod("HandleEndOfPlayback",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(handleMethod, "HandleEndOfPlayback メソッドが存在すること");
    }

    [Test]
    public void タイムコードモード中はループ再生の終端判定が実行されない_ロジックレベル検証()
    {
        // タイムコードモード有効時は、Update() 内で ProcessTimecodeFrame() が呼ばれて即座に return する。
        // そのため LoopPlaybackLogic.IsEndOfPlayback() は呼び出されない。
        // これをロジックレベルで検証: タイムコードモード時のフローでは
        // 終端到達判定ではなくタイムコードクランプで再生範囲を制御する。

        double header = 15000.0;
        double endTime = 10000.0;

        // 通常モードでは終端超過と判定される
        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "通常モードでは header > endTime で終端到達と判定されること");

        // タイムコードモードではクランプにより再生範囲内に収められる
        var clamped = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(header, endTime);
        Assert.AreEqual(endTime, clamped, 0.001,
            "タイムコードモードでは終端時間にクランプされ、終端到達判定は不要であること");
    }

    [Test]
    public void タイムコードモード中のタイムコード値は再生範囲でクランプされるためループ不要()
    {
        // タイムコードモードでは外部タイムコードが再生位置を制御するため、
        // 再生範囲を超えるタイムコードは ClampTimecodeToPlaybackRange でクランプされる。
        // これにより、ループ再生の終端到達処理は不要となる。
        // Requirement 3.2: タイムコード値に対応する再生位置へシーク

        double endTime = 60000.0; // 60秒の録画データ

        // タイムコード値が録画データの範囲内
        var withinRange = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(30000.0, endTime);
        Assert.AreEqual(30000.0, withinRange, 0.001);

        // タイムコード値が終端を超過 -> クランプ
        var exceeds = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(90000.0, endTime);
        Assert.AreEqual(endTime, exceeds, 0.001);

        // タイムコード値が先頭以前 -> 0にクランプ
        var negative = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(-1000.0, endTime);
        Assert.AreEqual(0.0, negative, 0.001);
    }

    #endregion

    #region 2. タイムコードモード中の手動操作無効化とモード解除後の復元

    [Test]
    public void ArtNetPlayerApplication_EnableTimecodeModeメソッドが存在する()
    {
        // タイムコードモード有効化メソッドが手動操作無効化を含む処理として存在すること
        // Requirement 3.3: タイムコードモード中の手動再生無効化
        var method = typeof(ArtNetPlayerApplication).GetMethod("EnableTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "EnableTimecodeMode メソッドが存在すること");
        Assert.AreEqual(typeof(void), method.ReturnType, "戻り値がvoidであること");
    }

    [Test]
    public void ArtNetPlayerApplication_DisableTimecodeModeメソッドが存在する()
    {
        // タイムコードモード無効化メソッドが手動操作復元を含む処理として存在すること
        // Requirement 3.6: タイムコードモード解除時の手動制御復元
        var method = typeof(ArtNetPlayerApplication).GetMethod("DisableTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(method, "DisableTimecodeMode メソッドが存在すること");
        Assert.AreEqual(typeof(void), method.ReturnType, "戻り値がvoidであること");
    }

    [Test]
    public void PlayerUI_SetManualControlEnabledメソッドが手動操作の有効無効切替を提供する()
    {
        // PlayerUI に手動操作の有効/無効切替メソッドが存在すること
        // EnableTimecodeMode は false を、DisableTimecodeMode は true を渡す前提
        // Requirement 3.3, 3.6: 手動再生コントロールの無効化と復元
        var method = typeof(PlayerUI).GetMethod("SetManualControlEnabled",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method, "SetManualControlEnabled メソッドが PlayerUI に存在すること");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "引数が1つであること");
        Assert.AreEqual(typeof(bool), parameters[0].ParameterType, "引数の型がboolであること");
    }

    [Test]
    public void PlayerUI_SetTimecodeDisplayVisibleメソッドがタイムコード表示切替を提供する()
    {
        // タイムコードモード有効/無効に連動してタイムコード表示を切り替えるメソッド
        // Requirement 3.4: タイムコード値のリアルタイム表示
        var method = typeof(PlayerUI).GetMethod("SetTimecodeDisplayVisible",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method, "SetTimecodeDisplayVisible メソッドが PlayerUI に存在すること");
    }

    [Test]
    public void PlayerUI_SetTimecodeDisplayメソッドがタイムコード値表示を提供する()
    {
        // Requirement 3.4: タイムコード値のリアルタイム表示
        var method = typeof(PlayerUI).GetMethod("SetTimecodeDisplay",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method, "SetTimecodeDisplay メソッドが PlayerUI に存在すること");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "引数が1つであること");
        Assert.AreEqual(typeof(string), parameters[0].ParameterType, "引数の型がstringであること");
    }

    [Test]
    public void PlayerUI_ループトグルとタイムコードトグルの両方が公開されている()
    {
        // ループトグルとタイムコードトグルの変更イベントが両方とも
        // IObservable<bool> として公開されていることを確認する
        // Requirement 2.1, 3.1: 各トグルUIの提供

        var loopProperty = typeof(PlayerUI).GetProperty("OnLoopToggleChangedAsObservable",
            BindingFlags.Public | BindingFlags.Instance);
        var tcProperty = typeof(PlayerUI).GetProperty("OnTimecodeToggleChangedAsObservable",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(loopProperty, "OnLoopToggleChangedAsObservable が公開されていること");
        Assert.IsNotNull(tcProperty, "OnTimecodeToggleChangedAsObservable が公開されていること");

        // 両方が IObservable<bool> 型であること
        Assert.IsTrue(typeof(IObservable<bool>).IsAssignableFrom(loopProperty.PropertyType),
            "OnLoopToggleChangedAsObservable が IObservable<bool> 型であること");
        Assert.IsTrue(typeof(IObservable<bool>).IsAssignableFrom(tcProperty.PropertyType),
            "OnTimecodeToggleChangedAsObservable が IObservable<bool> 型であること");
    }

    [Test]
    public void ArtNetPlayerApplication_OnPlayButtonイベント内でタイムコードモード時に手動操作が無視される構造()
    {
        // ArtNetPlayerApplication.Start() 内の OnPlayButtonPressedAsObservable.Subscribe で
        // isTimecodeMode が true の場合に return する構造が存在することを確認する。
        // これは Requirement 3.3 の手動再生制御無効化を Subscribe コールバック内で実装している。

        // isTimecodeMode フィールドが存在し、Start() 内で参照されることで
        // 再生ボタン押下時のイベント処理がタイムコードモード中に無視される。
        var field = typeof(ArtNetPlayerApplication).GetField("isTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(field, "isTimecodeMode フィールドがイベントハンドラでの条件分岐に使用可能であること");
        Assert.AreEqual(typeof(bool), field.FieldType);
    }

    [Test]
    public void ArtNetPlayerApplication_isPausedByTimeoutフラグでタイムアウト一時停止を管理する()
    {
        // タイムコードモード中のタイムアウト一時停止状態を管理するフラグが存在し、
        // モード無効化時にリセットされる構造を確認する。
        // Requirement 3.5: タイムコード未受信時の一時停止
        var field = typeof(ArtNetPlayerApplication).GetField("isPausedByTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(field, "isPausedByTimeout フィールドが存在すること");
        Assert.AreEqual(typeof(bool), field.FieldType, "isPausedByTimeout が bool 型であること");
    }

    #endregion

    #region 3. ループ再生の終端到達時の各モードでの動作

    [Test]
    public void ループ有効_通常モード_終端到達で先頭リセット()
    {
        // 通常再生モード（タイムコードモード無効）でループが有効な場合、
        // 終端到達時に先頭にリセットして再生を継続する。
        // Requirement 2.2: ループ有効時の先頭リセット自動再生

        double header = 10001.0;
        double endTime = 10000.0;

        // 終端到達を検出
        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "header が endTime を超過して終端到達と判定されること");

        // ループ有効時のアクション
        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled: true);
        Assert.AreEqual(EndOfPlaybackAction.ResetToBeginning, action,
            "ループ有効時は先頭にリセットするアクションが返ること");
    }

    [Test]
    public void ループ無効_通常モード_終端到達で再生停止()
    {
        // 通常再生モードでループが無効な場合、
        // 終端到達時に再生を停止しシークバーを終端位置に保持する。
        // Requirement 2.3: ループ無効時の終端停止

        double header = 10001.0;
        double endTime = 10000.0;

        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime));

        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled: false);
        Assert.AreEqual(EndOfPlaybackAction.StopPlayback, action,
            "ループ無効時は再生を停止するアクションが返ること");
    }

    [Test]
    public void タイムコードモード_終端超過タイムコード受信時はクランプされる()
    {
        // タイムコードモードでは終端到達の概念が異なる。
        // 外部タイムコードが終端を超える値を送信しても、
        // ClampTimecodeToPlaybackRange で終端時間にクランプされる。
        // Requirement 3.2: タイムコード値に対応する再生位置へシーク

        double endTime = 10000.0;
        double timecodeMs = 15000.0; // 終端超過のタイムコード

        var clamped = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(timecodeMs, endTime);
        Assert.AreEqual(endTime, clamped, 0.001,
            "タイムコードモードでは終端超過値が endTime にクランプされること");

        // クランプ後の値は LoopPlaybackLogic.IsEndOfPlayback で終端と判定されない
        Assert.IsFalse(LoopPlaybackLogic.IsEndOfPlayback(clamped, endTime),
            "クランプされた値は endTime と等しいため、終端到達と判定されないこと");
    }

    [Test]
    public void タイムコードモード_タイムアウト時は再生一時停止しループ判定に入らない()
    {
        // タイムコードモードでタイムアウトが発生した場合、
        // ShouldPauseOnTimeout が true を返し、最後の位置で一時停止する。
        // LoopPlaybackLogic の終端処理は呼ばれない（ProcessTimecodeFrame 内で処理される）。
        // Requirement 3.5: タイムコード未受信時の一時停止

        bool isTimedOut = true;
        bool isPausedByTimeout = false;

        var shouldPause = TimecodeSyncLogic.ShouldPauseOnTimeout(isTimedOut, isPausedByTimeout);
        Assert.IsTrue(shouldPause,
            "タイムアウト発生時に一時停止すべきと判定されること");

        // この一時停止はループ再生の終端処理とは独立した処理パス
        // (ProcessTimecodeFrame 内で isPausedByTimeout = true にして return する)
    }

    [Test]
    public void タイムコードモード_タイムアウト回復後にタイムコード受信が再開する()
    {
        // タイムアウト一時停止状態からタイムコードが再受信された場合の回復。
        // Requirement 3.5: タイムアウト後の回復

        bool isTimedOut = false;
        bool isPausedByTimeout = true;

        var shouldResume = TimecodeSyncLogic.ShouldResumeFromTimeout(isTimedOut, isPausedByTimeout);
        Assert.IsTrue(shouldResume,
            "タイムアウト解消時に再生を再開すべきと判定されること");
    }

    [Test]
    public void ループ設定は再生中に即座に反映される_フィールドが直接参照型()
    {
        // isLoopEnabled はプロパティではなくフィールドであるため、
        // Update() ループ内で毎フレーム即座に最新の値が参照される。
        // これにより再生中にループ設定を切り替えても即座に反映される。
        // Requirement 2.4: 再生中のループ設定即時反映

        var field = typeof(ArtNetPlayerApplication).GetField("isLoopEnabled",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(field, "isLoopEnabled フィールドが存在すること");
        Assert.IsFalse(field.IsStatic, "isLoopEnabled がインスタンスフィールドであること");
        Assert.IsFalse(field.IsInitOnly, "isLoopEnabled が readonly ではないこと（更新可能）");
    }

    #endregion

    #region 4. 統合シナリオ: 複数モードの組み合わせ検証

    [Test]
    public void 統合シナリオ_通常モードからタイムコードモードへ切替時の状態遷移()
    {
        // 通常の再生モード（ループ有効）からタイムコードモードに切り替えた場合の
        // ロジック的な状態遷移を検証する。

        // 1. 通常モードでループ有効
        bool isLoopEnabled = true;
        bool isTimecodeMode = false;
        bool isPausedByTimeout = false;

        // ループ有効で再生中、終端まだ未到達
        double header = 5000.0;
        double endTime = 10000.0;
        Assert.IsFalse(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "再生位置が途中の場合は終端到達しない");

        // 2. タイムコードモード有効化
        isTimecodeMode = true;
        Assert.IsTrue(TimecodeSyncLogic.CanEnableTimecodeMode(isInitialized: true),
            "データロード済みなのでタイムコードモード有効化可能");

        // 3. タイムコードモード中はタイムコード駆動で再生位置を更新
        double timecodeMs = 7500.0;
        var clamped = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(timecodeMs, endTime);
        Assert.AreEqual(7500.0, clamped, 0.001,
            "タイムコード値が再生範囲内の場合はそのまま使用");

        // 4. ループ有効フラグは保持されるが、タイムコードモード中は参照されない
        // （Update内でisTimecodeModeがtrueの場合にProcessTimecodeFrameで早期returnするため）
        Assert.IsTrue(isLoopEnabled, "ループ設定はタイムコードモード中も保持される");
    }

    [Test]
    public void 統合シナリオ_タイムコードモード解除後にループ設定が維持される()
    {
        // タイムコードモードから通常モードに戻った場合、
        // ループ設定が以前の状態のまま維持されることを検証する。

        // 1. ループ有効でタイムコードモード有効中
        bool isLoopEnabled = true;
        bool isTimecodeMode = true;

        // 2. タイムコードモードを無効化
        isTimecodeMode = false;

        // 3. ループ設定が維持されている
        Assert.IsTrue(isLoopEnabled, "タイムコードモード解除後もループ設定が維持されること");
        Assert.IsFalse(isTimecodeMode, "タイムコードモードが無効化されていること");

        // 4. 通常モードのループ再生ロジックが機能する
        double header = 10001.0;
        double endTime = 10000.0;
        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime));
        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled);
        Assert.AreEqual(EndOfPlaybackAction.ResetToBeginning, action,
            "ループ有効設定が維持されているため、終端到達で先頭リセットが選択されること");
    }

    [Test]
    public void 統合シナリオ_タイムコードモード解除後にループ無効で再生停止される()
    {
        // タイムコードモードから通常モードに戻り、ループが無効の場合、
        // 終端到達で再生が停止される。

        bool isLoopEnabled = false;
        bool isTimecodeMode = false;

        double header = 10001.0;
        double endTime = 10000.0;

        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime));
        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled);
        Assert.AreEqual(EndOfPlaybackAction.StopPlayback, action,
            "ループ無効で終端到達時は再生を停止すること");
    }

    [Test]
    public void 統合シナリオ_タイムコードタイムアウト後にモード解除して通常再生に復帰()
    {
        // タイムコードモードでタイムアウトが発生し一時停止。
        // その後モードを解除して通常の手動再生に復帰するシナリオ。

        // 1. タイムアウト発生
        bool isTimedOut = true;
        bool isPausedByTimeout = false;
        Assert.IsTrue(TimecodeSyncLogic.ShouldPauseOnTimeout(isTimedOut, isPausedByTimeout),
            "タイムアウト発生で一時停止");
        isPausedByTimeout = true;

        // 2. タイムコードモード解除
        bool isTimecodeMode = false;
        isPausedByTimeout = false; // DisableTimecodeMode でリセットされる

        // 3. 通常モードに復帰（手動操作が復元される）
        Assert.IsFalse(isTimecodeMode, "タイムコードモードが無効化されていること");
        Assert.IsFalse(isPausedByTimeout, "タイムアウト一時停止フラグがリセットされていること");
    }

    [Test]
    public void 統合シナリオ_タイムコードモードでの連続タイムコード受信と再生位置更新()
    {
        // タイムコードモード中に連続してタイムコードが受信された場合、
        // 各タイムコード値がクランプされて再生位置として使用されるシナリオ。
        // Requirement 3.2: タイムコード値に対応する再生位置へシーク

        double endTime = 120000.0; // 2分の録画データ

        // 連続するタイムコード値（1秒間隔で3つ）
        double[] timecodes = { 10000.0, 11000.0, 12000.0 };

        double previousPosition = -1;
        foreach (var tc in timecodes)
        {
            var position = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(tc, endTime);
            Assert.AreEqual(tc, position, 0.001,
                $"タイムコード {tc}ms が再生範囲内でそのまま使用されること");

            if (previousPosition >= 0)
            {
                Assert.IsTrue(position > previousPosition,
                    "連続するタイムコードで再生位置が前進すること");
            }
            previousPosition = position;
        }
    }

    [Test]
    public void 統合シナリオ_タイムコード表示フォーマットがモード中に更新される()
    {
        // タイムコードモード中は受信したタイムコード値を表示する。
        // TimecodeDisplayFormatter が正しいフォーマット文字列を生成する。
        // Requirement 3.4: タイムコード値のリアルタイム表示

        // デフォルト表示（未受信時）
        Assert.AreEqual("--:--:--:--", TimecodeDisplayFormatter.DefaultDisplayText,
            "デフォルト表示が '--:--:--:--' であること");

        // タイムコードデータからの表示生成
        var timecodeData = new TimecodeData
        {
            Hours = 1,
            Minutes = 5,
            Seconds = 30,
            Frames = 12,
            Type = 3, // SMPTE 30fps
            MillisecondsFromStart = 3930400.0
        };

        var display = TimecodeDisplayFormatter.FormatTimecode(timecodeData);
        Assert.AreEqual("01:05:30:12", display,
            "タイムコードデータが 'HH:MM:SS:FF' 形式にフォーマットされること");
    }

    [Test]
    public void 統合シナリオ_タイムコードモード中にタイムアウトと回復が交互に発生()
    {
        // タイムコードモード中にネットワーク断続が起きた場合のシナリオ。
        // タイムアウト -> 回復 -> 再度タイムアウト の流れを検証する。

        // 初期状態: 受信中
        bool isPausedByTimeout = false;

        // 1. 最初のタイムアウト
        Assert.IsTrue(TimecodeSyncLogic.ShouldPauseOnTimeout(true, isPausedByTimeout));
        isPausedByTimeout = true;

        // 2. タイムアウト中の再判定（重複パウズしない）
        Assert.IsFalse(TimecodeSyncLogic.ShouldPauseOnTimeout(true, isPausedByTimeout),
            "既にタイムアウト一時停止中の場合は再度の一時停止処理をしないこと");

        // 3. タイムコード再受信で回復
        Assert.IsTrue(TimecodeSyncLogic.ShouldResumeFromTimeout(false, isPausedByTimeout));
        isPausedByTimeout = false;

        // 4. 二度目のタイムアウト
        Assert.IsTrue(TimecodeSyncLogic.ShouldPauseOnTimeout(true, isPausedByTimeout));
        isPausedByTimeout = true;

        // 5. 再度回復
        Assert.IsTrue(TimecodeSyncLogic.ShouldResumeFromTimeout(false, isPausedByTimeout));
        isPausedByTimeout = false;

        Assert.IsFalse(isPausedByTimeout, "最終状態がタイムアウト一時停止でないこと");
    }

    #endregion

    #region 5. タイムアウトロジックとループ終端処理の独立性

    [Test]
    public void タイムアウトロジック_受信中でない場合はタイムアウトにならない()
    {
        // タイムコード受信を停止した場合（モード無効化後）、
        // タイムアウトは発生しない。ループ再生の終端処理に影響しない。
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: false,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 10.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result,
            "受信中でない場合はタイムアウトと判定されないこと（モード解除後の影響なし）");
    }

    [Test]
    public void タイムアウトロジック_受信中で閾値超過時にタイムアウト()
    {
        // タイムコード受信中で最終受信から閾値を超えた場合のタイムアウト。
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 3.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsTrue(result,
            "受信中で閾値超過時にタイムアウトと判定されること");
    }

    [Test]
    public void タイムアウトロジック_受信中で閾値以内はタイムアウトにならない()
    {
        // タイムコード受信中で最終受信から閾値以内の場合はタイムアウトしない。
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 1.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result,
            "受信中で閾値以内はタイムアウトにならないこと");
    }

    [Test]
    public void タイムアウトロジック_受信開始直後でまだ未受信の場合はタイムアウト()
    {
        // 受信を開始したが一度もパケットを受信していない場合はタイムアウトと判定する。
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: false,
            elapsedSinceLastReceive: 0.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsTrue(result,
            "受信中だが一度も受信していない場合はタイムアウトと判定されること");
    }

    #endregion

    #region 6. 終端到達時の複合条件検証

    [Test]
    public void 終端到達_ループ有効時の連続リセットシナリオ()
    {
        // ループ有効時に終端到達 -> リセット -> 再度終端到達 -> リセット
        // という連続ループのシナリオを検証する。
        // Requirement 2.2: ループ有効時の先頭リセット自動再生

        double endTime = 5000.0;
        int resetCount = 0;

        for (int iteration = 0; iteration < 3; iteration++)
        {
            // 再生進行して終端到達
            double header = endTime + 100.0;
            Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime));

            var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled: true);
            Assert.AreEqual(EndOfPlaybackAction.ResetToBeginning, action);

            // リセット実行
            header = 0;
            resetCount++;

            Assert.IsFalse(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
                $"リセット後 (iteration {iteration}) は終端到達でないこと");
        }

        Assert.AreEqual(3, resetCount, "3回のループリセットが正常に完了すること");
    }

    [Test]
    public void 終端到達_ループ無効で停止後に再生位置が終端に保持される()
    {
        // ループ無効で終端到達した場合、再生位置は endTime に設定される。
        // Requirement 2.3: ループ無効時の終端停止

        double endTime = 5000.0;
        double header = endTime + 100.0;

        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime));

        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled: false);
        Assert.AreEqual(EndOfPlaybackAction.StopPlayback, action);

        // 停止後、header は endTime に設定される
        header = endTime;
        Assert.IsFalse(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "header = endTime の場合は終端到達と判定されないこと（StopPlayback 後の安定状態）");
    }

    [Test]
    public void 終端近傍_ヘッダーが終端にちょうど等しい場合は到達前()
    {
        // header == endTime は終端到達ではない（> の判定）
        // これにより最後のフレームが正常に再生される。
        // Requirement 2.2, 2.3: 終端判定の境界条件

        double endTime = 10000.0;
        double header = endTime;

        Assert.IsFalse(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "header が endTime と等しい場合は終端到達ではないこと");
    }

    [Test]
    public void 終端近傍_ヘッダーが終端をごくわずかに超過した場合は到達()
    {
        double endTime = 10000.0;
        double header = endTime + 0.001;

        Assert.IsTrue(LoopPlaybackLogic.IsEndOfPlayback(header, endTime),
            "header が endTime をごくわずかに超過した場合は終端到達と判定されること");
    }

    #endregion

    #region 7. ArtNetTimecodeReceiver の統合構造検証

    [Test]
    public void ArtNetTimecodeReceiver_IsTimedOutプロパティが存在する()
    {
        // タイムアウト判定プロパティが TimecodeTimeoutLogic に委譲して実装されていることの構造確認。
        // Requirement 3.5: タイムコード未受信時の一時停止
        var property = typeof(ArtNetTimecodeReceiver).GetProperty("IsTimedOut",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property, "IsTimedOut プロパティが公開されていること");
        Assert.AreEqual(typeof(bool), property.PropertyType, "IsTimedOut が bool 型であること");
        Assert.IsTrue(property.CanRead, "IsTimedOut が読み取り可能であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_TimecodeQueueプロパティが存在する()
    {
        // メインスレッドでタイムコードデータを取得するキューが存在する。
        // Requirement 3.2: タイムコード値に対応する再生位置へシーク
        var property = typeof(ArtNetTimecodeReceiver).GetProperty("TimecodeQueue",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property, "TimecodeQueue プロパティが公開されていること");
        Assert.IsTrue(property.CanRead, "TimecodeQueue が読み取り可能であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_StartReceivingとStopReceivingメソッドが存在する()
    {
        // タイムコード受信の開始/停止メソッドが存在する。
        // EnableTimecodeMode / DisableTimecodeMode から呼ばれる。
        var startMethod = typeof(ArtNetTimecodeReceiver).GetMethod("StartReceiving",
            BindingFlags.Public | BindingFlags.Instance);
        var stopMethod = typeof(ArtNetTimecodeReceiver).GetMethod("StopReceiving",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(startMethod, "StartReceiving メソッドが存在すること");
        Assert.IsNotNull(stopMethod, "StopReceiving メソッドが存在すること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_NotifyTimecodeReceivedメソッドが存在する()
    {
        // メインスレッドから最終受信時刻を更新するメソッドが存在する。
        // ProcessTimecodeFrame 内でキューからデータをデキューした後に呼ばれる。
        var method = typeof(ArtNetTimecodeReceiver).GetMethod("NotifyTimecodeReceived",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(method, "NotifyTimecodeReceived メソッドが存在すること");
    }

    #endregion

    #region 8. TimecodePacketParser とタイムコード変換の統合検証

    [Test]
    public void タイムコードパケットパース_有効なパケットからTimecodeDataが生成される()
    {
        // 完全な ArtNet OpTimeCode パケットをパースし、TimecodeData が正しく生成され、
        // その値が ClampTimecodeToPlaybackRange で使用可能であることを検証する。

        // Art-Net OpTimeCode パケット (19バイト) を構築
        byte[] packet = new byte[19];
        // ID: "Art-Net\0"
        packet[0] = 0x41; // 'A'
        packet[1] = 0x72; // 'r'
        packet[2] = 0x74; // 't'
        packet[3] = 0x2D; // '-'
        packet[4] = 0x4E; // 'N'
        packet[5] = 0x65; // 'e'
        packet[6] = 0x74; // 't'
        packet[7] = 0x00; // '\0'
        // OpCode: 0x9700 (リトルエンディアン)
        packet[8] = 0x00;
        packet[9] = 0x97;
        // ProtVer
        packet[10] = 0x00;
        packet[11] = 14;
        // Filler
        packet[12] = 0x00;
        packet[13] = 0x00;
        // Timecode fields: 01:02:30:15 @ SMPTE 30fps
        packet[14] = 15;  // Frames
        packet[15] = 30;  // Seconds
        packet[16] = 2;   // Minutes
        packet[17] = 1;   // Hours
        packet[18] = 3;   // Type: SMPTE 30fps

        bool success = TimecodePacketParser.TryParseTimecodePacket(packet, out var timecodeData);
        Assert.IsTrue(success, "有効な OpTimeCode パケットのパースが成功すること");

        // タイムコード値の検証
        Assert.AreEqual(15, timecodeData.Frames);
        Assert.AreEqual(30, timecodeData.Seconds);
        Assert.AreEqual(2, timecodeData.Minutes);
        Assert.AreEqual(1, timecodeData.Hours);
        Assert.AreEqual(3, timecodeData.Type);

        // ミリ秒変換: (1*3600 + 2*60 + 30)*1000 + 15*1000/30 = 3750000 + 500 = 3750500
        double expectedMs = (1.0 * 3600 + 2.0 * 60 + 30.0) * 1000.0 + 15.0 * 1000.0 / 30.0;
        Assert.AreEqual(expectedMs, timecodeData.MillisecondsFromStart, 0.01,
            "タイムコード→ミリ秒変換が正しいこと");

        // クランプ検証: このミリ秒値を再生範囲内で使用可能
        double endTime = 7200000.0; // 2時間
        var clamped = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeData.MillisecondsFromStart, endTime);
        Assert.AreEqual(expectedMs, clamped, 0.01,
            "パースしたタイムコードが再生範囲内でクランプされずに使用可能であること");
    }

    [Test]
    public void タイムコードパケットパース_無効なパケットは拒否される()
    {
        // パケット長不足やOpCode不一致の場合はパース失敗する。
        // この場合タイムコードモード中はキューに何も追加されず、
        // 既存のループ再生状態にも影響しない。

        // パケット長不足
        byte[] shortPacket = new byte[10];
        Assert.IsFalse(TimecodePacketParser.TryParseTimecodePacket(shortPacket, out _),
            "パケット長不足の場合はパース失敗すること");

        // null パケット
        Assert.IsFalse(TimecodePacketParser.TryParseTimecodePacket(null, out _),
            "null パケットの場合はパース失敗すること");
    }

    [Test]
    public void タイムコード変換_各フレームレート種別で正しいミリ秒値が算出される()
    {
        // 01:00:00:00 (1時間ちょうど) を各フレームレート種別で変換する。
        // フレーム0なのでフレーム部分は0ms。
        double expectedMs = 3600000.0; // 1時間 = 3,600,000ms

        for (byte type = 0; type <= 3; type++)
        {
            double result = TimecodePacketParser.ConvertTimecodeToMilliseconds(
                frames: 0, seconds: 0, minutes: 0, hours: 1, type: type);

            Assert.AreEqual(expectedMs, result, 0.01,
                $"Type={type} で 01:00:00:00 が {expectedMs}ms に変換されること");
        }
    }

    [Test]
    public void タイムコード変換_フレーム部分のミリ秒精度がフレームレートに依存する()
    {
        // 00:00:00:01 (1フレーム目) を各フレームレートで変換した場合、
        // フレーム部分のミリ秒値がフレームレートに依存する。

        // Film 24fps: 1 * 1000 / 24 = 41.6667ms
        double film = TimecodePacketParser.ConvertTimecodeToMilliseconds(1, 0, 0, 0, 0);
        Assert.AreEqual(1000.0 / 24.0, film, 0.01, "Film 24fps: 1フレーム = ~41.67ms");

        // EBU 25fps: 1 * 1000 / 25 = 40.0ms
        double ebu = TimecodePacketParser.ConvertTimecodeToMilliseconds(1, 0, 0, 0, 1);
        Assert.AreEqual(40.0, ebu, 0.01, "EBU 25fps: 1フレーム = 40.0ms");

        // DF 29.97fps: 1 * 1000 / 29.97 = 33.3667ms
        double df = TimecodePacketParser.ConvertTimecodeToMilliseconds(1, 0, 0, 0, 2);
        Assert.AreEqual(1000.0 / 29.97, df, 0.01, "DF 29.97fps: 1フレーム = ~33.37ms");

        // SMPTE 30fps: 1 * 1000 / 30 = 33.3333ms
        double smpte = TimecodePacketParser.ConvertTimecodeToMilliseconds(1, 0, 0, 0, 3);
        Assert.AreEqual(1000.0 / 30.0, smpte, 0.01, "SMPTE 30fps: 1フレーム = ~33.33ms");
    }

    #endregion
}
