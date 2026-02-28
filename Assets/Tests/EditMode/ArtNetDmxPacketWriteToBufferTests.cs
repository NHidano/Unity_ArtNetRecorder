using NUnit.Framework;

/// <summary>
/// ArtNetDmxPacket.WriteToBuffer() のバイナリ互換テスト。
/// WriteToBuffer() の出力が既存 ToArray() の出力とバイト単位で一致することを検証する。
/// </summary>
public class ArtNetDmxPacketWriteToBufferTests
{
    [Test]
    public void WriteToBuffer_Universe0_AllZeroDmx_MatchesToArray()
    {
        var dmxData = new byte[512];
        short universe = 0;

        var expected = CreatePacketViaToArray(universe, dmxData);
        var buffer = new byte[ArtNetDmxPacket.PacketSize];
        var len = ArtNetDmxPacket.WriteToBuffer(buffer, universe, dmxData);

        Assert.AreEqual(expected.Length, len);
        Assert.AreEqual(expected, buffer);
    }

    [Test]
    public void WriteToBuffer_Universe1_MatchesToArray()
    {
        var dmxData = new byte[512];
        dmxData[0] = 255;
        dmxData[511] = 128;
        short universe = 1;

        var expected = CreatePacketViaToArray(universe, dmxData);
        var buffer = new byte[ArtNetDmxPacket.PacketSize];
        var len = ArtNetDmxPacket.WriteToBuffer(buffer, universe, dmxData);

        Assert.AreEqual(expected.Length, len);
        Assert.AreEqual(expected, buffer);
    }

    [Test]
    public void WriteToBuffer_HighUniverse_MatchesToArray()
    {
        var dmxData = new byte[512];
        for (int i = 0; i < 512; i++) dmxData[i] = (byte)(i % 256);
        short universe = 32767;

        var expected = CreatePacketViaToArray(universe, dmxData);
        var buffer = new byte[ArtNetDmxPacket.PacketSize];
        var len = ArtNetDmxPacket.WriteToBuffer(buffer, universe, dmxData);

        Assert.AreEqual(expected.Length, len);
        Assert.AreEqual(expected, buffer);
    }

    [Test]
    public void WriteToBuffer_ReturnsPacketSize530()
    {
        var dmxData = new byte[512];
        var buffer = new byte[ArtNetDmxPacket.PacketSize];
        var len = ArtNetDmxPacket.WriteToBuffer(buffer, 0, dmxData);

        Assert.AreEqual(530, len);
    }

    [Test]
    public void WriteToBuffer_MultipleCallsReuseSameBuffer_ProducesCorrectOutput()
    {
        var buffer = new byte[ArtNetDmxPacket.PacketSize];

        // 1回目: Universe 0
        var dmxData1 = new byte[512];
        dmxData1[0] = 100;
        ArtNetDmxPacket.WriteToBuffer(buffer, 0, dmxData1);
        var expected1 = CreatePacketViaToArray(0, dmxData1);
        Assert.AreEqual(expected1, buffer);

        // 2回目: 同じバッファで Universe 5, 異なるデータ
        var dmxData2 = new byte[512];
        dmxData2[0] = 200;
        dmxData2[255] = 50;
        ArtNetDmxPacket.WriteToBuffer(buffer, 5, dmxData2);
        var expected2 = CreatePacketViaToArray(5, dmxData2);
        Assert.AreEqual(expected2, buffer);
    }

    private static byte[] CreatePacketViaToArray(short universe, byte[] dmxData)
    {
        var packet = new ArtNetDmxPacket
        {
            Universe = universe,
            DmxData = dmxData
        };
        return packet.ToArray();
    }
}
