using System.Collections.Concurrent;
using NUnit.Framework;
using ProjectBlue.ArtNetRecorder;

/// <summary>
/// ArtNetTimecodeReceiver のタイムコード受信コンポーネントに関するテスト。
/// タスク 3.2: タイムコード受信コンポーネントを新規作成する
///
/// テスト対象:
/// - TimecodeData 構造体のフィールド
/// - OpTimeCodeパケットのパースロジック
/// - タイムコード→ミリ秒変換ロジック（フレームレート種別対応）
/// - パケット長不足・OpCode不一致のスキップ処理
/// - ConcurrentQueue によるスレッド間通信データ構造
/// </summary>
public class ArtNetTimecodeReceiverTests
{
    #region TimecodeData 構造体

    [Test]
    public void TimecodeData_DefaultValues_AllZero()
    {
        var data = new TimecodeData();

        Assert.AreEqual(0.0, data.MillisecondsFromStart);
        Assert.AreEqual(0, data.Frames);
        Assert.AreEqual(0, data.Seconds);
        Assert.AreEqual(0, data.Minutes);
        Assert.AreEqual(0, data.Hours);
        Assert.AreEqual(0, data.Type);
    }

    [Test]
    public void TimecodeData_CanSetAllFields()
    {
        var data = new TimecodeData
        {
            MillisecondsFromStart = 3723500.0,
            Frames = 12,
            Seconds = 30,
            Minutes = 2,
            Hours = 1,
            Type = 3
        };

        Assert.AreEqual(3723500.0, data.MillisecondsFromStart);
        Assert.AreEqual(12, data.Frames);
        Assert.AreEqual(30, data.Seconds);
        Assert.AreEqual(2, data.Minutes);
        Assert.AreEqual(1, data.Hours);
        Assert.AreEqual(3, data.Type);
    }

    [Test]
    public void TimecodeData_CanBeStoredInConcurrentQueue()
    {
        // ConcurrentQueue でスレッド間通信が可能であることを確認
        var queue = new ConcurrentQueue<TimecodeData>();
        var data = new TimecodeData
        {
            MillisecondsFromStart = 1000.0,
            Frames = 5,
            Seconds = 10,
            Minutes = 20,
            Hours = 1,
            Type = 1
        };

        queue.Enqueue(data);

        Assert.IsTrue(queue.TryDequeue(out var result));
        Assert.AreEqual(1000.0, result.MillisecondsFromStart);
        Assert.AreEqual(5, result.Frames);
        Assert.AreEqual(10, result.Seconds);
        Assert.AreEqual(20, result.Minutes);
        Assert.AreEqual(1, result.Hours);
        Assert.AreEqual(1, result.Type);
    }

    #endregion

    #region パケットパースロジック - TryParseTimecodePacket

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ReturnsTrue()
    {
        // 正常な19バイトのOpTimeCodeパケット
        var buffer = CreateValidTimecodePacket(frames: 10, seconds: 30, minutes: 5, hours: 1, type: 3);

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.IsTrue(result);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ExtractsFrames()
    {
        var buffer = CreateValidTimecodePacket(frames: 15, seconds: 0, minutes: 0, hours: 0, type: 0);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(15, timecodeData.Frames);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ExtractsSeconds()
    {
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 45, minutes: 0, hours: 0, type: 0);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(45, timecodeData.Seconds);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ExtractsMinutes()
    {
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 30, hours: 0, type: 0);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(30, timecodeData.Minutes);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ExtractsHours()
    {
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 0, hours: 23, type: 0);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(23, timecodeData.Hours);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_ExtractsType()
    {
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 0, hours: 0, type: 2);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(2, timecodeData.Type);
    }

    [Test]
    public void TryParseTimecodePacket_ValidPacket_CalculatesMilliseconds()
    {
        // 1時間5分30秒 + 12フレーム (SMPTE 30fps)
        // = (1*3600 + 5*60 + 30) * 1000 + 12 * 1000 / 30
        // = 3930000 + 400 = 3930400
        var buffer = CreateValidTimecodePacket(frames: 12, seconds: 30, minutes: 5, hours: 1, type: 3);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(3930400.0, timecodeData.MillisecondsFromStart, 0.01);
    }

    [Test]
    public void TryParseTimecodePacket_PacketTooShort_ReturnsFalse()
    {
        // 18バイト (19バイト未満) のパケット
        var buffer = new byte[18];

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out _);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryParseTimecodePacket_EmptyBuffer_ReturnsFalse()
    {
        var buffer = new byte[0];

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out _);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryParseTimecodePacket_NullBuffer_ReturnsFalse()
    {
        var result = TimecodePacketParser.TryParseTimecodePacket(null, out _);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryParseTimecodePacket_WrongOpCode_ReturnsFalse()
    {
        // OpCode が Dmx (0x50) のパケット → TimeCode ではないためスキップ
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 0, hours: 0, type: 0);
        // OpCode を Dmx に変更
        buffer[8] = 0x00;
        buffer[9] = 0x50;

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out _);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryParseTimecodePacket_OpCodePoll_ReturnsFalse()
    {
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 0, hours: 0, type: 0);
        buffer[8] = 0x00;
        buffer[9] = 0x20; // Poll

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out _);

