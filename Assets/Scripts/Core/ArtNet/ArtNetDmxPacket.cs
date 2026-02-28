public class ArtNetDmxPacket : ArtNetPacket
{
    public const int PacketSize = 530;

    public ArtNetDmxPacket()
        : base(ArtNetOpCodes.Dmx)
    {
    }

    #region Packet Properties

    public byte Sequence { get; set; } = 0;

    public byte Physical { get; set; } = 0;

    public short Universe { get; set; } = 0;

    public short Length
    {
        get
        {
            if (DmxData == null)
                return 0;
            return (short)DmxData.Length;
        }
    }

    public byte[] DmxData { get; set; } = null;

    public override void WriteData(ArtNetBinaryWriter data)
    {
        base.WriteData(data);

        data.Write(Sequence);
        data.Write(Physical);
        data.Write(Universe);
        data.WriteNetwork(Length);
        data.Write(DmxData);
    }

    /// <summary>
    /// GCアロケーションなしでArtNet DMXパケットを既存バッファに書き込む。
    /// バッファは最低 PacketSize (530) バイト必要。
    /// </summary>
    public static int WriteToBuffer(byte[] buffer, short universe, byte[] dmxData)
    {
        // "Art-Net\0" (8 bytes)
        buffer[0] = 0x41; // A
        buffer[1] = 0x72; // r
        buffer[2] = 0x74; // t
        buffer[3] = 0x2D; // -
        buffer[4] = 0x4E; // N
        buffer[5] = 0x65; // e
        buffer[6] = 0x74; // t
        buffer[7] = 0x00; // \0

        // OpCode 0x0050 (Big Endian)
        buffer[8] = 0x00;
        buffer[9] = 0x50;

        // Version 14 (Big Endian)
        buffer[10] = 0x00;
        buffer[11] = 0x0E;

        // Sequence
        buffer[12] = 0x00;

        // Physical
        buffer[13] = 0x00;

        // Universe (Little Endian)
        buffer[14] = (byte)(universe & 0xFF);
        buffer[15] = (byte)((universe >> 8) & 0xFF);

        // Length 512 (Big Endian)
        buffer[16] = 0x02;
        buffer[17] = 0x00;

        // DmxData
        System.Buffer.BlockCopy(dmxData, 0, buffer, 18, dmxData.Length);

        return PacketSize;
    }

    #endregion
}
