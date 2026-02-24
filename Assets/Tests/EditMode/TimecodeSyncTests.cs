using System;
using System.Reflection;
using NUnit.Framework;
using ProjectBlue.ArtNetRecorder;

/// <summary>
/// タイムコード同期再生ロジックの統合テスト。
/// タスク 3.5: タイムコード同期再生ロジックをアプリケーション層に統合する
///
/// テスト対象:
/// - TimecodeSyncLogic: タイムコード同期再生の純粋な判定ロジック
/// - ArtNetPlayerApplication: isTimecodeMode フラグ、タイムコード同期関連フィールド・メソッドの存在確認
///
/// Requirements: 3.2, 3.3, 3.5, 3.6
/// </summary>
public class TimecodeSyncTests
{
    #region ArtNetPlayerApplication - isTimecodeMode フラグの存在確認

    [Test]
    public void ArtNetPlayerApplication_HasIsTimecodeModeField()
    {
        // ArtNetPlayerApplication に isTimecodeMode フィールドが存在することを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("isTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "isTimecodeMode フィールドが ArtNetPlayerApplication に存在すること");
    }

    [Test]
    public void ArtNetPlayerApplication_IsTimecodeMode_IsBoolType()
    {
        // isTimecodeMode フィールドの型が bool であることを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("isTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "isTimecodeMode フィールドが存在すること");
        Assert.AreEqual(typeof(bool), fieldInfo.FieldType,
            "isTimecodeMode フィールドの型が bool であること");
    }

    [Test]
    public void ArtNetPlayerApplication_IsTimecodeMode_DefaultIsFalse()
    {
        // isTimecodeMode のデフォルト値が false であることを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("isTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "isTimecodeMode フィールドが存在すること");
        Assert.AreEqual(typeof(bool), fieldInfo.FieldType);
    }

    #endregion

    #region ArtNetPlayerApplication - timecodeReceiver SerializeField の存在確認

    [Test]
    public void ArtNetPlayerApplication_HasTimecodeReceiverField()
    {
        // ArtNetPlayerApplication に timecodeReceiver フィールドが存在することを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("timecodeReceiver",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timecodeReceiver フィールドが ArtNetPlayerApplication に存在すること");
    }

    [Test]
    public void ArtNetPlayerApplication_TimecodeReceiverField_IsCorrectType()
    {
        // timecodeReceiver フィールドの型が ArtNetTimecodeReceiver であることを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("timecodeReceiver",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "フィールドが存在すること");
        Assert.AreEqual(typeof(ArtNetTimecodeReceiver), fieldInfo.FieldType,
            "timecodeReceiver フィールドの型が ArtNetTimecodeReceiver であること");
    }

    [Test]
    public void ArtNetPlayerApplication_TimecodeReceiverField_HasSerializeFieldAttribute()
    {
        // timecodeReceiver フィールドに [SerializeField] 属性が付与されていることを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("timecodeReceiver",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "フィールドが存在すること");

        var attrs = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(attrs.Length > 0,
            "timecodeReceiver フィールドに [SerializeField] 属性が付与されていること");
    }

    #endregion

    #region TimecodeSyncLogic - モード有効化の事前条件検証

    [Test]
    public void CanEnableTimecodeMode_DataLoaded_ReturnsTrue()
    {
        // 録画データがロード済みの場合、タイムコードモードを有効化できる
        var result = TimecodeSyncLogic.CanEnableTimecodeMode(isInitialized: true);

        Assert.IsTrue(result, "録画データがロード済みの場合はモードを有効化できること");
    }

    [Test]
    public void CanEnableTimecodeMode_DataNotLoaded_ReturnsFalse()
    {
        // 録画データがロードされていない場合、タイムコードモードを有効化できない
        var result = TimecodeSyncLogic.CanEnableTimecodeMode(isInitialized: false);

        Assert.IsFalse(result, "録画データがロードされていない場合はモードを有効化できないこと");
    }

    #endregion

    #region TimecodeSyncLogic - タイムアウト時の再生一時停止判定

    [Test]
    public void ShouldPauseOnTimeout_TimedOut_NotAlreadyPaused_ReturnsTrue()
    {
        // タイムアウト状態で、まだ一時停止していない場合は一時停止すべき
        var result = TimecodeSyncLogic.ShouldPauseOnTimeout(
            isTimedOut: true,
            isAlreadyPausedByTimeout: false);

        Assert.IsTrue(result, "タイムアウト状態でまだ一時停止していない場合は一時停止すべきこと");
    }

    [Test]
    public void ShouldPauseOnTimeout_TimedOut_AlreadyPaused_ReturnsFalse()
    {
        // タイムアウト状態で、既に一時停止済みの場合は再度の一時停止は不要
        var result = TimecodeSyncLogic.ShouldPauseOnTimeout(
            isTimedOut: true,
            isAlreadyPausedByTimeout: true);

        Assert.IsFalse(result, "既にタイムアウトによる一時停止済みの場合は再度の一時停止は不要であること");
    }

    [Test]
    public void ShouldPauseOnTimeout_NotTimedOut_ReturnsFalse()
    {
        // タイムアウトでない場合は一時停止しない
        var result = TimecodeSyncLogic.ShouldPauseOnTimeout(
            isTimedOut: false,
            isAlreadyPausedByTimeout: false);

        Assert.IsFalse(result, "タイムアウトでない場合は一時停止しないこと");
    }

    #endregion

    #region TimecodeSyncLogic - タイムアウト回復判定

