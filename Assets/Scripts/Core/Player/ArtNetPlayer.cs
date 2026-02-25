using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using Cysharp.Threading.Tasks;
using ProjectBlue;
using UnityEngine;

public class ArtNetPlayer : MonoBehaviour
{

    [SerializeField] private ArtNetResendUI artNetResendUI;

    private DmxRecordData dmxRecordData;

    private byte[][] dmx;
    private float[] dmxRaw;
    private int maxUniverseNum;

    /// <summary>
    /// 前回の探索結果インデックス。連続再生時の前方探索キャッシュに使用する。
    /// </summary>
    private int lastSearchIndex = -1;

    UdpClient udpClient = new UdpClient();

    public async UniTask<DmxRecordData> Load(string path)
    {
        dmxRecordData = await ReadFile(path);
        lastSearchIndex = -1;
        return dmxRecordData;
    }

    private static async UniTask<DmxRecordData> ReadFile(string path)
    {

        var result = await UniTask.Run(() => DmxRecordData.ReadFromFilePath(path));

        return result;
    }

    public double GetDuration()
    {
        return dmxRecordData.Data.Last().time;
    }

    public void Initialize(int maxUniverseNum)
    {
        this.maxUniverseNum = maxUniverseNum;

        dmx = new byte[maxUniverseNum][];
        for(var i = 0; i < maxUniverseNum; i++)
        {
            dmx[i] = new byte[512];
        }

        dmxRaw = new float[maxUniverseNum * 512];

        lastSearchIndex = -1;
    }

    public float[] ReadAndSend(double header)
    {
        var data = dmxRecordData.Data;
        if (data.Count == 0) return dmxRaw;

        // 二分探索（キャッシュ付き）でパケットインデックスを検出
        var index = DmxPacketSearcher.FindPacketIndex(data, header, lastSearchIndex);
        lastSearchIndex = index;

        var packet = data[index];

        // ユニバースデータをバッファにコピー（境界チェック付き）
        foreach (var universeData in packet.data)
        {
            // ユニバース番号の境界チェック: 範囲外はスキップ
            if (universeData.universe < 0 || universeData.universe >= maxUniverseNum)
            {
                continue;
            }

            Buffer.BlockCopy(universeData.data, 0, dmx[universeData.universe], 0, universeData.data.Length);

            if (artNetResendUI.IsEnabled)
            {
                var artNetPacket = new ArtNetDmxPacket
                {
                    Universe = (short) universeData.universe, DmxData = dmx[universeData.universe]
                };

                var artNetPacketBytes = artNetPacket.ToArray();

                udpClient.Send(artNetPacketBytes, artNetPacketBytes.Length, artNetResendUI.IPAddress.ToString(), artNetResendUI.Port);
            }
        }

        // 全ユニバースのチャンネルデータをフラットなfloat配列にコピー
        for (var universe = 0; universe < dmx.Length; universe++)
        {
            for (var channel = 0; channel < dmx[universe].Length; channel++)
            {
                dmxRaw[universe * dmx[universe].Length + channel] = dmx[universe][channel];
            }
        }

        return dmxRaw;
    }
}
