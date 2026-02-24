using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// DmxRecordData のユニバース数動的検出およびデータ型変更に関するテスト。
/// タスク 1.1: 録画データの最大ユニバース数を動的に検出する
/// </summary>
public class DmxRecordDataTests
{
    #region MaxUniverseCount - 最大ユニバース数の検出

    [Test]
    public void MaxUniverseCount_SingleUniverse0_Returns1()
    {
        // ユニバース番号0のみのパケット => MaxUniverseCount = 0 + 1 = 1
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 })
        };

        var data = new DmxRecordData(100.0, packets);

        Assert.AreEqual(1, data.MaxUniverseCount);
    }

    [Test]
    public void MaxUniverseCount_MultipleUniverses_ReturnsMaxPlusOne()
    {
        // ユニバース番号0, 1, 5が含まれるパケット => MaxUniverseCount = 5 + 1 = 6
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0, 1 }),
            CreatePacket(1, 200.0, new[] { 3, 5 }),
            CreatePacket(2, 300.0, new[] { 2 })
        };

        var data = new DmxRecordData(300.0, packets);

        Assert.AreEqual(6, data.MaxUniverseCount);
    }

    [Test]
    public void MaxUniverseCount_LargeUniverseNumber_ReturnsCorrectly()
    {
        // ユニバース番号63が含まれるパケット => MaxUniverseCount = 63 + 1 = 64
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0, 31, 63 })
        };

        var data = new DmxRecordData(100.0, packets);

        Assert.AreEqual(64, data.MaxUniverseCount);
    }

    [Test]
    public void MaxUniverseCount_EmptyPacketList_ReturnsMinimumOne()
    {
        // パケットが空のリスト => MaxUniverseCount は最低1を保証
        var packets = new List<DmxRecordPacket>();

        var data = new DmxRecordData(0.0, packets);

        Assert.AreEqual(1, data.MaxUniverseCount);
    }

    [Test]
    public void MaxUniverseCount_PacketsWithEmptyUniverseData_ReturnsMinimumOne()
    {
        // パケットはあるがUniverseDataが空 => MaxUniverseCount は最低1を保証
        var packets = new List<DmxRecordPacket>
        {
            new DmxRecordPacket
            {
                sequence = 0,
                time = 100.0,
                numUniverses = 0,
                data = new List<UniverseData>()
            }
        };

        var data = new DmxRecordData(100.0, packets);

        Assert.AreEqual(1, data.MaxUniverseCount);
    }

    [Test]
    public void MaxUniverseCount_HighestUniverseInMiddlePacket_DetectedCorrectly()
    {
        // 最大ユニバース番号が中間パケットにある場合でも正しく検出される
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0, 1 }),
            CreatePacket(1, 200.0, new[] { 0, 99 }),  // 最大のユニバース番号99がここ
            CreatePacket(2, 300.0, new[] { 0, 2 })
        };

        var data = new DmxRecordData(300.0, packets);

        Assert.AreEqual(100, data.MaxUniverseCount);
    }

    #endregion

    #region Data プロパティの型変更 - IReadOnlyList

    [Test]
    public void Data_ReturnsIReadOnlyList()
    {
        // Data プロパティが IReadOnlyList<DmxRecordPacket> を返すことを確認
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 })
        };

        var data = new DmxRecordData(100.0, packets);

        Assert.IsInstanceOf<IReadOnlyList<DmxRecordPacket>>(data.Data);
    }

    [Test]
    public void Data_SupportsIndexAccess()
    {
        // IReadOnlyList<T> のインデックスアクセスが可能であることを確認
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 }),
            CreatePacket(1, 200.0, new[] { 1 }),
            CreatePacket(2, 300.0, new[] { 2 })
        };

        var data = new DmxRecordData(300.0, packets);
        var readOnlyList = data.Data;

        Assert.AreEqual(0, readOnlyList[0].sequence);
        Assert.AreEqual(1, readOnlyList[1].sequence);
        Assert.AreEqual(2, readOnlyList[2].sequence);
    }

    [Test]
    public void Data_SupportsCountProperty()
    {
        // IReadOnlyList<T> の Count プロパティが正しく動作することを確認
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 }),
            CreatePacket(1, 200.0, new[] { 1 })
        };

        var data = new DmxRecordData(200.0, packets);

        Assert.AreEqual(2, data.Data.Count);
    }

    [Test]
    public void Data_IsEnumerable_BackwardCompatibility()
    {
        // IReadOnlyList<T> は IEnumerable<T> を実装するため、後方互換性を確認
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 }),
            CreatePacket(1, 200.0, new[] { 1 })
        };

        var data = new DmxRecordData(200.0, packets);

        // IEnumerable として使用可能
        IEnumerable<DmxRecordPacket> enumerable = data.Data;
        Assert.AreEqual(2, enumerable.Count());

        // foreach が使用可能
        int count = 0;
        foreach (var packet in data.Data)
        {
            count++;
        }
        Assert.AreEqual(2, count);
    }

    [Test]
    public void Data_SupportsLinqLast_BackwardCompatibility()
    {
        // 既存コードの .Last() 呼び出しとの後方互換性
        var packets = new List<DmxRecordPacket>
        {
            CreatePacket(0, 100.0, new[] { 0 }),
            CreatePacket(1, 200.0, new[] { 1 }),
            CreatePacket(2, 300.0, new[] { 2 })
        };

        var data = new DmxRecordData(300.0, packets);

        var lastPacket = data.Data.Last();
        Assert.AreEqual(2, lastPacket.sequence);
        Assert.AreEqual(300.0, lastPacket.time);
    }

    #endregion

    #region ヘルパーメソッド

    private static DmxRecordPacket CreatePacket(int sequence, double time, int[] universeNumbers)
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

    #endregion
}
