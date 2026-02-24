using System;
using System.Net;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// ArtNetResendUI の永続化機能に関するテスト。
/// タスク 4: ArtNet再送信先設定の永続化を実装する
///
/// 要件:
/// - 4.1: DstIP入力値の保存
/// - 4.2: DstPort入力値の保存
/// - 4.3: 起動時の保存値読み込み・表示
/// - 4.4: 保存データ未存在時のデフォルト値表示
/// - 4.5: トグル状態の保存
/// </summary>
public class ArtNetResendPersistenceTests
{
    #region PlayerPrefsキー定数の存在確認

    [Test]
    public void ResendSettingsKeys_DstIPKey_HasCorrectValue()
    {
        // PlayerPrefsのキー定数が正しい値であることを確認する
        Assert.AreEqual("ArtNetResend_DstIP", ResendSettingsKeys.DstIP,
            "DstIPキーが設計書と一致すること");
    }

    [Test]
    public void ResendSettingsKeys_DstPortKey_HasCorrectValue()
    {
        Assert.AreEqual("ArtNetResend_DstPort", ResendSettingsKeys.DstPort,
            "DstPortキーが設計書と一致すること");
    }

    [Test]
    public void ResendSettingsKeys_EnabledKey_HasCorrectValue()
    {
        Assert.AreEqual("ArtNetResend_Enabled", ResendSettingsKeys.Enabled,
            "Enabledキーが設計書と一致すること");
    }

    #endregion

    #region デフォルト値の確認

    [Test]
    public void ResendSettingsDefaults_DefaultDstIP_Is2001()
    {
        // デフォルト値が設計書の仕様と一致することを確認する
        Assert.AreEqual("2.0.0.1", ResendSettingsDefaults.DstIP,
            "デフォルトのDstIPが '2.0.0.1' であること");
    }

    [Test]
    public void ResendSettingsDefaults_DefaultDstPort_Is6454()
    {
        Assert.AreEqual(6454, ResendSettingsDefaults.DstPort,
            "デフォルトのDstPortが 6454 であること");
    }

    [Test]
    public void ResendSettingsDefaults_DefaultEnabled_IsFalse()
    {
        Assert.AreEqual(0, ResendSettingsDefaults.Enabled,
            "デフォルトのトグル状態が OFF (0) であること");
    }

    #endregion

    #region ResendSettingsValidator - IPアドレス検証ロジック

    [Test]
    public void ValidateIP_ValidIPv4_ReturnsTrue()
    {
        // 有効なIPv4アドレスの検証が成功すること
        Assert.IsTrue(ResendSettingsValidator.IsValidIP("192.168.1.1"),
            "有効なIPv4アドレスの検証が成功すること");
    }

    [Test]
    public void ValidateIP_ValidArtNetIP_ReturnsTrue()
    {
        // ArtNetで一般的に使用されるIPアドレスの検証が成功すること
        Assert.IsTrue(ResendSettingsValidator.IsValidIP("2.0.0.1"),
            "ArtNet用IPアドレス '2.0.0.1' の検証が成功すること");
    }

