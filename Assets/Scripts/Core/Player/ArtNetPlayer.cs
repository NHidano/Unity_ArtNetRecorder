using System;
using System.IO;
using System.Linq;
using System.Net;
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

    private byte[] sendBuffer;
    private IPEndPoint cachedEndPoint;
    private IPAddress cachedIPAddress;
    private int cachedPort;

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

        sendBuffer = new byte[ArtNetDmxPacket.PacketSize];

        lastSearchIndex = -1;
    }

    private void OnDestroy()
    {
        udpClient?.Dispose();
        udpClient = null;
    }

    public float[] ReadAndSend(double header)
    {
        var data = dmxRecordData.Data;
        if (data.Count == 0) return dmxRaw;

        // 二分探索（キャッシュ付き）でパケットインデックスを検出
        var index = DmxPacketSearcher.FindPacketIndex(data, header, lastSearchIndex);
        lastSearchIndex = index;

        var packet = data[index];

        // Resend有効時、IP/Port変更があればIPEndPointを再生成
        var resendEnabled = artNetResendUI.IsEnabled;
        if (resendEnabled)
        {
            var currentIP = artNetResendUI.IPAddress;
            var currentPort = artNetResendUI.Port;
            if (cachedEndPoint == null || !Equals(cachedIPAddress, currentIP) || cachedPort != currentPort)
            {
                cachedIPAddress = currentIP;
                cachedPort = currentPort;
                cachedEndPoint = new IPEndPoint(cachedIPAddress, cachedPort);
            }
        }

        // ユニバースデータをバッファにコピー（境界チェック付き）
        foreach (var universeData in packet.data)
        {
            // ユニバース番号の境界チェック: 範囲外はスキップ
            if (universeData.universe < 0 || universeData.universe >= maxUniverseNum)
            {
                continue;
            }

            Buffer.BlockCopy(universeData.data, 0, dmx[universeData.universe], 0, universeData.data.Length);

            if (resendEnabled)
            {
                var len = ArtNetDmxPacket.WriteToBuffer(sendBuffer, (short)universeData.universe, dmx[universeData.universe]);
                udpClient.Send(sendBuffer, len, cachedEndPoint);
            }
        }

        // 全ユニバースのチャンネルデータをフラットなfloat配列にコピー
        var universeCount = dmx.Length;
        for (var universe = 0; universe < universeCount; universe++)
        {
            var universeBytes = dmx[universe];
            var channelCount = universeBytes.Length;
            var baseOffset = universe * channelCount;
            for (var channel = 0; channel < channelCount; channel++)
            {
                dmxRaw[baseOffset + channel] = universeBytes[channel];
            }
        }

        return dmxRaw;
    }
}
