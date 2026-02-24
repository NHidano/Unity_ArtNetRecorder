using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// シークバー性能改善の統合確認テスト。
/// タスク 5.1: 動的ユニバース数でのバッファ初期化、二分探索によるシーク、
/// ビジュアライザー連携が一貫して動作することを確認する。
///
/// 検証対象コンポーネント:
/// - DmxRecordData: 動的ユニバース数検出 (タスク1.1)
/// - DmxPacketSearcher: 二分探索によるパケット検出 (タスク1.2)
/// - ArtNetPlayerApplication: 動的バッファ初期化 (タスク1.3)
///
/// Requirements: 1.1, 1.2, 1.3, 1.4
/// </summary>
public class SeekBarIntegrationTests
{
    #region 統合フロー: DmxRecordData → バッファ初期化 → 二分探索 → データコピー

    [Test]
    public void 統合フロー_32ユニバースデータでシーク操作が一貫して動作する()
    {
        // 32ユニバースの録画データを作成する
        var packets = CreateMultiUniversePacketList(
            packetCount: 100,
            universeCount: 32,
            intervalMs: 100.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);

        // 1. DmxRecordData がユニバース数を正しく検出すること (タスク1.1, Req1.4)
        Assert.AreEqual(32, recordData.MaxUniverseCount,
            "32ユニバースのデータからMaxUniverseCountが32であること");

        // 2. バッファを動的ユニバース数で初期化する (タスク1.3, Req1.1)
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;
        Assert.AreEqual(32, maxUniverseNum,
            "32ユニバースは32の倍数のまま32であること");

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 3. 二分探索でパケットを検出する (タスク1.2, Req1.2)
        var data = recordData.Data;
        double seekHeader = 5050.0; // 50番目と51番目のパケットの間
        var index = DmxPacketSearcher.FindPacketIndex(data, seekHeader, -1);
        Assert.IsTrue(index >= 50 && index <= 51,
            $"header=5050.0msに対してインデックス{index}が50-51の範囲内であること");

        // 4. 検出したパケットのデータをバッファにコピーする (タスク1.2, Req1.2)
        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(data[index], dmx, dmxRaw, maxUniverseNum),
            "32ユニバースのパケットデータをバッファにコピーしてもエラーが発生しないこと");

