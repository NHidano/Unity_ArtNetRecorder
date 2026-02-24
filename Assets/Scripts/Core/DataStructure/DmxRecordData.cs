using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class DmxRecordData
{
    private double duration;
    private List<DmxRecordPacket> data;
    private int maxUniverseCount;

    public double Duration => duration;
    public IReadOnlyList<DmxRecordPacket> Data => data;

    /// <summary>
    /// 録画データに含まれるユニバースの最大数 (最大ユニバース番号+1)。
    /// 最低値は1が保証される。
    /// </summary>
    public int MaxUniverseCount => maxUniverseCount;

    /// <summary>
    /// パケットリストからMaxUniverseCountを自動計算するコンストラクタ。
    /// 外部からのインスタンス生成時（テスト、Timeline等）に使用する。
    /// </summary>
    public DmxRecordData(double duration, List<DmxRecordPacket> data)
        : this(duration, data, CalculateMaxUniverseCount(data))
    {
    }

    /// <summary>
    /// MaxUniverseCountを明示的に指定するコンストラクタ。
    /// ReadFromFilePathの走査ループ内で既に計算済みの場合に二重計算を回避する。
    /// </summary>
    private DmxRecordData(double duration, List<DmxRecordPacket> data, int maxUniverseCount)
    {
        this.duration = duration;
        this.data = data;
        this.maxUniverseCount = maxUniverseCount;
    }

    public static DmxRecordData ReadFromFilePath(string path)
    {
        try
        {
            var list = new List<DmxRecordPacket>();

            double finalPaketTime = 0;
            int maxUniverseNumber = -1;

            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var reader = new BinaryReader(stream);

                // loop to the end
                var baseStream = reader.BaseStream;
                while ( baseStream.Position != baseStream.Length )
                {
                    var sequence = (int)reader.ReadUInt32();
                    var time = reader.ReadDouble();

                    finalPaketTime = time;

                    var numUniverses = (int)reader.ReadUInt32();

                    var data = new List<UniverseData>();

                    for (var i = 0; i < numUniverses; i++)
                    {
                        var universe = (int)reader.ReadUInt32();
                        if (universe > maxUniverseNumber)
                        {
                            maxUniverseNumber = universe;
                        }
                        data.Add(new UniverseData{universe=universe, data=reader.ReadBytes( 512).ToArray()});
                    }

                    list.Add(new DmxRecordPacket
                    {
                        sequence = sequence, time = time, numUniverses = numUniverses, data = data
                    });
                }
            }

            // 走査ループで計算済みの最大ユニバース番号+1を使用。最低値1を保証する
            var universeCount = Math.Max(maxUniverseNumber + 1, 1);
            return new DmxRecordData(finalPaketTime, list, universeCount);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed importing {path}. {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// パケットリストから最大ユニバース番号+1を計算する。
    /// ユニバースが存在しない場合は最低値1を返す。
    /// </summary>
    private static int CalculateMaxUniverseCount(List<DmxRecordPacket> packets)
    {
        int maxUniverseNumber = -1;

        foreach (var packet in packets)
        {
            foreach (var universeData in packet.data)
            {
                if (universeData.universe > maxUniverseNumber)
                {
                    maxUniverseNumber = universeData.universe;
                }
            }
        }

        // 最大ユニバース番号+1がユニバース数。最低値1を保証する
        return Math.Max(maxUniverseNumber + 1, 1);
    }
}


[Serializable]
public class DmxRecordPacket
{
    public int sequence;
    public double time; // millisec
    public int numUniverses;
    public List<UniverseData> data;
}

[Serializable]
public class UniverseData
{
    public int universe;
    public byte[] data;
}