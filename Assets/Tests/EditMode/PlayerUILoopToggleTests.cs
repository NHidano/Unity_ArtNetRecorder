using System;
using NUnit.Framework;

/// <summary>
/// PlayerUI のループ再生トグルUIに関するテスト。
/// タスク 2.1: ループ再生トグルUIを追加する
/// </summary>
public class PlayerUILoopToggleTests
{
    #region OnLoopToggleChangedAsObservable - リアクティブストリームの公開

    [Test]
    public void OnLoopToggleChangedAsObservable_PropertyExists_ReturnsIObservableBool()
    {
        // PlayerUIクラスにOnLoopToggleChangedAsObservableプロパティが存在し、
        // IObservable<bool>型であることをリフレクションで確認する
        var propertyInfo = typeof(PlayerUI).GetProperty("OnLoopToggleChangedAsObservable");

        Assert.IsNotNull(propertyInfo, "OnLoopToggleChangedAsObservable プロパティが PlayerUI に存在すること");
        Assert.AreEqual(typeof(IObservable<bool>), propertyInfo.PropertyType,
            "OnLoopToggleChangedAsObservable の型が IObservable<bool> であること");
    }

    [Test]
    public void OnLoopToggleChangedAsObservable_PropertyIsPublicGetter()
    {
        // OnLoopToggleChangedAsObservableがpublicなgetterを持つことを確認する
        var propertyInfo = typeof(PlayerUI).GetProperty("OnLoopToggleChangedAsObservable");

        Assert.IsNotNull(propertyInfo, "プロパティが存在すること");
        Assert.IsTrue(propertyInfo.CanRead, "プロパティがgetterを持つこと");

        var getter = propertyInfo.GetGetMethod();
        Assert.IsNotNull(getter, "getterがpublicであること");
        Assert.IsTrue(getter.IsPublic, "getterがpublicであること");
    }

    #endregion

    #region LoopToggle SerializeField - トグルフィールドの存在確認

    [Test]
    public void PlayerUI_HasLoopToggleSerializeField()
    {
        // PlayerUIクラスにloopToggleフィールドが存在し、SerializeField属性を持つことを確認する
        var fieldInfo = typeof(PlayerUI).GetField("loopToggle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "loopToggle フィールドが PlayerUI に存在すること");

        var serializeFieldAttr = fieldInfo.GetCustomAttributes(typeof(UnityEngine.SerializeField), false);
        Assert.IsTrue(serializeFieldAttr.Length > 0,
            "loopToggle フィールドに [SerializeField] 属性が付与されていること");
    }

    [Test]
    public void PlayerUI_LoopToggleField_IsUnityToggleType()
    {
        // loopToggleフィールドの型がUnityEngine.UI.Toggleであることを確認する
        var fieldInfo = typeof(PlayerUI).GetField("loopToggle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(fieldInfo, "loopToggle フィールドが存在すること");
        Assert.AreEqual(typeof(UnityEngine.UI.Toggle), fieldInfo.FieldType,
            "loopToggle フィールドの型が Toggle であること");
    }

    #endregion
}
