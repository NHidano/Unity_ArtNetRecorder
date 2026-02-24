namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// ArtNet OpTimeCode パケットのパースおよびタイムコード変換ユーティリティ。
    /// タスク 3.2: タイムコード受信コンポーネントを新規作成する
    ///
    /// MonoBehaviour に依存しない純粋なロジッククラスとして実装し、
    /// EditMode テストで検証可能にする。
    ///
    /// ArtNet OpTimeCode パケット構造 (19バイト):
    ///   [0-7]  ID       "Art-Net\0"
    ///   [8-9]  OpCode   0x9700 (リトルエンディアン)
    ///   [10]   ProtVerHi  0
    ///   [11]   ProtVerLo  14
    ///   [12]   Filler1    0
    ///   [13]   Filler2    0
    ///   [14]   Frames     0-29 (Typeに依存)
    ///   [15]   Seconds    0-59
    ///   [16]   Minutes    0-59
    ///   [17]   Hours      0-23
    ///   [18]   Type       0=Film(24fps), 1=EBU(25fps), 2=DF(29.97fps), 3=SMPTE(30fps)
    /// </summary>
    public static class TimecodePacketParser
    {
        /// <summary>OpTimeCode パケットの最小バイト長</summary>
        public const int MinPacketLength = 19;

        /// <summary>Frames フィールドのバイトオフセット</summary>
        public const int FramesOffset = 14;

        /// <summary>Seconds フィールドのバイトオフセット</summary>
        public const int SecondsOffset = 15;

        /// <summary>Minutes フィールドのバイトオフセット</summary>
        public const int MinutesOffset = 16;

        /// <summary>Hours フィールドのバイトオフセット</summary>
        public const int HoursOffset = 17;

        /// <summary>Type フィールドのバイトオフセット</summary>
        public const int TypeOffset = 18;

        /// <summary>
        /// 受信バッファからOpTimeCodeパケットをパースする。
        /// パケット長が不足している場合、またはOpCodeがTimeCodeでない場合はスキップする。
        /// </summary>
        /// <param name="buffer">受信したUDPパケットのバイト配列</param>
        /// <param name="timecodeData">パース結果のタイムコードデータ</param>
        /// <returns>パース成功時はtrue、スキップ時はfalse</returns>
        public static bool TryParseTimecodePacket(byte[] buffer, out TimecodeData timecodeData)
        {
            timecodeData = default;

            // パケット長チェック: 19バイト未満はスキップ
            if (buffer == null || buffer.Length < MinPacketLength)
            {
                return false;
            }

            // OpCode チェック: TimeCode (0x97) でない場合はスキップ
            var opCode = ArtNetPacketUtillity.GetOpCode(buffer);
            if (opCode != ArtNetOpCodes.TimeCode)
            {
                return false;
            }

            // タイムコードフィールドを抽出
            byte frames = buffer[FramesOffset];
            byte seconds = buffer[SecondsOffset];
            byte minutes = buffer[MinutesOffset];
            byte hours = buffer[HoursOffset];
            byte type = buffer[TypeOffset];

            // タイムコード値をミリ秒に変換
            double milliseconds = ConvertTimecodeToMilliseconds(frames, seconds, minutes, hours, type);

            timecodeData = new TimecodeData
            {
                MillisecondsFromStart = milliseconds,
                Frames = frames,
                Seconds = seconds,
                Minutes = minutes,
                Hours = hours,
                Type = type
            };

            return true;
        }

        /// <summary>
        /// タイムコード値をミリ秒に変換する。
        /// 変換式: ((Hours * 3600 + Minutes * 60 + Seconds) * 1000) + (Frames * 1000 / fps)
        /// fps は Type に依存する (0=24, 1=25, 2=29.97, 3=30)。
        /// </summary>
        /// <param name="frames">フレーム番号</param>
        /// <param name="seconds">秒</param>
        /// <param name="minutes">分</param>
        /// <param name="hours">時</param>
        /// <param name="type">フレームレート種別</param>
        /// <returns>先頭からのミリ秒</returns>
        public static double ConvertTimecodeToMilliseconds(byte frames, byte seconds, byte minutes, byte hours, byte type)
        {
            double fps = GetFrameRate(type);
            double totalSeconds = hours * 3600.0 + minutes * 60.0 + seconds;
            double frameMilliseconds = frames * 1000.0 / fps;
            return totalSeconds * 1000.0 + frameMilliseconds;
        }

        /// <summary>
        /// タイムコード種別からフレームレートを取得する。
        /// 未知の種別の場合はデフォルトの30fpsを返す。
        /// </summary>
        /// <param name="type">フレームレート種別 (0-3)</param>
        /// <returns>フレームレート (fps)</returns>
        public static double GetFrameRate(byte type)
        {
            switch (type)
            {
                case 0: return 24.0;    // Film
                case 1: return 25.0;    // EBU
                case 2: return 29.97;   // DF
                case 3: return 30.0;    // SMPTE
                default: return 30.0;   // デフォルト: SMPTE
            }
        }
    }
}
