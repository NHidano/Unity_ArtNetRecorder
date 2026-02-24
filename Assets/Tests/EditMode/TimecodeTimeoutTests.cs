using System.Reflection;
using NUnit.Framework;
using ProjectBlue.ArtNetRecorder;

/// <summary>
/// タイムコード受信タイムアウト検出に関するテスト。
/// タスク 3.3: タイムコード受信タイムアウト検出を実装する
///
/// テスト対象:
/// - TimecodeTimeoutLogic: タイムアウト判定の純粋ロジック
/// - ArtNetTimecodeReceiver: タイムアウト関連フィールド・プロパティの存在確認
///
/// 要件 3.5: タイムコード未受信時の一時停止
/// - 最終受信からの経過時間を監視し、閾値（デフォルト2秒）を超えた場合にタイムアウト状態を公開する
/// - タイムアウト閾値をInspectorから調整可能にする
/// </summary>
public class TimecodeTimeoutTests
{
    #region TimecodeTimeoutLogic - タイムアウト判定の純粋ロジック

    [Test]
    public void IsTimedOut_ElapsedExceedsThreshold_ReturnsTrue()
    {
        // 経過時間が閾値を超えた場合、タイムアウトと判定する
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 2.5f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsTrue(result, "経過時間が閾値を超えた場合はタイムアウトと判定すること");
    }

