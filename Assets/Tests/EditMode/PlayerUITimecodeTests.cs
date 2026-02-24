using System;
using System.Reflection;
using NUnit.Framework;
using ProjectBlue.ArtNetRecorder;

/// <summary>
/// PlayerUI のタイムコード受信モードUIに関するテスト。
/// タスク 3.4: タイムコード受信モードUIを追加する
///
/// テスト対象:
/// - タイムコードトグル (timecodeToggle) の SerializeField 存在確認
/// - OnTimecodeToggleChangedAsObservable プロパティの存在・型確認
/// - SetManualControlEnabled メソッドの存在確認
/// - SetTimecodeDisplay メソッドの存在確認
/// - SetTimecodeDisplayVisible メソッドの存在確認
/// - タイムコード表示テキスト (timecodeDisplayText) の SerializeField 存在確認
/// - TimecodeDisplayFormatter の表示フォーマットロジック
///
/// Requirements: 3.1, 3.3, 3.4, 3.6
/// </summary>
public class PlayerUITimecodeTests
{
    #region タイムコードトグル SerializeField - フィールドの存在確認

    [Test]
    public void PlayerUI_HasTimecodeToggleSerializeField()
    {
        // PlayerUIクラスに timecodeToggle フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(PlayerUI).GetField("timecodeToggle",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timecodeToggle フィールドが PlayerUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "timecodeToggle フィールドに [SerializeField] 属性が付与されていること");
    }

    [Test]
    public void PlayerUI_TimecodeToggleField_IsUnityToggleType()
    {
        // timecodeToggle フィールドの型が UnityEngine.UI.Toggle であることを確認する
        var fieldInfo = typeof(PlayerUI).GetField("timecodeToggle",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timecodeToggle フィールドが存在すること");
        Assert.AreEqual(typeof(UnityEngine.UI.Toggle), fieldInfo.FieldType,
            "timecodeToggle フィールドの型が Toggle であること");
    }

    #endregion

    #region タイムコード表示テキスト SerializeField - フィールドの存在確認

    [Test]
    public void PlayerUI_HasTimecodeDisplayTextSerializeField()
    {
        // PlayerUIクラスに timecodeDisplayText フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(PlayerUI).GetField("timecodeDisplayText",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timecodeDisplayText フィールドが PlayerUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "timecodeDisplayText フィールドに [SerializeField] 属性が付与されていること");
    }

    [Test]
    public void PlayerUI_TimecodeDisplayTextField_IsUnityTextType()
    {
        // timecodeDisplayText フィールドの型が UnityEngine.UI.Text であることを確認する
        var fieldInfo = typeof(PlayerUI).GetField("timecodeDisplayText",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "timecodeDisplayText フィールドが存在すること");
        Assert.AreEqual(typeof(UnityEngine.UI.Text), fieldInfo.FieldType,
            "timecodeDisplayText フィールドの型が Text であること");
    }

    #endregion

    #region OnTimecodeToggleChangedAsObservable - リアクティブストリームの公開

    [Test]
    public void OnTimecodeToggleChangedAsObservable_PropertyExists_ReturnsIObservableBool()
    {
        // PlayerUIクラスに OnTimecodeToggleChangedAsObservable プロパティが存在し、
        // IObservable<bool> 型であることをリフレクションで確認する
        var propertyInfo = typeof(PlayerUI).GetProperty("OnTimecodeToggleChangedAsObservable");

        Assert.IsNotNull(propertyInfo, "OnTimecodeToggleChangedAsObservable プロパティが PlayerUI に存在すること");
        Assert.AreEqual(typeof(IObservable<bool>), propertyInfo.PropertyType,
            "OnTimecodeToggleChangedAsObservable の型が IObservable<bool> であること");
    }

    [Test]
    public void OnTimecodeToggleChangedAsObservable_PropertyIsPublicGetter()
    {
        // OnTimecodeToggleChangedAsObservable が public な getter を持つことを確認する
        var propertyInfo = typeof(PlayerUI).GetProperty("OnTimecodeToggleChangedAsObservable");

        Assert.IsNotNull(propertyInfo, "プロパティが存在すること");
        Assert.IsTrue(propertyInfo.CanRead, "プロパティが getter を持つこと");

        var getter = propertyInfo.GetGetMethod();
        Assert.IsNotNull(getter, "getter が public であること");
        Assert.IsTrue(getter.IsPublic, "getter が public であること");
    }

    #endregion

    #region SetManualControlEnabled - 手動再生コントロールの有効/無効切替

    [Test]
    public void SetManualControlEnabled_MethodExists()
    {
        // PlayerUI に SetManualControlEnabled(bool) メソッドが存在することを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetManualControlEnabled",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "SetManualControlEnabled(bool) メソッドが PlayerUI に存在すること");
    }

    [Test]
    public void SetManualControlEnabled_ReturnsVoid()
    {
        // SetManualControlEnabled の戻り値が void であることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetManualControlEnabled",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.AreEqual(typeof(void), methodInfo.ReturnType,
            "SetManualControlEnabled の戻り値が void であること");
    }