    [Test]
    public void ValidateIP_EmptyString_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidIP(""),
            "空文字列の検証が失敗すること");
    }

    [Test]
    public void ValidateIP_NullString_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidIP(null),
            "nullの検証が失敗すること");
    }

    [Test]
    public void ValidateIP_InvalidFormat_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidIP("not.an.ip"),
            "不正な形式のIPアドレスの検証が失敗すること");
    }

    [Test]
    public void ValidateIP_TooManyOctets_ReturnsFalse()
    {
        // オクテットが5つ以上のIPアドレスは不正
        Assert.IsFalse(ResendSettingsValidator.IsValidIP("192.168.1.1.1"),
            "オクテットが5つ以上のIPアドレスの検証が失敗すること");
    }

    #endregion

    #region ResendSettingsValidator - ポート番号検証ロジック

    [Test]
    public void ValidatePort_ValidPort6454_ReturnsTrue()
    {
        // ArtNetデフォルトポートの検証が成功すること
        Assert.IsTrue(ResendSettingsValidator.IsValidPort(6454),
            "ポート 6454 の検証が成功すること");
    }

    [Test]
    public void ValidatePort_MinPort_ReturnsTrue()
    {
        // 最小有効ポート番号の検証が成功すること
        Assert.IsTrue(ResendSettingsValidator.IsValidPort(1),
            "ポート 1 の検証が成功すること");
    }

    [Test]
    public void ValidatePort_MaxPort_ReturnsTrue()
    {
        // 最大有効ポート番号の検証が成功すること
        Assert.IsTrue(ResendSettingsValidator.IsValidPort(65535),
            "ポート 65535 の検証が成功すること");
    }

    [Test]
    public void ValidatePort_ZeroPort_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPort(0),
            "ポート 0 の検証が失敗すること");
    }

    [Test]
    public void ValidatePort_NegativePort_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPort(-1),
            "負のポート番号の検証が失敗すること");
    }

    [Test]
    public void ValidatePort_OverflowPort_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPort(65536),
            "65536 以上のポート番号の検証が失敗すること");
    }

    #endregion

    #region ResendSettingsValidator - ポート文字列パース検証

    [Test]
    public void ValidatePortString_ValidString_ReturnsTrue()
    {
        Assert.IsTrue(ResendSettingsValidator.IsValidPortString("6454"),
            "有効なポート文字列の検証が成功すること");
    }

    [Test]
    public void ValidatePortString_NonNumeric_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPortString("abc"),
            "数値でない文字列の検証が失敗すること");
    }

    [Test]
    public void ValidatePortString_Empty_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPortString(""),
            "空文字列の検証が失敗すること");
    }

    [Test]
    public void ValidatePortString_Null_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPortString(null),
            "nullの検証が失敗すること");
    }

    [Test]
    public void ValidatePortString_OutOfRange_ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IsValidPortString("70000"),
            "範囲外の数値文字列の検証が失敗すること");
    }

    #endregion

    #region ResendSettingsValidator - 保存値のフォールバック付き読み込みロジック

    [Test]
    public void GetValidatedIP_ValidIP_ReturnsSameValue()
    {
        // 有効なIPアドレスはそのまま返されること
        var result = ResendSettingsValidator.GetValidatedIP("10.0.0.1");
        Assert.AreEqual("10.0.0.1", result,
            "有効なIPアドレスはそのまま返されること");
    }

    [Test]
    public void GetValidatedIP_InvalidIP_ReturnsDefault()
    {
        // 不正なIPアドレスの場合、デフォルト値が返されること
        var result = ResendSettingsValidator.GetValidatedIP("invalid");
        Assert.AreEqual(ResendSettingsDefaults.DstIP, result,
            "不正なIPアドレスの場合はデフォルト値が返されること");
    }

    [Test]
    public void GetValidatedIP_EmptyString_ReturnsDefault()
    {
        var result = ResendSettingsValidator.GetValidatedIP("");
        Assert.AreEqual(ResendSettingsDefaults.DstIP, result,
            "空文字列の場合はデフォルト値が返されること");
    }

    [Test]
    public void GetValidatedPort_ValidPort_ReturnsSameValue()
    {
        var result = ResendSettingsValidator.GetValidatedPort(8000);
        Assert.AreEqual(8000, result,
            "有効なポート番号はそのまま返されること");
    }

    [Test]
    public void GetValidatedPort_InvalidPort_ReturnsDefault()
    {
        var result = ResendSettingsValidator.GetValidatedPort(0);
        Assert.AreEqual(ResendSettingsDefaults.DstPort, result,
            "不正なポート番号の場合はデフォルト値が返されること");
    }

    [Test]
    public void GetValidatedPort_NegativePort_ReturnsDefault()
    {
        var result = ResendSettingsValidator.GetValidatedPort(-100);
        Assert.AreEqual(ResendSettingsDefaults.DstPort, result,
            "負のポート番号の場合はデフォルト値が返されること");
    }

    #endregion

    #region ArtNetResendUI - 永続化関連メソッドの存在確認

    [Test]
    public void ArtNetResendUI_HasLoadSettingsMethod()
    {
        // ArtNetResendUI に LoadSettings メソッドが存在することを確認する
        var methodInfo = typeof(ArtNetResendUI).GetMethod("LoadSettings",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "LoadSettings メソッドが ArtNetResendUI に存在すること");
    }

    [Test]
    public void ArtNetResendUI_HasSaveIPMethod()
    {
        // ArtNetResendUI に SaveIP メソッドが存在することを確認する
        var methodInfo = typeof(ArtNetResendUI).GetMethod("SaveIP",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "SaveIP メソッドが ArtNetResendUI に存在すること");
    }

    [Test]
    public void ArtNetResendUI_HasSavePortMethod()
    {
        // ArtNetResendUI に SavePort メソッドが存在することを確認する
        var methodInfo = typeof(ArtNetResendUI).GetMethod("SavePort",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "SavePort メソッドが ArtNetResendUI に存在すること");
    }

    [Test]
    public void ArtNetResendUI_HasSaveToggleStateMethod()
    {
        // ArtNetResendUI に SaveToggleState メソッドが存在することを確認する
        var methodInfo = typeof(ArtNetResendUI).GetMethod("SaveToggleState",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(methodInfo, "SaveToggleState メソッドが ArtNetResendUI に存在すること");
    }

    #endregion

    #region ArtNetResendUI - トグル状態のリアクティブ購読確認

    [Test]
    public void ArtNetResendUI_HasEnableToggleSerializeField()
    {
        // ArtNetResendUI に enableToggle フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(ArtNetResendUI).GetField("enableToggle",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "enableToggle フィールドが ArtNetResendUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "enableToggle フィールドに [SerializeField] 属性が付与されていること");
    }

    [Test]
    public void ArtNetResendUI_HasIpInputFieldSerializeField()
    {
        // ArtNetResendUI に ipInputField フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(ArtNetResendUI).GetField("ipInputField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "ipInputField フィールドが ArtNetResendUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "ipInputField フィールドに [SerializeField] 属性が付与されていること");
    }

    [Test]
    public void ArtNetResendUI_HasPortInputFieldSerializeField()
    {
        // ArtNetResendUI に portInputField フィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(ArtNetResendUI).GetField("portInputField",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "portInputField フィールドが ArtNetResendUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "portInputField フィールドに [SerializeField] 属性が付与されていること");
    }

    #endregion

    #region ResendSettingsValidator - トグル状態のint変換

    [Test]
    public void ToggleStateToInt_TrueReturns1()
    {
        Assert.AreEqual(1, ResendSettingsValidator.ToggleStateToInt(true),
            "ONの場合は1を返すこと");
    }

    [Test]
    public void ToggleStateToInt_FalseReturns0()
    {
        Assert.AreEqual(0, ResendSettingsValidator.ToggleStateToInt(false),
            "OFFの場合は0を返すこと");
    }

    [Test]
    public void IntToToggleState_1ReturnsTrue()
    {
        Assert.IsTrue(ResendSettingsValidator.IntToToggleState(1),
            "1の場合はtrueを返すこと");
    }

    [Test]
    public void IntToToggleState_0ReturnsFalse()
    {
        Assert.IsFalse(ResendSettingsValidator.IntToToggleState(0),
            "0の場合はfalseを返すこと");
    }

    [Test]
    public void IntToToggleState_OtherValueReturnsFalse()
    {
        // 不正な値の場合はデフォルト (false) にフォールバック
        Assert.IsFalse(ResendSettingsValidator.IntToToggleState(99),
            "不正な値の場合はfalseを返すこと");
    }

    #endregion
}
