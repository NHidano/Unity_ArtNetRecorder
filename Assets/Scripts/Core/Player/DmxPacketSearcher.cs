using System;
using System.Collections.Generic;

/// <summary>
/// DMXパケットの二分探索ユーティリティ。
/// タスク 1.2: DMXデータ探索を二分探索に最適化する
///
/// パケットリスト（時間順ソート済み）から指定ヘッダーに対応するパケットインデックスを
/// 二分探索で高速に検出する。連続再生時のインデックスキャッシュ最適化を併用する。
/// </summary>
public static class DmxPacketSearcher
{
    /// <summary>
    /// 指定ヘッダー時間に対応するパケットインデックスを検出する。
    /// header以上のtimeを持つ最初のパケットのインデックスを返す。
    /// すべてのパケットがheader未満の場合は最後のインデックスを返す。
    ///
    /// lastIndex が有効で、かつ前方探索が可能な場合はキャッシュから前方探索する。
    /// そうでない場合は二分探索にフォールバックする。
    /// </summary>
    /// <param name="data">時間順ソート済みのパケットリスト</param>
    /// <param name="header">探索対象のヘッダー時間（ミリ秒）</param>
    /// <param name="lastIndex">前回の探索結果インデックス（-1の場合はキャッシュなし）</param>
    /// <returns>対応するパケットのインデックス</returns>
    public static int FindPacketIndex(IReadOnlyList<DmxRecordPacket> data, double header, int lastIndex)
    {
        if (data.Count == 0) return 0;
        if (data.Count == 1) return 0;

        // キャッシュインデックスが有効で、前方探索が可能な場合
        if (lastIndex >= 0 && lastIndex < data.Count)
        {
            // lastIndexのパケット時間がheader以下の場合、前方探索を試みる
            if (data[lastIndex].time <= header)
            {
                // lastIndexから前方に探索
                for (int i = lastIndex; i < data.Count; i++)
                {
                    if (data[i].time >= header)
                    {
                        return i;
                    }
                }
                // すべてのパケットがheader未満の場合は最後のインデックス
                return data.Count - 1;
            }
            // lastIndexのパケット時間がheaderより大きい場合（後方シーク）、二分探索にフォールバック
        }

        // 二分探索: header以上のtimeを持つ最初のパケットを検出する (lower bound)
        return BinarySearchLowerBound(data, header);
    }

    /// <summary>
    /// 二分探索で header 以上の time を持つ最初のパケットのインデックスを返す。
    /// すべてのパケットが header 未満の場合は最後のインデックスを返す。
    /// </summary>
    private static int BinarySearchLowerBound(IReadOnlyList<DmxRecordPacket> data, double header)
    {
        int low = 0;
        int high = data.Count;

        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (data[mid].time < header)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        // low がデータ範囲外の場合（すべてのパケットがheader未満）、最後のインデックスを返す
        if (low >= data.Count)
        {
            return data.Count - 1;
        }

        return low;
    }

    /// <summary>
    /// パケットのユニバースデータをバッファにコピーする。
    /// ユニバース番号がバッファサイズを超過する場合、または負の場合はスキップする。
    /// </summary>
    /// <param name="packet">コピー元のパケット</param>
    /// <param name="dmx">ユニバースごとのバイトバッファ</param>
    /// <param name="dmxRaw">フラットなfloatバッファ</param>
    /// <param name="maxUniverseNum">バッファの最大ユニバース数</param>
    public static void CopyPacketToBuffers(DmxRecordPacket packet, byte[][] dmx, float[] dmxRaw, int maxUniverseNum)
    {
        foreach (var universeData in packet.data)
        {
            // ユニバース番号の境界チェック: 範囲外はスキップ
            if (universeData.universe < 0 || universeData.universe >= maxUniverseNum)
            {
                continue;
            }

            Buffer.BlockCopy(universeData.data, 0, dmx[universeData.universe], 0, universeData.data.Length);
        }

        // 全ユニバースのチャンネルデータをフラットなfloat配列にコピー
        for (var universe = 0; universe < dmx.Length; universe++)
        {
            for (var channel = 0; channel < dmx[universe].Length; channel++)
            {
                dmxRaw[universe * dmx[universe].Length + channel] = dmx[universe][channel];
            }
        }
    }
}