        Assert.IsFalse(result);
    }

    [Test]
    public void TryParseTimecodePacket_LargerThan19Bytes_StillParsesCorrectly()
    {
        // 19バイト以上のバッファでも正しくパースできることを確認
        var buffer = new byte[100];
        var validPacket = CreateValidTimecodePacket(frames: 5, seconds: 10, minutes: 20, hours: 2, type: 1);
        System.Array.Copy(validPacket, buffer, validPacket.Length);

        var result = TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.IsTrue(result);
        Assert.AreEqual(5, timecodeData.Frames);
        Assert.AreEqual(10, timecodeData.Seconds);
        Assert.AreEqual(20, timecodeData.Minutes);
        Assert.AreEqual(2, timecodeData.Hours);
        Assert.AreEqual(1, timecodeData.Type);
    }

    #endregion

    #region タイムコード → ミリ秒変換 - フレームレート種別対応

    [Test]
    public void ConvertToMilliseconds_Film24fps_CorrectConversion()
    {
        // Type 0 = Film (24fps)
        // 0時間0分1秒 + 12フレーム = 1000 + 12 * 1000 / 24 = 1000 + 500 = 1500
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 12, seconds: 1, minutes: 0, hours: 0, type: 0);

        Assert.AreEqual(1500.0, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_EBU25fps_CorrectConversion()
    {
        // Type 1 = EBU (25fps)
        // 0時間0分1秒 + 10フレーム = 1000 + 10 * 1000 / 25 = 1000 + 400 = 1400
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 10, seconds: 1, minutes: 0, hours: 0, type: 1);

        Assert.AreEqual(1400.0, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_DF29_97fps_CorrectConversion()
    {
        // Type 2 = DF (29.97fps)
        // 0時間0分1秒 + 15フレーム = 1000 + 15 * 1000 / 29.97
        // = 1000 + 500.500... ≈ 1500.50
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 15, seconds: 1, minutes: 0, hours: 0, type: 2);

        Assert.AreEqual(1000.0 + 15.0 * 1000.0 / 29.97, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_SMPTE30fps_CorrectConversion()
    {
        // Type 3 = SMPTE (30fps)
        // 0時間0分1秒 + 15フレーム = 1000 + 15 * 1000 / 30 = 1000 + 500 = 1500
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 15, seconds: 1, minutes: 0, hours: 0, type: 3);

        Assert.AreEqual(1500.0, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_ZeroTimecode_ReturnsZero()
    {
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 0, seconds: 0, minutes: 0, hours: 0, type: 0);

        Assert.AreEqual(0.0, ms, 0.001);
    }

    [Test]
    public void ConvertToMilliseconds_FullTimecode_CorrectConversion()
    {
        // 2時間30分45秒 + 20フレーム (EBU 25fps)
        // = (2*3600 + 30*60 + 45) * 1000 + 20 * 1000 / 25
        // = (7200 + 1800 + 45) * 1000 + 800
        // = 9045000 + 800 = 9045800
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 20, seconds: 45, minutes: 30, hours: 2, type: 1);

        Assert.AreEqual(9045800.0, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_MaxValues_NoOverflow()
    {
        // 最大値テスト: 23時間59分59秒 + 29フレーム (SMPTE 30fps)
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 29, seconds: 59, minutes: 59, hours: 23, type: 3);

        var expected = (23.0 * 3600 + 59 * 60 + 59) * 1000 + 29.0 * 1000.0 / 30.0;
        Assert.AreEqual(expected, ms, 0.01);
    }

    [Test]
    public void ConvertToMilliseconds_UnknownType_DefaultsTo30fps()
    {
        // 未知のType値 (4以上) の場合はデフォルト30fpsにフォールバック
        var ms = TimecodePacketParser.ConvertTimecodeToMilliseconds(
            frames: 15, seconds: 1, minutes: 0, hours: 0, type: 255);

        // 30fpsで計算: 1000 + 15 * 1000 / 30 = 1500
        Assert.AreEqual(1500.0, ms, 0.01);
    }

    #endregion

    #region パースとミリ秒変換の統合テスト

    [Test]
    public void TryParseTimecodePacket_Film24fps_CorrectMilliseconds()
    {
        // Type 0 = Film (24fps), 0h 0m 1s 12f
        var buffer = CreateValidTimecodePacket(frames: 12, seconds: 1, minutes: 0, hours: 0, type: 0);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(0, timecodeData.Type);
        Assert.AreEqual(1500.0, timecodeData.MillisecondsFromStart, 0.01);
    }

    [Test]
    public void TryParseTimecodePacket_EBU25fps_CorrectMilliseconds()
    {
        // Type 1 = EBU (25fps), 0h 1m 0s 0f
        var buffer = CreateValidTimecodePacket(frames: 0, seconds: 0, minutes: 1, hours: 0, type: 1);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(1, timecodeData.Type);
        Assert.AreEqual(60000.0, timecodeData.MillisecondsFromStart, 0.01);
    }

    [Test]
    public void TryParseTimecodePacket_DF29_97fps_CorrectMilliseconds()
    {
        // Type 2 = DF (29.97fps), 0h 0m 0s 1f
        var buffer = CreateValidTimecodePacket(frames: 1, seconds: 0, minutes: 0, hours: 0, type: 2);

        TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData);

        Assert.AreEqual(2, timecodeData.Type);
        Assert.AreEqual(1.0 * 1000.0 / 29.97, timecodeData.MillisecondsFromStart, 0.01);
    }

    #endregion

    #region GetFrameRate ヘルパー

    [Test]
    public void GetFrameRate_Type0_Returns24()
    {
        Assert.AreEqual(24.0, TimecodePacketParser.GetFrameRate(0), 0.001);
    }

    [Test]
    public void GetFrameRate_Type1_Returns25()
    {
        Assert.AreEqual(25.0, TimecodePacketParser.GetFrameRate(1), 0.001);
    }

    [Test]
    public void GetFrameRate_Type2_Returns29_97()
    {
        Assert.AreEqual(29.97, TimecodePacketParser.GetFrameRate(2), 0.001);
    }

    [Test]
    public void GetFrameRate_Type3_Returns30()
    {
        Assert.AreEqual(30.0, TimecodePacketParser.GetFrameRate(3), 0.001);
    }

    [Test]
    public void GetFrameRate_UnknownType_Returns30()
    {
        Assert.AreEqual(30.0, TimecodePacketParser.GetFrameRate(4), 0.001);
        Assert.AreEqual(30.0, TimecodePacketParser.GetFrameRate(255), 0.001);
    }

    #endregion

    #region ArtNet Timecodeパケット構造の検証

    [Test]
    public void TimecodePacket_MinimumLength_Is19Bytes()
    {
        // OpTimeCodeパケットの最小長は19バイトであること
        Assert.AreEqual(19, TimecodePacketParser.MinPacketLength);
    }

    [Test]
    public void TimecodePacket_FramesOffset_Is14()
    {
        Assert.AreEqual(14, TimecodePacketParser.FramesOffset);
    }

    [Test]
    public void TimecodePacket_SecondsOffset_Is15()
    {
        Assert.AreEqual(15, TimecodePacketParser.SecondsOffset);
    }

    [Test]
    public void TimecodePacket_MinutesOffset_Is16()
    {
        Assert.AreEqual(16, TimecodePacketParser.MinutesOffset);
    }

    [Test]
    public void TimecodePacket_HoursOffset_Is17()
    {
        Assert.AreEqual(17, TimecodePacketParser.HoursOffset);
    }

    [Test]
    public void TimecodePacket_TypeOffset_Is18()
    {
        Assert.AreEqual(18, TimecodePacketParser.TypeOffset);
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// 有効なArtNet OpTimeCodeパケット (19バイト) を生成する。
    /// パケット構造:
    ///   [0-7]  = "Art-Net\0"
    ///   [8-9]  = OpCode 0x9700 (リトルエンディアン: [8]=0x00, [9]=0x97)
    ///   [10]   = ProtVerHi = 0
    ///   [11]   = ProtVerLo = 14
    ///   [12]   = Filler1 = 0
    ///   [13]   = Filler2 = 0
    ///   [14]   = Frames
    ///   [15]   = Seconds
    ///   [16]   = Minutes
    ///   [17]   = Hours
    ///   [18]   = Type
    /// </summary>
    private static byte[] CreateValidTimecodePacket(byte frames, byte seconds, byte minutes, byte hours, byte type)
    {
        var buffer = new byte[19];
        // Art-Net ヘッダー
        buffer[0] = (byte)'A';
        buffer[1] = (byte)'r';
        buffer[2] = (byte)'t';
        buffer[3] = (byte)'-';
        buffer[4] = (byte)'N';
        buffer[5] = (byte)'e';
        buffer[6] = (byte)'t';
        buffer[7] = 0x00;
        // OpCode: 0x9700 リトルエンディアン
        buffer[8] = 0x00;
        buffer[9] = 0x97;
        // Protocol Version
        buffer[10] = 0x00; // ProtVerHi
        buffer[11] = 14;   // ProtVerLo
        // Fillers
        buffer[12] = 0x00;
        buffer[13] = 0x00;
        // Timecode fields
        buffer[14] = frames;
        buffer[15] = seconds;
        buffer[16] = minutes;
        buffer[17] = hours;
        buffer[18] = type;

        return buffer;
    }

    #endregion
}
