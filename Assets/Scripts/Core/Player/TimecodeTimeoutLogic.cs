namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// タイムコード受信タイムアウト検出の判定ロジック。
    /// タスク 3.3: タイムコード受信タイムアウト検出を実装する
    ///
    /// ArtNetTimecodeReceiver の IsTimedOut プロパティから呼ばれる純粋な判定ロジックを提供する。
    /// MonoBehaviour に依存しないため、EditMode テストで検証可能。
    ///
    /// 要件 3.5: タイムコード未受信時の一時停止
    /// - 最終受信からの経過時間を監視し、閾値（デフォルト2秒）を超えた場合にタイムアウト状態を公開する
    /// </summary>
    public static class TimecodeTimeoutLogic
    {
        /// <summary>デフォルトのタイムアウト閾値（秒）</summary>
        public const float DefaultTimeoutSeconds = 2.0f;

        /// <summary>
        /// 経過時間が閾値を超えたかどうかを判定する。
        /// 閾値との比較は厳密な「超過」であり、等しい場合はタイムアウトとしない。
        /// </summary>
        /// <param name="elapsedSeconds">最終受信からの経過時間（秒）</param>
        /// <param name="timeoutThresholdSeconds">タイムアウト閾値（秒）</param>
        /// <returns>タイムアウトの場合は true</returns>
        public static bool IsTimedOut(float elapsedSeconds, float timeoutThresholdSeconds)
        {
            if (elapsedSeconds < 0f) return false;
            return elapsedSeconds > timeoutThresholdSeconds;
        }

        /// <summary>
        /// 受信状態を考慮した総合的なタイムアウト判定を行う。
        /// - 受信中でない場合はタイムアウトではない
        /// - 受信中だが一度も受信していない場合はタイムアウトと判定する
        /// - 受信中で最終受信からの経過時間が閾値を超えた場合はタイムアウトと判定する
        /// </summary>
        /// <param name="isReceiving">受信中かどうか</param>
        /// <param name="hasReceivedAtLeastOnce">少なくとも1回以上受信したかどうか</param>
        /// <param name="elapsedSinceLastReceive">最終受信からの経過時間（秒）</param>
        /// <param name="timeoutThresholdSeconds">タイムアウト閾値（秒）</param>
        /// <returns>タイムアウト状態の場合は true</returns>
        public static bool DetermineTimeoutState(
            bool isReceiving,
            bool hasReceivedAtLeastOnce,
            float elapsedSinceLastReceive,
            float timeoutThresholdSeconds)
        {
            if (!isReceiving) return false;
            if (!hasReceivedAtLeastOnce) return true;
            return IsTimedOut(elapsedSinceLastReceive, timeoutThresholdSeconds);
        }
    }
}