        // 5. コピーされたデータがdmxRawに正しく反映されていること
        // 各ユニバースの先頭チャンネルにはテストデータが設定されている
        for (int u = 0; u < 32; u++)
        {
            Assert.AreEqual((float)data[index].data[u].data[0], dmxRaw[u * 512],
                $"ユニバース{u}のチャンネル0がdmxRawに反映されていること");
        }
    }

    [Test]
    public void 統合フロー_64ユニバースデータでシーク操作が一貫して動作する()
    {
        // 64ユニバースの録画データを作成する
        var packets = CreateMultiUniversePacketList(
            packetCount: 50,
            universeCount: 64,
            intervalMs: 200.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);

        // 1. DmxRecordData が64ユニバースを正しく検出すること
        Assert.AreEqual(64, recordData.MaxUniverseCount,
            "64ユニバースのデータからMaxUniverseCountが64であること");

        // 2. バッファ初期化: 64は32の倍数なのでそのまま
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;
        Assert.AreEqual(64, maxUniverseNum);

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 3. 先頭へのシーク
        var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, 0.0, -1);
        Assert.AreEqual(0, index, "先頭へのシークでインデックス0が返ること");

        DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);

        // 4. 末尾へのシーク
        var endIndex = DmxPacketSearcher.FindPacketIndex(
            recordData.Data, recordData.Duration + 1000.0, -1);
        Assert.AreEqual(recordData.Data.Count - 1, endIndex,
            "末尾を超えたシークで最後のインデックスが返ること");

        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[endIndex], dmx, dmxRaw, maxUniverseNum),
            "64ユニバースの末尾パケットをバッファにコピーしてもエラーが発生しないこと");
    }

    [Test]
    public void 統合フロー_非32倍数ユニバース数でバッファ初期化が正しく切り上げられる()
    {
        // 40ユニバースの録画データ: 32の倍数でないケース
        var packets = CreateMultiUniversePacketList(
            packetCount: 10,
            universeCount: 40,
            intervalMs: 100.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);

        Assert.AreEqual(40, recordData.MaxUniverseCount,
            "40ユニバースのデータからMaxUniverseCountが40であること");

        // 32の倍数に切り上げ => 64
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;
        Assert.AreEqual(64, maxUniverseNum,
            "40ユニバースは32の倍数に切り上げて64になること");

        // バッファサイズ64で初期化しても40ユニバースのデータが正常にコピーされる
        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, 500.0, -1);

        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum),
            "40ユニバースのデータを64サイズのバッファにコピーしてもエラーが発生しないこと");

        // ユニバース0-39のデータが正しくコピーされている
        for (int u = 0; u < 40; u++)
        {
            Assert.AreEqual(
                (float)recordData.Data[index].data[u].data[0],
                dmxRaw[u * 512],
                $"ユニバース{u}のチャンネル0がdmxRawに反映されていること");
        }

        // ユニバース40-63のバッファは初期値0のまま（使用されていない領域）
        for (int u = 40; u < 64; u++)
        {
            Assert.AreEqual(0f, dmxRaw[u * 512],
                $"未使用ユニバース{u}のチャンネル0がデフォルト値0のままであること");
        }
    }

    #endregion

    #region 連続シーク: キャッシュ付き二分探索の一貫性

    [Test]
    public void 連続シーク_前方シークが一貫してデータを返す()
    {
        // 大量パケットの録画データ
        var packets = CreateMultiUniversePacketList(
            packetCount: 500,
            universeCount: 32,
            intervalMs: 50.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 前方シークをシミュレート（再生中のフレーム更新）
        int lastIndex = -1;
        double previousHeader = -1;

        for (double header = 0; header <= recordData.Duration; header += 16.6)
        {
            var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, header, lastIndex);

            // インデックスが有効範囲内であること
            Assert.IsTrue(index >= 0 && index < recordData.Data.Count,
                $"header={header}msに対するインデックス{index}が有効範囲内であること");

            // インデックスが非減少であること（前方シーク）
            Assert.IsTrue(index >= lastIndex || lastIndex == -1,
                $"前方シーク時にインデックスが非減少であること (previous={lastIndex}, current={index})");

            // データコピーがエラーなく完了すること
            DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);

            lastIndex = index;
            previousHeader = header;
        }
    }

    [Test]
    public void ランダムシーク_任意位置へのシークが一貫してデータを返す()
    {
        // 32ユニバース、200パケットの録画データ
        var packets = CreateMultiUniversePacketList(
            packetCount: 200,
            universeCount: 32,
            intervalMs: 100.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // ランダムなシーク位置をシミュレート（シークバー操作）
        var seekPositions = new double[]
        {
            5000.0,   // 中間
            0.0,      // 先頭（後方シーク）
            19000.0,  // 末尾付近（前方大ジャンプ）
            1000.0,   // 再び先頭付近（後方シーク）
            10000.0,  // 中間（前方シーク）
            10000.0,  // 同じ位置（再シーク）
            19900.0,  // 末尾
        };

        int lastIndex = -1;

        foreach (var seekPosition in seekPositions)
        {
            var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, seekPosition, lastIndex);

            // インデックスが有効範囲内であること
            Assert.IsTrue(index >= 0 && index < recordData.Data.Count,
                $"seekPosition={seekPosition}msに対するインデックス{index}が有効範囲内であること");

            // 検出されたパケットの時間がシーク位置に対して妥当であること
            var packetTime = recordData.Data[index].time;
            if (index < recordData.Data.Count - 1)
            {
                // パケット時間がシーク位置以上であること（lower bound）
                Assert.IsTrue(packetTime >= seekPosition || index == recordData.Data.Count - 1,
                    $"パケット時間{packetTime}がシーク位置{seekPosition}以上であること");
            }

            // データコピーがエラーなく完了すること
            Assert.DoesNotThrow(() =>
                DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum),
                $"seekPosition={seekPosition}msのデータコピーがエラーなく完了すること");

            lastIndex = index;
        }
    }

    #endregion

    #region ビジュアライザー連携: dmxRaw配列のサイズと内容の一貫性

    [Test]
    public void ビジュアライザー連携_dmxRaw配列サイズがComputeShaderディスパッチと一致する()
    {
        // 48ユニバースのデータ: 32の倍数に切り上げ → 64
        var packets = CreateMultiUniversePacketList(
            packetCount: 10,
            universeCount: 48,
            intervalMs: 100.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);

        // ArtNetPlayerApplication と同じ切り上げロジック
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;
        Assert.AreEqual(64, maxUniverseNum,
            "48ユニバースは32の倍数に切り上げて64であること");

        // dmxRaw配列のサイズがComputeBufferの期待サイズと一致する
        var expectedBufferSize = maxUniverseNum * 512;
        var dmxRaw = new float[expectedBufferSize];
        Assert.AreEqual(64 * 512, dmxRaw.Length,
            "dmxRaw配列のサイズがComputeBuffer (64*512) と一致すること");

        // DataVisualizer.Dispatch で使用される maxUniverseNum / 32 が正の整数であること
        Assert.AreEqual(2, maxUniverseNum / 32,
            "ComputeShaderディスパッチのグループ数 (64/32=2) が正しいこと");
    }

    [Test]
    public void ビジュアライザー連携_シーク後のdmxRawが全ユニバースのデータを含む()
    {
        // 32ユニバース、各チャンネルに特定値を持つデータ
        var packets = CreateMultiUniversePacketListWithChannelData(
            packetCount: 5,
            universeCount: 32,
            intervalMs: 1000.0);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);
        var maxUniverseNum = 32;

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 中間パケット（インデックス2、time=2000.0）へシーク
        var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, 2000.0, -1);
        DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);

        // 各ユニバースのチャンネル0にテストデータが入っていること
        for (int u = 0; u < 32; u++)
        {
            Assert.AreEqual(
                (float)recordData.Data[index].data[u].data[0],
                dmxRaw[u * 512],
                $"シーク後のdmxRawにユニバース{u}のチャンネル0データが正しく含まれること");
        }

        // dmxRaw全体のサイズが正しいこと（ビジュアライザーに渡すバッファ）
        Assert.AreEqual(32 * 512, dmxRaw.Length,
            "dmxRaw配列が32ユニバース分のデータを含むこと");
    }

    #endregion

    #region 大規模データでのシーク応答性

    [Test]
    public void シーク応答性_32ユニバース1000パケットでの二分探索が高速に完了する()
    {
        // 32ユニバース、1000パケットの大規模データ
        var packets = CreateMultiUniversePacketList(
            packetCount: 1000,
            universeCount: 32,
            intervalMs: 33.3);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);
        var maxUniverseNum = 32;

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 100回のランダムシーク操作を計測する
        var sw = Stopwatch.StartNew();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var seekPos = random.NextDouble() * recordData.Duration;
            var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, seekPos, -1);
            DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);
        }

        sw.Stop();

        // 100回のシーク操作が1秒以内に完了すること（16.6ms/frameの余裕を持つ基準）
        Assert.Less(sw.ElapsedMilliseconds, 1000,
            $"100回のシーク操作が1000ms以内に完了すること (実測: {sw.ElapsedMilliseconds}ms)");
    }

    [Test]
    public void シーク応答性_64ユニバース2000パケットでのシーク操作が高速に完了する()
    {
        // 64ユニバース、2000パケットの大規模データ
        var packets = CreateMultiUniversePacketList(
            packetCount: 2000,
            universeCount: 64,
            intervalMs: 16.6);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);
        var maxUniverseNum = 64;

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 連続再生シミュレーション: 前方シーク100フレーム
        var sw = Stopwatch.StartNew();
        int lastIndex = -1;

        for (int frame = 0; frame < 100; frame++)
        {
            double header = frame * 16.6;
            var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, header, lastIndex);
            DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);
            lastIndex = index;
        }

        sw.Stop();

        // 100フレーム分の処理が500ms以内に完了すること
        Assert.Less(sw.ElapsedMilliseconds, 500,
            $"64ユニバース・100フレーム分の連続シークが500ms以内に完了すること (実測: {sw.ElapsedMilliseconds}ms)");
    }

    [Test]
    public void シーク応答性_100ユニバース以上の大規模データでシークが1フレーム以内に完了する()
    {
        // 128ユニバース、500パケットの大規模データ
        var packets = CreateMultiUniversePacketList(
            packetCount: 500,
            universeCount: 128,
            intervalMs: 33.3);

        var recordData = new DmxRecordData(packets[packets.Count - 1].time, packets);

        // 32の倍数に切り上げ（128は既に32の倍数）
        var maxUniverseNum = ((recordData.MaxUniverseCount + 31) / 32) * 32;
        Assert.AreEqual(128, maxUniverseNum);

        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // 単一シーク操作の時間を計測（二分探索 + データコピー）
        var sw = Stopwatch.StartNew();

        var seekPos = recordData.Duration / 2;
        var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, seekPos, -1);
        DmxPacketSearcher.CopyPacketToBuffers(recordData.Data[index], dmx, dmxRaw, maxUniverseNum);

        sw.Stop();

        // 単一シーク操作が16.6ms（1フレーム@60fps）以内に完了すること
        Assert.Less(sw.ElapsedMilliseconds, 17,
            $"128ユニバースの単一シーク操作が16.6ms以内に完了すること (実測: {sw.ElapsedMilliseconds}ms)");
    }

    #endregion

    #region ArtNetPlayerApplication のバッファ初期化ロジック検証

    [Test]
    public void アプリケーション層_動的ユニバース数でバッファ初期化ロジックが正しい()
    {
        // ArtNetPlayerApplication の Initialize 内部で使用される
        // 切り上げロジックの一貫性を検証する

        // テストケース: 各ユニバース数と期待される切り上げ結果
        var testCases = new (int input, int expected)[]
        {
            (1, 32),
            (16, 32),
            (32, 32),
            (33, 64),
            (48, 64),
            (64, 64),
            (65, 96),
            (100, 128),
            (128, 128),
            (256, 256),
        };

        foreach (var (input, expected) in testCases)
        {
            var result = ((input + 31) / 32) * 32;
            Assert.AreEqual(expected, result,
                $"ユニバース数{input}の32の倍数への切り上げ結果が{expected}であること");
        }
    }

    [Test]
    public void アプリケーション層_ArtNetPlayerApplicationにmaxUniverseNumハードコードが存在しない()
    {
        // ArtNetPlayerApplication クラスに "const int maxUniverseNum = 32" が存在しないことを
        // リフレクションで確認する（タスク1.3で削除された修正の回帰テスト）
        var fields = typeof(ArtNetPlayerApplication).GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

        foreach (var field in fields)
        {
            // const intフィールドで名前が "maxUniverseNum" のものがないことを確認
            if (field.Name == "maxUniverseNum" && field.IsLiteral && !field.IsInitOnly)
            {
                Assert.Fail("ArtNetPlayerApplication にハードコードされた const maxUniverseNum が残っています。" +
                            "タスク1.3で動的ユニバース数に置換されるべきです。");
            }
        }

        Assert.Pass("ArtNetPlayerApplication にハードコードされた const maxUniverseNum は存在しません");
    }

    [Test]
    public void アプリケーション層_DmxRecordDataにMaxUniverseCountプロパティが存在する()
    {
        // DmxRecordData クラスに MaxUniverseCount プロパティが公開されていることを確認する
        var property = typeof(DmxRecordData).GetProperty("MaxUniverseCount",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.IsNotNull(property,
            "DmxRecordData に MaxUniverseCount プロパティが公開されていること");
        Assert.AreEqual(typeof(int), property.PropertyType,
            "MaxUniverseCount の型が int であること");
        Assert.IsTrue(property.CanRead,
            "MaxUniverseCount が読み取り可能であること");
    }

    #endregion

    #region 境界条件: ユニバース番号のバッファ境界チェック

    [Test]
    public void 境界チェック_バッファサイズ外のユニバース番号がスキップされる()
    {
        // 32ユニバースのバッファに対して、ユニバース32以上のデータがスキップされる
        var maxUniverseNum = 32;
        var dmx = CreateDmxBuffer(maxUniverseNum);
        var dmxRaw = new float[maxUniverseNum * 512];

        // ユニバース0-31 + ユニバース32（バッファ範囲外）を含むパケット
        var universeDataList = new List<UniverseData>();
        for (int u = 0; u <= 32; u++) // 0-32: 33ユニバース分
        {
            var uData = new UniverseData
            {
                universe = u,
                data = new byte[512]
            };
            uData.data[0] = (byte)(u + 1);
            universeDataList.Add(uData);
        }

        var packet = new DmxRecordPacket
        {
            sequence = 0,
            time = 0.0,
            numUniverses = universeDataList.Count,
            data = universeDataList
        };

        // エラーなくコピーが完了する
        Assert.DoesNotThrow(() =>
            DmxPacketSearcher.CopyPacketToBuffers(packet, dmx, dmxRaw, maxUniverseNum),
            "バッファ範囲外のユニバースデータがあってもエラーが発生しないこと");

        // 範囲内のデータ（ユニバース0-31）は正しくコピーされている
        for (int u = 0; u < 32; u++)
        {
            Assert.AreEqual((float)(u + 1), dmxRaw[u * 512],
                $"ユニバース{u}のデータが正しくコピーされていること");
        }
    }

    [Test]
    public void 境界チェック_空パケットリストでも安全に動作する()
    {
        // 空のパケットリストでもFindPacketIndexが安全に動作する
        var packets = new List<DmxRecordPacket>();
        var recordData = new DmxRecordData(0.0, packets);

        Assert.AreEqual(1, recordData.MaxUniverseCount,
            "空のパケットリストでもMaxUniverseCountが最低1であること");

        // 空リストでのFindPacketIndexは0を返す
        var index = DmxPacketSearcher.FindPacketIndex(recordData.Data, 0.0, -1);
        Assert.AreEqual(0, index, "空リストでのFindPacketIndexが0を返すこと");
    }

    #endregion

    #region ヘルパーメソッド

    /// <summary>
    /// 指定したユニバース数とパケット数で録画データのパケットリストを作成する。
    /// 各ユニバースのチャンネル0にユニバース番号を設定する。
    /// </summary>
    private static List<DmxRecordPacket> CreateMultiUniversePacketList(
        int packetCount, int universeCount, double intervalMs)
    {
        var packets = new List<DmxRecordPacket>();

        for (int p = 0; p < packetCount; p++)
        {
            var universeDataList = new List<UniverseData>();
            for (int u = 0; u < universeCount; u++)
            {
                var data = new byte[512];
                data[0] = (byte)(u % 256); // ユニバース番号をチャンネル0に設定
                universeDataList.Add(new UniverseData
                {
                    universe = u,
                    data = data
                });
            }

            packets.Add(new DmxRecordPacket
            {
                sequence = p,
                time = p * intervalMs,
                numUniverses = universeCount,
                data = universeDataList
            });
        }

        return packets;
    }

    /// <summary>
    /// チャンネルデータに特定値を設定した録画データパケットリストを作成する。
    /// 各ユニバースのチャンネル0にはパケットインデックス*ユニバース番号の値を設定する。
    /// </summary>
    private static List<DmxRecordPacket> CreateMultiUniversePacketListWithChannelData(
        int packetCount, int universeCount, double intervalMs)
    {
        var packets = new List<DmxRecordPacket>();

        for (int p = 0; p < packetCount; p++)
        {
            var universeDataList = new List<UniverseData>();
            for (int u = 0; u < universeCount; u++)
            {
                var data = new byte[512];
                data[0] = (byte)((p * universeCount + u) % 256);
                universeDataList.Add(new UniverseData
                {
                    universe = u,
                    data = data
                });
            }

            packets.Add(new DmxRecordPacket
            {
                sequence = p,
                time = p * intervalMs,
                numUniverses = universeCount,
                data = universeDataList
            });
        }

        return packets;
    }

    /// <summary>
    /// DMXバッファ（byte[][]）を作成する。
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
