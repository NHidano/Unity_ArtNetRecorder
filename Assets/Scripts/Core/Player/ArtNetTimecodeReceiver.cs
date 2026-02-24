using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ProjectBlue.ArtNetRecorder
{
    /// <summary>
    /// ArtNet Timecodeパケットをバックグラウンドスレッドで受信し、
    /// タイムコード値をメインスレッドに通知するコンポーネント。
    /// タスク 3.2: タイムコード受信コンポーネントを新規作成する
    ///
    /// 既存の ArtNetRecorder と同じバックグラウンドスレッド + ConcurrentQueue パターンを踏襲する。
    /// ポート6454で UDP パケットを受信し、OpTimeCode (0x9700) を検出してパースする。
    /// </summary>
    public class ArtNetTimecodeReceiver : MonoBehaviour
    {
        /// <summary>タイムコード受信タイムアウト閾値 (秒)</summary>
        [SerializeField] private float timeoutSeconds = 2.0f;

        /// <summary>メインスレッドが Update() で TryDequeue して取得するタイムコードキュー</summary>
        public ConcurrentQueue<TimecodeData> TimecodeQueue { get; } = new ConcurrentQueue<TimecodeData>();

        /// <summary>タイムアウト状態かどうか。最終受信から timeoutSeconds を超えた場合に true。</summary>
        public bool IsTimedOut
        {
            get
            {
                if (!isReceiving) return false;
                if (lastReceivedTime < 0) return true; // まだ一度も受信していない
                return (Time.realtimeSinceStartup - lastReceivedTime) > timeoutSeconds;
            }
        }

        /// <summary>受信中かどうか</summary>
        public bool IsReceiving => isReceiving;

        private volatile bool isReceiving;
        private float lastReceivedTime = -1f;
        private UdpClient udpClient;
        private CancellationTokenSource receiveCts;
        private SynchronizationContext synchronizationContext;

        private void Awake()
        {
            synchronizationContext = SynchronizationContext.Current;
        }

        private void OnDestroy()
        {
            StopReceiving();
        }

        /// <summary>
        /// タイムコード受信を開始する。
        /// バックグラウンドスレッドでポート6454のUDPパケット受信ループを起動する。
        /// ポートが他のコンポーネント（レコーダー）に占有されている場合は SocketException を
        /// キャッチしてエラーダイアログを表示する。
        /// </summary>
        /// <param name="cancellationToken">GameObjectのライフサイクルに紐付けるキャンセルトークン</param>
        public void StartReceiving(CancellationToken cancellationToken)
        {
            if (isReceiving) return;

            // キューを空にする
            while (TimecodeQueue.TryDequeue(out _)) { }

            lastReceivedTime = -1f;
            isReceiving = true;

            receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = receiveCts.Token;

            var task = Task.Run(() =>
            {
                try
                {
                    var ip = new IPEndPoint(IPAddress.Any, Const.ArtNetServerPort);
                    udpClient = new UdpClient(ip);

                    Debug.Log("ArtNet Timecode Receiver started on port " + Const.ArtNetServerPort);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            // UDPパケット受信（ブロッキング呼び出しをキャンセル対応で使用）
                            var receiveTask = udpClient.ReceiveAsync();

                            // キャンセルを待つために短いタイムアウトでポーリング
                            while (!receiveTask.IsCompleted)
                            {
                                if (token.IsCancellationRequested)
                                {
                                    return;
                                }
                                Thread.Sleep(1);
                            }

                            if (receiveTask.IsFaulted)
                            {
                                continue;
                            }

                            var result = receiveTask.Result;
                            var buffer = result.Buffer;

                            // パケットパース: OpTimeCode を検出してタイムコードデータを抽出する
                            if (TimecodePacketParser.TryParseTimecodePacket(buffer, out var timecodeData))
                            {
                                TimecodeQueue.Enqueue(timecodeData);

                                // メインスレッドで最終受信時刻を更新するためにキューに入れるだけで、
                                // lastReceivedTime は Update() で更新する
                            }
                            // パケット長不足・OpCode不一致の場合は TryParseTimecodePacket が false を返すため
                            // 自動的にスキップされる
                        }
                        catch (ObjectDisposedException)
                        {
                            // ソケットが StopReceiving() で閉じられた
                            break;
                        }
                        catch (SocketException)
                        {
                            // 受信中のソケットエラー
                            break;
                        }
                    }
                }
                catch (SocketException)
                {
                    // ポート占有エラー: メインスレッドに通知
                    synchronizationContext?.Post(_ =>
                    {
                        isReceiving = false;
                        Logger.Error("ポート6454が他のアプリケーションによって専有されています");
                        DialogManager.OpenError("ポート6454が他のアプリケーションによって\n専有されています").Forget();
                    }, null);
                    return;
                }
                catch (Exception e)
                {
                    if (!(e is OperationCanceledException))
                    {
                        Debug.LogException(e);
                    }
                }
                finally
                {
                    CleanupSocket();
                    isReceiving = false;
                    Debug.Log("ArtNet Timecode Receiver stopped");
                }
            }, token);
        }

        /// <summary>
        /// タイムコード受信を停止し、UDPソケットを解放する。
        /// </summary>
        public void StopReceiving()
        {
            if (!isReceiving && receiveCts == null) return;

            isReceiving = false;
            receiveCts?.Cancel();
            CleanupSocket();
            receiveCts?.Dispose();
            receiveCts = null;
            lastReceivedTime = -1f;
        }

        /// <summary>
        /// メインスレッドの Update() から呼び出して最終受信時刻を更新する。
        /// TimecodeQueue からデータをデキューした後に呼び出すこと。
        /// </summary>
        public void NotifyTimecodeReceived()
        {
            lastReceivedTime = Time.realtimeSinceStartup;
        }

        private void CleanupSocket()
        {
            try
            {
                udpClient?.Close();
                udpClient?.Dispose();
            }
            catch (Exception)
            {
                // ソケット解放時のエラーは無視
            }
            udpClient = null;
        }
    }
}
