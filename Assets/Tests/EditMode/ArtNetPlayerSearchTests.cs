using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// ArtNetPlayer の二分探索ベースDMXデータ探索に関するテスト。
/// タスク 1.2: DMXデータ探索を二分探索に最適化する
/// </summary>
public class ArtNetPlayerSearchTests
{
    #region FindPacketIndex - 二分探索によるパケットインデックス検出

    [Test]
    public void FindPacketIndex_HeaderAtExactPacketTime_ReturnsCorrectIndex()
    {
        // ヘッダーがパケットの正確な時間と一致する場合、そのパケットのインデックスを返す
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0, 400.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 200.0, -1);

        Assert.AreEqual(2, index);
    }

    [Test]
    public void FindPacketIndex_HeaderBetweenPackets_ReturnsFirstPacketAtOrAfterHeader()
    {
        // ヘッダーが2つのパケット時間の間にある場合、header以上の最初のパケットインデックスを返す
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0, 400.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 150.0, -1);

        Assert.AreEqual(2, index);
    }

    [Test]
    public void FindPacketIndex_HeaderBeforeFirstPacket_ReturnsZero()
    {
        // ヘッダーが最初のパケットより前の場合、インデックス0を返す
        var packets = CreatePacketList(new[] { 100.0, 200.0, 300.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 50.0, -1);

        Assert.AreEqual(0, index);
    }

    [Test]
    public void FindPacketIndex_HeaderAfterLastPacket_ReturnsLastIndex()
    {
        // ヘッダーが最後のパケットより後の場合、最後のインデックスを返す
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 500.0, -1);

        Assert.AreEqual(2, index);
    }

    [Test]
    public void FindPacketIndex_HeaderAtZero_ReturnsZero()
    {
        // ヘッダーが0の場合、インデックス0を返す
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 0.0, -1);

        Assert.AreEqual(0, index);
    }

    [Test]
    public void FindPacketIndex_SinglePacket_ReturnsZero()
    {
        // パケットが1つだけの場合、常にインデックス0を返す
        var packets = CreatePacketList(new[] { 100.0 });

        Assert.AreEqual(0, DmxPacketSearcher.FindPacketIndex(packets, 0.0, -1));
        Assert.AreEqual(0, DmxPacketSearcher.FindPacketIndex(packets, 100.0, -1));
        Assert.AreEqual(0, DmxPacketSearcher.FindPacketIndex(packets, 200.0, -1));
    }

    [Test]
    public void FindPacketIndex_LargeDataset_FindsCorrectly()
    {
        // 大量のパケット（1000件）でも正しくインデックスを検出する
        var times = new double[1000];
        for (int i = 0; i < 1000; i++)
        {
            times[i] = i * 10.0; // 0, 10, 20, ..., 9990
        }
        var packets = CreatePacketList(times);

        // 正確な時間
        Assert.AreEqual(500, DmxPacketSearcher.FindPacketIndex(packets, 5000.0, -1));

        // 中間の時間 (5005.0 は 5010.0のパケットに対応)
        Assert.AreEqual(501, DmxPacketSearcher.FindPacketIndex(packets, 5005.0, -1));
    }

    #endregion

    #region FindPacketIndex - インデックスキャッシュによる前方探索最適化

    [Test]
    public void FindPacketIndex_WithCachedIndex_ForwardSeek_FindsCorrectly()
    {
        // 前回のインデックスから前方探索で高速に検出する
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0, 400.0, 500.0 });

        // 前回インデックス2 (time=200.0) から、header=350.0 を探索 => インデックス4 (time=400.0)
        // ただし、300.0 >= 350.0 は偽なので、400.0 >= 350.0 が真 => インデックス4
        // 修正: 300.0 >= 350.0 は偽、400.0 >= 350.0 は真 => インデックス4
        var index = DmxPacketSearcher.FindPacketIndex(packets, 350.0, 2);

        Assert.AreEqual(4, index);
    }

    [Test]
    public void FindPacketIndex_WithCachedIndex_ExactNextPacket_FindsCorrectly()
    {
        // 連続再生時：前回インデックスから1つ先のパケットを高速に検出する
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0, 400.0 });

        // 前回インデックス1 (time=100.0) から、header=200.0 を探索 => インデックス2
        var index = DmxPacketSearcher.FindPacketIndex(packets, 200.0, 1);

        Assert.AreEqual(2, index);
    }

    [Test]
    public void FindPacketIndex_WithCachedIndex_BackwardSeek_FallsBackToBinarySearch()
    {
        // シークバーを後方に戻した場合、キャッシュは使えず二分探索にフォールバックする
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0, 400.0 });

        // 前回インデックス4 (time=400.0) から、header=100.0 を探索 => インデックス1
        var index = DmxPacketSearcher.FindPacketIndex(packets, 100.0, 4);

        Assert.AreEqual(1, index);
    }

    [Test]
    public void FindPacketIndex_WithCachedIndex_SamePosition_ReturnsCachedIndex()
    {
        // 同じ位置のヘッダーの場合、キャッシュされたインデックスを返す
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0 });

        // 前回インデックス2 (time=200.0) で、header=200.0 を探索 => インデックス2
        var index = DmxPacketSearcher.FindPacketIndex(packets, 200.0, 2);

        Assert.AreEqual(2, index);
    }

    [Test]
    public void FindPacketIndex_WithInvalidCachedIndex_FallsBackToBinarySearch()
    {
        // キャッシュインデックスが無効（-1）の場合、二分探索を使用する
        var packets = CreatePacketList(new[] { 0.0, 100.0, 200.0, 300.0 });

        var index = DmxPacketSearcher.FindPacketIndex(packets, 200.0, -1);

        Assert.AreEqual(2, index);
    }

    #endregion

    #region ReadAndSendの動作テスト（バッファ・ユニバース境界チェック）

    [Test]
    public void CopyPacketToBuffers_UniverseWithinBounds_CopiesCorrectly()
    {
        // バッファサイズ内のユニバース番号は正しくコピーされる
        int maxUniverse = 4;
        var dmx = CreateDmxBuffer(maxUniverse);
        var dmxRaw = new float[maxUniverse * 512];

        var packet = CreatePacketWithData(0, 0.0, new[] { 0, 1, 2, 3 });
        // ユニバース0のチャンネル0に値42を設定
        packet.data[0].data[0] = 42;
        // ユニバース3のチャンネル511に値255を設定
        packet.data[3].data[511] = 255;

        DmxPacketSearcher.CopyPacketToBuffers(packet, dmx, dmxRaw, maxUniverse);

        Assert.AreEqual(42, dmx[0][0]);
        Assert.AreEqual(255, dmx[3][511]);
        Assert.AreEqual(42f, dmxRaw[0 * 512 + 0]);
        Assert.AreEqual(255f, dmxRaw[3 * 512 + 511]);
    }

    [Test]
    public void CopyPacketToBuffers_UniverseExceedsBuffer_SkipsWithoutError()
    {
        // ユニバース番号がバッファサイズを超過する場合、スキップしてエラーにならない
        int maxUniverse = 2;
        var dmx = CreateDmxBuffer(maxUniverse);
        var dmxRaw = new float[maxUniverse * 512];

        // ユニバース0は範囲内、ユニバース5は範囲外
        var universeDataList = new List<UniverseData>
        {
            new UniverseData { universe = 0, data = new byte[512] },
            new UniverseData { universe = 5, data = new byte[512] }
        };
        universeDataList[0].data[0] = 100;
        universeDataList[1].data[0] = 200;

        var packet = new DmxRecordPacket
        {
            sequence = 0, time = 0.0, numUniverses = 2, data = universeDataList
        };

        // 例外が発生しないことを確認
        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(packet, dmx, dmxRaw, maxUniverse));

        // 範囲内のユニバース0は正しくコピーされる
        Assert.AreEqual(100, dmx[0][0]);
        Assert.AreEqual(100f, dmxRaw[0]);
    }

    [Test]
    public void CopyPacketToBuffers_NegativeUniverse_SkipsWithoutError()
    {
        // 負のユニバース番号はスキップされる（異常データ対策）
        int maxUniverse = 2;
        var dmx = CreateDmxBuffer(maxUniverse);
        var dmxRaw = new float[maxUniverse * 512];

        var universeDataList = new List<UniverseData>
        {
            new UniverseData { universe = -1, data = new byte[512] },
            new UniverseData { universe = 0, data = new byte[512] }
        };
        universeDataList[1].data[0] = 50;

        var packet = new DmxRecordPacket
        {
            sequence = 0, time = 0.0, numUniverses = 2, data = universeDataList
        };

        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(packet, dmx, dmxRaw, maxUniverse));

        Assert.AreEqual(50, dmx[0][0]);
    }

    [Test]
    public void CopyPacketToBuffers_AllDataCopiedToRawBuffer()
    {
        // dmxRaw配列にすべてのユニバースのチャンネルデータが正しくフラット化される
        int maxUniverse = 2;
        var dmx = CreateDmxBuffer(maxUniverse);
        var dmxRaw = new float[maxUniverse * 512];

        var packet = CreatePacketWithData(0, 0.0, new[] { 0, 1 });
        // ユニバース0のチャンネル0=10, チャンネル1=20
        packet.data[0].data[0] = 10;
        packet.data[0].data[1] = 20;
        // ユニバース1のチャンネル0=30, チャンネル1=40
        packet.data[1].data[0] = 30;
        packet.data[1].data[1] = 40;

        DmxPacketSearcher.CopyPacketToBuffers(packet, dmx, dmxRaw, maxUniverse);

        // ユニバース0のチャンネル
        Assert.AreEqual(10f, dmxRaw[0 * 512 + 0]);
        Assert.AreEqual(20f, dmxRaw[0 * 512 + 1]);
        // ユニバース1のチャンネル
        Assert.AreEqual(30f, dmxRaw[1 * 512 + 0]);
        Assert.AreEqual(40f, dmxRaw[1 * 512 + 1]);
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// 指定した時間リストからパケットリストを作成する（ユニバースデータは空）
    /// </summary>
    private static IReadOnlyList<DmxRecordPacket> CreatePacketList(double[] times)
    {
        var packets = new List<DmxRecordPacket>();
        for (int i = 0; i < times.Length; i++)
        {
            packets.Add(new DmxRecordPacket
            {
                sequence = i,
                time = times[i],
                numUniverses = 1,
                data = new List<UniverseData>
                {
                    new UniverseData { universe = 0, data = new byte[512] }
                }
            });
        }
        return packets;
    }

    /// <summary>
    /// 指定したユニバース番号配列でパケットを作成する（データは初期値0）
    /// </summary>
    private static DmxRecordPacket CreatePacketWithData(int sequence, double time, int[] universeNumbers)
    {
        var universeDataList = new List<UniverseData>();
        foreach (var universeNum in universeNumbers)
        {
            universeDataList.Add(new UniverseData
            {
                universe = universeNum,
                data = new byte[512]
            });
        }

        return new DmxRecordPacket
        {
            sequence = sequence,
            time = time,
            numUniverses = universeNumbers.Length,
            data = universeDataList
        };
    }

    /// <summary>
    /// DMXバッファ（byte[][]）を作成する
    /// </summary>
    private static byte[][] CreateDmxBuffer(int maxUniverse)
    {
        var dmx = new byte[maxUniverse][];
        for (int i = 0; i < maxUniverse; i++)
        {
            dmx[i] = new byte[512];
        }
        return dmx;
    }

    #endregion
}