    [Test]
    public void SetManualControlEnabled_IsPublicMethod()
    {
        // SetManualControlEnabled が public メソッドであることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetManualControlEnabled",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.IsTrue(methodInfo.IsPublic, "SetManualControlEnabled が public であること");
    }

    #endregion

    #region SetTimecodeDisplay - タイムコード表示の更新

    [Test]
    public void SetTimecodeDisplay_MethodExists()
    {
        // PlayerUI に SetTimecodeDisplay(string) メソッドが存在することを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplay",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null);

        Assert.IsNotNull(methodInfo, "SetTimecodeDisplay(string) メソッドが PlayerUI に存在すること");
    }

    [Test]
    public void SetTimecodeDisplay_ReturnsVoid()
    {
        // SetTimecodeDisplay の戻り値が void であることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplay",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.AreEqual(typeof(void), methodInfo.ReturnType,
            "SetTimecodeDisplay の戻り値が void であること");
    }

    [Test]
    public void SetTimecodeDisplay_IsPublicMethod()
    {
        // SetTimecodeDisplay が public メソッドであることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplay",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(string) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.IsTrue(methodInfo.IsPublic, "SetTimecodeDisplay が public であること");
    }

    #endregion

    #region SetTimecodeDisplayVisible - タイムコード表示の表示/非表示

    [Test]
    public void SetTimecodeDisplayVisible_MethodExists()
    {
        // PlayerUI に SetTimecodeDisplayVisible(bool) メソッドが存在することを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplayVisible",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "SetTimecodeDisplayVisible(bool) メソッドが PlayerUI に存在すること");
    }

    [Test]
    public void SetTimecodeDisplayVisible_ReturnsVoid()
    {
        // SetTimecodeDisplayVisible の戻り値が void であることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplayVisible",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.AreEqual(typeof(void), methodInfo.ReturnType,
            "SetTimecodeDisplayVisible の戻り値が void であること");
    }

    [Test]
    public void SetTimecodeDisplayVisible_IsPublicMethod()
    {
        // SetTimecodeDisplayVisible が public メソッドであることを確認する
        var methodInfo = typeof(PlayerUI).GetMethod("SetTimecodeDisplayVisible",
            BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(bool) }, null);

        Assert.IsNotNull(methodInfo, "メソッドが存在すること");
        Assert.IsTrue(methodInfo.IsPublic, "SetTimecodeDisplayVisible が public であること");
    }

    #endregion

    #region TimecodeDisplayFormatter - タイムコード表示フォーマットロジック

    [Test]
    public void FormatTimecode_ZeroValues_ReturnsZeroFormat()
    {
        // 全てゼロの場合 "00:00:00:00" を返す
        var result = TimecodeDisplayFormatter.FormatTimecode(0, 0, 0, 0);

        Assert.AreEqual("00:00:00:00", result);
    }

    [Test]
    public void FormatTimecode_SingleDigitValues_ZeroPadded()
    {
        // 1桁の値がゼロパディングされることを確認する
        var result = TimecodeDisplayFormatter.FormatTimecode(1, 2, 3, 5);

        Assert.AreEqual("01:02:03:05", result);
    }

    [Test]
    public void FormatTimecode_DoubleDigitValues_FormattedCorrectly()
    {
        // 2桁の値が正しくフォーマットされることを確認する
        var result = TimecodeDisplayFormatter.FormatTimecode(23, 59, 59, 29);

        Assert.AreEqual("23:59:59:29", result);
    }

    [Test]
    public void FormatTimecode_TypicalTimecode_FormattedCorrectly()
    {
        // 典型的なタイムコード値のフォーマットを確認する
        var result = TimecodeDisplayFormatter.FormatTimecode(1, 5, 30, 12);

        Assert.AreEqual("01:05:30:12", result);
    }

    [Test]
    public void FormatTimecodeFromData_CreatesCorrectString()
    {
        // TimecodeData 構造体からフォーマット文字列を生成する
        var data = new TimecodeData
        {
            Hours = 2,
            Minutes = 30,
            Seconds = 45,
            Frames = 20,
            Type = 3,
            MillisecondsFromStart = 9045666.67
        };

        var result = TimecodeDisplayFormatter.FormatTimecode(data);

        Assert.AreEqual("02:30:45:20", result);
    }

    [Test]
    public void FormatTimecodeFromData_DefaultTimecodeData_ReturnsZeroFormat()
    {
        // デフォルトの TimecodeData 構造体でゼロフォーマットを返す
        var data = new TimecodeData();

        var result = TimecodeDisplayFormatter.FormatTimecode(data);

        Assert.AreEqual("00:00:00:00", result);
    }

    [Test]
    public void DefaultDisplayText_ReturnsExpectedValue()
    {
        // デフォルト表示テキストの定数が期待値であることを確認する
        Assert.AreEqual("--:--:--:--", TimecodeDisplayFormatter.DefaultDisplayText);
    }

    #endregion
}