    [Test]
    public void IsTimedOut_ElapsedEqualsThreshold_ReturnsFalse()
    {
        // 経過時間が閾値と等しい場合、タイムアウトではない
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 2.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "経過時間が閾値と等しい場合はタイムアウトと判定しないこと");
    }

    [Test]
    public void IsTimedOut_ElapsedLessThanThreshold_ReturnsFalse()
    {
        // 経過時間が閾値未満の場合、タイムアウトではない
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 1.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "経過時間が閾値未満の場合はタイムアウトと判定しないこと");
    }

    [Test]
    public void IsTimedOut_ZeroElapsed_ReturnsFalse()
    {
        // 経過時間が0の場合、タイムアウトではない
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "経過時間が0の場合はタイムアウトと判定しないこと");
    }

    [Test]
    public void IsTimedOut_CustomThreshold_RespectedCorrectly()
    {
        // カスタム閾値（5秒）が正しく適用されることを確認
        var resultNotTimedOut = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 4.9f,
            timeoutThresholdSeconds: 5.0f);

        var resultTimedOut = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 5.1f,
            timeoutThresholdSeconds: 5.0f);

        Assert.IsFalse(resultNotTimedOut, "閾値5秒に対して4.9秒はタイムアウトではないこと");
        Assert.IsTrue(resultTimedOut, "閾値5秒に対して5.1秒はタイムアウトであること");
    }

    [Test]
    public void IsTimedOut_VerySmallThreshold_Works()
    {
        // 非常に小さい閾値（0.1秒）でもタイムアウト判定が正しく動作する
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: 0.2f,
            timeoutThresholdSeconds: 0.1f);

        Assert.IsTrue(result, "閾値0.1秒に対して0.2秒はタイムアウトであること");
    }

    [Test]
    public void IsTimedOut_NegativeElapsed_ReturnsFalse()
    {
        // 負の経過時間（まだ受信していない状態のエッジケース）はタイムアウトではない
        // ただし、受信開始後に一度も受信していない場合の判定は別のメソッドで行う
        var result = TimecodeTimeoutLogic.IsTimedOut(
            elapsedSeconds: -1.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "負の経過時間はタイムアウトと判定しないこと");
    }

    #endregion

    #region TimecodeTimeoutLogic - 受信状態の総合判定

    [Test]
    public void DetermineTimeoutState_NotReceiving_ReturnsNotTimedOut()
    {
        // 受信中でない場合、タイムアウト状態にはならない
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: false,
            hasReceivedAtLeastOnce: false,
            elapsedSinceLastReceive: 10.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "受信中でない場合はタイムアウトと判定しないこと");
    }

    [Test]
    public void DetermineTimeoutState_ReceivingNeverReceived_ReturnsTimedOut()
    {
        // 受信中だが一度も受信していない場合、タイムアウトと判定する
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: false,
            elapsedSinceLastReceive: 0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsTrue(result, "受信中だが一度も受信していない場合はタイムアウトと判定すること");
    }

    [Test]
    public void DetermineTimeoutState_ReceivingWithinThreshold_ReturnsNotTimedOut()
    {
        // 受信中で、最終受信からの経過時間が閾値内の場合、タイムアウトではない
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 1.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "最終受信からの経過時間が閾値内の場合はタイムアウトと判定しないこと");
    }

    [Test]
    public void DetermineTimeoutState_ReceivingExceedsThreshold_ReturnsTimedOut()
    {
        // 受信中で、最終受信からの経過時間が閾値を超えた場合、タイムアウトと判定する
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 3.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsTrue(result, "最終受信からの経過時間が閾値を超えた場合はタイムアウトと判定すること");
    }

    [Test]
    public void DetermineTimeoutState_ReceivingExactlyAtThreshold_ReturnsNotTimedOut()
    {
        // 受信中で、最終受信からの経過時間が閾値と等しい場合、タイムアウトではない
        var result = TimecodeTimeoutLogic.DetermineTimeoutState(
            isReceiving: true,
            hasReceivedAtLeastOnce: true,
            elapsedSinceLastReceive: 2.0f,
            timeoutThresholdSeconds: 2.0f);

        Assert.IsFalse(result, "最終受信からの経過時間が閾値と等しい場合はタイムアウトと判定しないこと");
    }

    #endregion

    #region ArtNetTimecodeReceiver - タイムアウト関連フィールドの存在確認

    [Test]
    public void ArtNetTimecodeReceiver_HasTimeoutSecondsSerializeField()
    {
        // timeoutSeconds フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(ArtNetTimecodeReceiver).GetField("timeoutSeconds",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timeoutSeconds フィールドが ArtNetTimecodeReceiver に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "timeoutSeconds フィールドに [SerializeField] 属性が付与されていること（Inspectorから調整可能）");
    }

    [Test]
    public void ArtNetTimecodeReceiver_TimeoutSeconds_IsFloatType()
    {
        // timeoutSeconds フィールドの型が float であることを確認する
        var fieldInfo = typeof(ArtNetTimecodeReceiver).GetField("timeoutSeconds",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timeoutSeconds フィールドが存在すること");
        Assert.AreEqual(typeof(float), fieldInfo.FieldType,
            "timeoutSeconds フィールドの型が float であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_HasIsTimedOutProperty()
    {
        // IsTimedOut プロパティが public で存在することを確認する
        var propertyInfo = typeof(ArtNetTimecodeReceiver).GetProperty("IsTimedOut");

        Assert.IsNotNull(propertyInfo, "IsTimedOut プロパティが ArtNetTimecodeReceiver に存在すること");
        Assert.AreEqual(typeof(bool), propertyInfo.PropertyType,
            "IsTimedOut プロパティの型が bool であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_IsTimedOut_HasPublicGetter()
    {
        // IsTimedOut プロパティが public な getter を持つことを確認する
        var propertyInfo = typeof(ArtNetTimecodeReceiver).GetProperty("IsTimedOut");

        Assert.IsNotNull(propertyInfo, "プロパティが存在すること");
        Assert.IsTrue(propertyInfo.CanRead, "プロパティが getter を持つこと");

        var getter = propertyInfo.GetGetMethod();
        Assert.IsNotNull(getter, "getter が public であること");
        Assert.IsTrue(getter.IsPublic, "getter が public であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_HasLastReceivedTimeField()
    {
        // lastReceivedTime フィールドが存在することを確認する（経過時間の基準点）
        var fieldInfo = typeof(ArtNetTimecodeReceiver).GetField("lastReceivedTime",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "lastReceivedTime フィールドが ArtNetTimecodeReceiver に存在すること");
        Assert.AreEqual(typeof(float), fieldInfo.FieldType,
            "lastReceivedTime フィールドの型が float であること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_HasNotifyTimecodeReceivedMethod()
    {
        // NotifyTimecodeReceived メソッドが public で存在することを確認する
        var methodInfo = typeof(ArtNetTimecodeReceiver).GetMethod("NotifyTimecodeReceived",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "NotifyTimecodeReceived メソッドが ArtNetTimecodeReceiver に存在すること");
    }

    [Test]
    public void ArtNetTimecodeReceiver_HasIsReceivingProperty()
    {
        // IsReceiving プロパティが public で存在することを確認する（タイムアウト判定の前提条件）
        var propertyInfo = typeof(ArtNetTimecodeReceiver).GetProperty("IsReceiving");

        Assert.IsNotNull(propertyInfo, "IsReceiving プロパティが ArtNetTimecodeReceiver に存在すること");
        Assert.AreEqual(typeof(bool), propertyInfo.PropertyType,
            "IsReceiving プロパティの型が bool であること");
    }

    #endregion

    #region TimecodeTimeoutLogic - デフォルト閾値の定数

    [Test]
    public void DefaultTimeoutSeconds_Is2()
    {
        // デフォルトのタイムアウト閾値が2秒であることを確認する
        Assert.AreEqual(2.0f, TimecodeTimeoutLogic.DefaultTimeoutSeconds,
            "デフォルトのタイムアウト閾値が2秒であること");
    }

    #endregion
}
