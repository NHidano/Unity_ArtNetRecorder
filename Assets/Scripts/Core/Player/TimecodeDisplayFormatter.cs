namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// タイムコード表示のフォーマットロジック。
    /// タスク 3.4: タイムコード受信モードUIを追加する
    ///
    /// MonoBehaviour に依存しない純粋なフォーマットロジックを提供する。
    /// EditMode テストで検証可能。
    ///
    /// Requirements: 3.4 (タイムコード値のリアルタイム表示)
    /// </summary>
    public static class TimecodeDisplayFormatter
    {
        /// <summary>タイムコード未受信時のデフォルト表示テキスト</summary>
        public const string DefaultDisplayText = "--:--:--:--";

        /// <summary>
        /// タイムコード値を "HH:MM:SS:FF" 形式の表示文字列にフォーマットする。
        /// </summary>
        /// <param name="hours">時 (0-23)</param>
        /// <param name="minutes">分 (0-59)</param>
        /// <param name="seconds">秒 (0-59)</param>
        /// <param name="frames">フレーム番号 (0-29, Typeに依存)</param>
        /// <returns>"HH:MM:SS:FF" 形式のフォーマット文字列</returns>
        public static string FormatTimecode(byte hours, byte minutes, byte seconds, byte frames)
        {
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }

        /// <summary>
        /// TimecodeData 構造体からタイムコード表示文字列を生成する。
        /// </summary>
        /// <param name="data">タイムコードデータ</param>
        /// <returns>"HH:MM:SS:FF" 形式のフォーマット文字列</returns>
        public static string FormatTimecode(TimecodeData data)
        {
            return FormatTimecode(data.Hours, data.Minutes, data.Seconds, data.Frames);
        }
    }
}
