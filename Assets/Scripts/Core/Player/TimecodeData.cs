namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// ArtNet Timecodeパケットから抽出されたタイムコードデータ。
    /// ConcurrentQueue 経由でバックグラウンドスレッドからメインスレッドに受け渡される。
    /// タスク 3.2: タイムコード受信コンポーネントを新規作成する
    /// </summary>
    public struct TimecodeData
    {
        /// <summary>先頭からのミリ秒</summary>
        public double MillisecondsFromStart;

        /// <summary>フレーム番号 (0-29, Typeに依存)</summary>
        public byte Frames;

        /// <summary>秒 (0-59)</summary>
        public byte Seconds;

        /// <summary>分 (0-59)</summary>
        public byte Minutes;

        /// <summary>時 (0-23)</summary>
        public byte Hours;

        /// <summary>フレームレート種別 (0=Film 24fps, 1=EBU 25fps, 2=DF 29.97fps, 3=SMPTE 30fps)</summary>
        public byte Type;
    }
}
