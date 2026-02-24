namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// タイムコード同期再生の判定ロジック。
    /// タスク 3.5: タイムコード同期再生ロジックをアプリケーション層に統合する
    ///
    /// ArtNetPlayerApplication の Update() ループから呼ばれる純粋な判定ロジックを提供する。
    /// MonoBehaviour に依存しないため、EditMode テストで検証可能。
    ///
    /// Requirements: 3.2, 3.3, 3.5, 3.6
    /// </summary>
    public static class TimecodeSyncLogic
    {
        /// <summary>
        /// タイムコードモードを有効化できるかどうかを判定する。
        /// 録画データがロード済みであることが前提条件。
        /// </summary>
        /// <param name="isInitialized">録画データがロード済みかどうか</param>
        /// <returns>有効化可能な場合は true</returns>
        public static bool CanEnableTimecodeMode(bool isInitialized)
        {
            return isInitialized;
        }

        /// <summary>
        /// タイムアウト時に再生を一時停止すべきかどうかを判定する。
        /// タイムアウト状態でまだ一時停止していない場合にのみ true を返す。
        /// </summary>
        /// <param name="isTimedOut">タイムアウト状態かどうか</param>
        /// <param name="isAlreadyPausedByTimeout">既にタイムアウトにより一時停止済みかどうか</param>
        /// <returns>一時停止すべき場合は true</returns>
        public static bool ShouldPauseOnTimeout(bool isTimedOut, bool isAlreadyPausedByTimeout)
        {
            return isTimedOut && !isAlreadyPausedByTimeout;
        }

        /// <summary>
        /// タイムアウトからの回復が必要かどうかを判定する。
        /// タイムアウトが解消され、タイムアウトによる一時停止状態だった場合に true を返す。
        /// </summary>
        /// <param name="isTimedOut">タイムアウト状態かどうか</param>
        /// <param name="isAlreadyPausedByTimeout">タイムアウトにより一時停止済みかどうか</param>
        /// <returns>回復すべき場合は true</returns>
        public static bool ShouldResumeFromTimeout(bool isTimedOut, bool isAlreadyPausedByTimeout)
        {
            return !isTimedOut && isAlreadyPausedByTimeout;
        }

        /// <summary>
        /// タイムコード値を再生範囲内にクランプする。
        /// 負の値は0に、終端時間を超える値は終端時間にクランプする。
        /// </summary>
        /// <param name="timecodeMilliseconds">タイムコードから算出されたミリ秒</param>
        /// <param name="endTimeMilliseconds">録画データの終端時間（ミリ秒）</param>
        /// <returns>クランプされたミリ秒値</returns>
        public static double ClampTimecodeToPlaybackRange(double timecodeMilliseconds, double endTimeMilliseconds)
        {
            if (timecodeMilliseconds < 0.0) return 0.0;
            if (timecodeMilliseconds > endTimeMilliseconds) return endTimeMilliseconds;
            return timecodeMilliseconds;
        }
    }
}