    [Test]
    public void ShouldResumeFromTimeout_NotTimedOut_WasPaused_ReturnsTrue()
    {
        // タイムアウトが解消され、タイムアウトによる一時停止状態だった場合は回復すべき
        var result = TimecodeSyncLogic.ShouldResumeFromTimeout(
            isTimedOut: false,
            isAlreadyPausedByTimeout: true);

        Assert.IsTrue(result, "タイムアウト解消時に一時停止状態だった場合は回復すべきこと");
    }

    [Test]
    public void ShouldResumeFromTimeout_NotTimedOut_NotPaused_ReturnsFalse()
    {
        // タイムアウトでなく、タイムアウトによる一時停止もしていない場合は回復不要
        var result = TimecodeSyncLogic.ShouldResumeFromTimeout(
            isTimedOut: false,
            isAlreadyPausedByTimeout: false);

        Assert.IsFalse(result, "タイムアウトによる一時停止をしていない場合は回復不要であること");
    }

    [Test]
    public void ShouldResumeFromTimeout_TimedOut_ReturnsFalse()
    {
        // まだタイムアウト中の場合は回復しない
        var result = TimecodeSyncLogic.ShouldResumeFromTimeout(
            isTimedOut: true,
            isAlreadyPausedByTimeout: true);

        Assert.IsFalse(result, "まだタイムアウト中の場合は回復しないこと");
    }

    #endregion

    #region TimecodeSyncLogic - タイムコード値の再生範囲クランプ

    [Test]
    public void ClampTimecodeToPlaybackRange_WithinRange_ReturnsOriginal()
    {
        // タイムコード値が再生範囲内の場合、そのまま返す
        var result = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeMilliseconds: 5000.0, endTimeMilliseconds: 10000.0);

        Assert.AreEqual(5000.0, result, 0.001,
            "再生範囲内のタイムコード値はそのまま返すこと");
    }

    [Test]
    public void ClampTimecodeToPlaybackRange_ExceedsEndTime_ClampsToEndTime()
    {
        // タイムコード値が終端時間を超える場合、終端時間にクランプする
        var result = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeMilliseconds: 15000.0, endTimeMilliseconds: 10000.0);

        Assert.AreEqual(10000.0, result, 0.001,
            "終端時間を超えるタイムコード値は終端時間にクランプすること");
    }

    [Test]
    public void ClampTimecodeToPlaybackRange_NegativeValue_ClampsToZero()
    {
        // タイムコード値が負の場合、0にクランプする
        var result = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeMilliseconds: -100.0, endTimeMilliseconds: 10000.0);

        Assert.AreEqual(0.0, result, 0.001,
            "負のタイムコード値は0にクランプすること");
    }

    [Test]
    public void ClampTimecodeToPlaybackRange_Zero_ReturnsZero()
    {
        // タイムコード値が0の場合、0を返す
        var result = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeMilliseconds: 0.0, endTimeMilliseconds: 10000.0);

        Assert.AreEqual(0.0, result, 0.001,
            "タイムコード値0はそのまま0を返すこと");
    }

    [Test]
    public void ClampTimecodeToPlaybackRange_ExactlyEndTime_ReturnsEndTime()
    {
        // タイムコード値が終端時間と等しい場合、終端時間を返す
        var result = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            timecodeMilliseconds: 10000.0, endTimeMilliseconds: 10000.0);

        Assert.AreEqual(10000.0, result, 0.001,
            "終端時間と等しいタイムコード値はそのまま返すこと");
    }

    #endregion

    #region ArtNetPlayerApplication - isPausedByTimeout フラグの存在確認

    [Test]
    public void ArtNetPlayerApplication_HasIsPausedByTimeoutField()
    {
        // タイムアウトによる一時停止状態を管理するフラグが存在することを確認する
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("isPausedByTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "isPausedByTimeout フィールドが ArtNetPlayerApplication に存在すること");
    }

    [Test]
    public void ArtNetPlayerApplication_IsPausedByTimeout_IsBoolType()
    {
        var fieldInfo = typeof(ArtNetPlayerApplication).GetField("isPausedByTimeout",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "フィールドが存在すること");
        Assert.AreEqual(typeof(bool), fieldInfo.FieldType,
            "isPausedByTimeout フィールドの型が bool であること");
    }

    #endregion

    #region ArtNetPlayerApplication - EnableTimecodeMode / DisableTimecodeMode メソッドの存在確認

    [Test]
    public void ArtNetPlayerApplication_HasEnableTimecodeModeMethod()
    {
        // タイムコードモードを有効化するメソッドが存在することを確認する
        var methodInfo = typeof(ArtNetPlayerApplication).GetMethod("EnableTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "EnableTimecodeMode メソッドが ArtNetPlayerApplication に存在すること");
    }

    [Test]
    public void ArtNetPlayerApplication_HasDisableTimecodeModeMethod()
    {
        // タイムコードモードを無効化するメソッドが存在することを確認する
        var methodInfo = typeof(ArtNetPlayerApplication).GetMethod("DisableTimecodeMode",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "DisableTimecodeMode メソッドが ArtNetPlayerApplication に存在すること");
    }

    #endregion

    #region ArtNetPlayerApplication - Update内タイムコード処理メソッドの存在確認

    [Test]
    public void ArtNetPlayerApplication_HasProcessTimecodeFrameMethod()
    {
        // Update内でタイムコードフレームを処理するメソッドが存在することを確認する
        var methodInfo = typeof(ArtNetPlayerApplication).GetMethod("ProcessTimecodeFrame",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "ProcessTimecodeFrame メソッドが ArtNetPlayerApplication に存在すること");
    }

    #endregion
}
