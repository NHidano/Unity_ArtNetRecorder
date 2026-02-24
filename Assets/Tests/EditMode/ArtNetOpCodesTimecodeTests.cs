using NUnit.Framework;
using ProjectBlue;

/// <summary>
/// ArtNetOpCodes の Timecodeエントリ追加に関するテスト。
/// タスク 3.1: ArtNet OpCode定義にタイムコードを追加する
/// </summary>
public class ArtNetOpCodesTimecodeTests
{
    #region TimeCode OpCode定義

    [Test]
    public void TimeCode_HasCorrectValue_0x97()
    {
        // OpTimeCode は 0x97 であること (ArtNet仕様: 0x9700 リトルエンディアン)
        Assert.AreEqual(0x97, (int)ArtNetOpCodes.TimeCode);
    }

    [Test]
    public void TimeCode_ExistsInEnum()
    {
        // TimeCode がenum内に存在し、定義済みの値であること
        Assert.IsTrue(System.Enum.IsDefined(typeof(ArtNetOpCodes), ArtNetOpCodes.TimeCode));
    }

    #endregion

    #region 既存OpCodeとの互換性

    [Test]
    public void TimeCode_DoesNotConflictWithExistingOpCodes()
    {
        // TimeCode の値が既存のOpCodeと重複しないこと
        Assert.AreNotEqual((int)ArtNetOpCodes.None, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.Poll, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.PollReply, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.Dmx, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.TodRequest, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.TodData, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.TodControl, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.Rdm, (int)ArtNetOpCodes.TimeCode);
        Assert.AreNotEqual((int)ArtNetOpCodes.RdmSub, (int)ArtNetOpCodes.TimeCode);
    }

    [Test]
    public void ExistingOpCodes_RetainOriginalValues()
    {
        // 既存のOpCodeの値が変更されていないことを確認
        Assert.AreEqual(0x00, (int)ArtNetOpCodes.None);
        Assert.AreEqual(0x20, (int)ArtNetOpCodes.Poll);
        Assert.AreEqual(0x21, (int)ArtNetOpCodes.PollReply);
        Assert.AreEqual(0x50, (int)ArtNetOpCodes.Dmx);
        Assert.AreEqual(0x80, (int)ArtNetOpCodes.TodRequest);
        Assert.AreEqual(0x81, (int)ArtNetOpCodes.TodData);
        Assert.AreEqual(0x82, (int)ArtNetOpCodes.TodControl);
        Assert.AreEqual(0x83, (int)ArtNetOpCodes.Rdm);
        Assert.AreEqual(0x84, (int)ArtNetOpCodes.RdmSub);
    }

    #endregion

    #region GetOpCode との互換性

    [Test]
    public void GetOpCode_WithTimecodePacket_ReturnsTimeCode()
    {
        // ArtNet Timecodeパケット (OpCode 0x9700 リトルエンディアン) を受信した場合、
        // GetOpCode が TimeCode を返すことを確認
        // パケット構造: [0-7]="Art-Net\0", [8]=0x00(lo), [9]=0x97(hi)
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
        // OpCode: 0x9700 リトルエンディアン => buffer[8]=0x00, buffer[9]=0x97
        buffer[8] = 0x00;
        buffer[9] = 0x97;

        var opCode = ArtNetPacketUtillity.GetOpCode(buffer);

        Assert.AreEqual(ArtNetOpCodes.TimeCode, opCode);
    }

    [Test]
    public void GetOpCode_WithDmxPacket_StillReturnsDmx()
    {
        // 既存のDmxパケット判定が変更されていないことを確認
        var buffer = new byte[18];
        buffer[0] = (byte)'A';
        buffer[1] = (byte)'r';
        buffer[2] = (byte)'t';
        buffer[3] = (byte)'-';
        buffer[4] = (byte)'N';
        buffer[5] = (byte)'e';
        buffer[6] = (byte)'t';
        buffer[7] = 0x00;
        // OpCode: 0x5000 リトルエンディアン => buffer[8]=0x00, buffer[9]=0x50
        buffer[8] = 0x00;
        buffer[9] = 0x50;

        var opCode = ArtNetPacketUtillity.GetOpCode(buffer);

        Assert.AreEqual(ArtNetOpCodes.Dmx, opCode);
    }

    [Test]
    public void GetOpCode_WithTimecodePacket_DoesNotReturnDmx()
    {
        // Timecodeパケットが誤ってDmxと判定されないことを確認
        var buffer = new byte[19];
        buffer[0] = (byte)'A';
        buffer[1] = (byte)'r';
        buffer[2] = (byte)'t';
        buffer[3] = (byte)'-';
        buffer[4] = (byte)'N';
        buffer[5] = (byte)'e';
        buffer[6] = (byte)'t';
        buffer[7] = 0x00;
        buffer[8] = 0x00;
        buffer[9] = 0x97;

        var opCode = ArtNetPacketUtillity.GetOpCode(buffer);

        Assert.AreNotEqual(ArtNetOpCodes.Dmx, opCode);
    }

    #endregion
}
