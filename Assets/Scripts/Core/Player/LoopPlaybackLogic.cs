/// <summary>
/// 終端到達時のアクションを表す列挙型。
/// </summary>
public enum EndOfPlaybackAction
{
    /// <summary>先頭にリセットして再生を継続する（ループ有効時）</summary>
    ResetToBeginning,

    /// <summary>再生を停止する（ループ無効時）</summary>
    StopPlayback
}

/// <summary>
/// ループ再生の判定ロジック。
/// タスク 2.2: ループ再生ロジックをアプリケーション層に実装する
///
/// ArtNetPlayerApplication の Update() ループから呼ばれる純粋な判定ロジックを提供する。
/// MonoBehaviour に依存しないため、EditMode テストで検証可能。
/// </summary>
public static class LoopPlaybackLogic
{
    /// <summary>
    /// 再生位置が録画データの終端に到達したかどうかを判定する。
    /// </summary>
    /// <param name="header">現在の再生ヘッダー位置（ミリ秒）</param>
    /// <param name="endTime">録画データの終端時間（ミリ秒）</param>
    /// <returns>終端を超過した場合は true</returns>
    public static bool IsEndOfPlayback(double header, double endTime)
    {
        return header > endTime;
    }

    /// <summary>
    /// 終端到達時のアクションを決定する。
    /// ループ有効時は先頭リセット、無効時は停止を返す。
    /// </summary>
    /// <param name="isLoopEnabled">ループ再生が有効かどうか</param>
    /// <returns>実行すべきアクション</returns>
    public static EndOfPlaybackAction DetermineEndOfPlaybackAction(bool isLoopEnabled)
    {
        return isLoopEnabled ? EndOfPlaybackAction.ResetToBeginning : EndOfPlaybackAction.StopPlayback;
    }
}
