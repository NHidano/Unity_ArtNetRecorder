using System;
using System.IO;
using Cysharp.Threading.Tasks;
using ProjectBlue.ArtNetRecorder;
using UniRx;
using UnityEngine;

public enum PlayState
{
    Playing, Pausing
}

public class ArtNetPlayerApplication : ApplicationBase
{

    [SerializeField] private DataVisualizer visualizer;

    [SerializeField] private PlayerUI playerUI;

    [SerializeField] private FileDialogUI dmxFileDialogUI;
    [SerializeField] private FileDialogUI audioDialogUI;

    [SerializeField] private AudioPlayer audioPlayer;

    [SerializeField] private ArtNetPlayer artNetPlayer;

    [SerializeField] private LoadingUI loadingUI;

    // タイムコード受信コンポーネント (タスク3.5)
    [SerializeField] private ArtNetTimecodeReceiver timecodeReceiver;

    private bool initialized = false;

    private double header = 0;
    private double endTime;

    private PlayState playState = PlayState.Pausing;

    // ループ再生フラグ: トグルUIからのイベントで切り替える (タスク2.2)
    private bool isLoopEnabled = false;

    // タイムコード受信モードフラグ: UIトグルからのイベントで切り替える (タスク3.5)
    private bool isTimecodeMode = false;

    // タイムアウトによる一時停止状態を管理するフラグ (タスク3.5)
    private bool isPausedByTimeout = false;


    public override void OnClose()
    {
        // タイムコードモード中にタブを閉じた場合はモードを無効化する
        if (isTimecodeMode)
        {
            DisableTimecodeMode();
        }
        Pause();
    }

    public override void OnOpen()
    {
        // throw new NotImplementedException();
    }


    private void Start()
    {

        loadingUI.Hide();

        playerUI.OnPlayButtonPressedAsObservable.Subscribe(_ =>
        {
            // タイムコードモード有効時は手動再生コントロールを無視する (タスク3.5)
            if (isTimecodeMode) return;

            if (playState == PlayState.Playing)
            {
                Pause();
            }
            else
            {
                Resume();
            }

        }).AddTo(this);


        dmxFileDialogUI.OnFileNameChanged.Subscribe(path =>
        {
            if (File.Exists(path))
            {
                Initialize(path);
            }
        }).AddTo(this);

        audioDialogUI.OnFileNameChanged.Subscribe(async path =>
        {
            if (File.Exists(path))
            {
                audioPlayer.LoadClipFromPath(path).Forget();
            }
        }).AddTo(this);

        // ループ再生トグルの変更イベントを購読し、isLoopEnabled フラグを即座に反映する (タスク2.2)
        playerUI.OnLoopToggleChangedAsObservable.Subscribe(value =>
        {
            isLoopEnabled = value;
        }).AddTo(this);

        // タイムコード受信モードトグルの変更イベントを購読する (タスク3.5)
        playerUI.OnTimecodeToggleChangedAsObservable.Subscribe(value =>
        {
            if (value)
            {
                EnableTimecodeMode();
            }
            else
            {
                DisableTimecodeMode();
            }
        }).AddTo(this);

    }



    private async void Initialize(string path)
    {

        // タイムコードモード中に新しいデータをロードする場合はモードを無効化する
        if (isTimecodeMode)
        {
            DisableTimecodeMode();
        }

        initialized = false;

        // read file
        loadingUI.Display();

        var data = await artNetPlayer.Load(path);

        loadingUI.Hide();

        endTime = data.Duration;

        if (data != null)
        {
            // 録画データから実際のユニバース数を取得し、ComputeShaderディスパッチ用に32の倍数に切り上げ
            var maxUniverseNum = ((data.MaxUniverseCount + 31) / 32) * 32;

            // initialize visualizer
            visualizer.Initialize(maxUniverseNum);

            // initialize player
            playerUI.Initialize(endTime);
            playerUI.SetAsPlayVisual();
            playState = PlayState.Pausing;

            // initialize buffers
            artNetPlayer.Initialize(maxUniverseNum);

            initialized = true;
        }

        // ローディング画面から開ける
    }

    public void Resume()
    {

        if (!initialized) return;

        // ここでヘッダ読んでくる

        header = playerUI.GetSliderPosition() * endTime;
        endTime = artNetPlayer.GetDuration();

        playerUI.SetAsPauseVisual();

        playState = PlayState.Playing;

        audioPlayer.Resume(2566.667f + (float)header);
    }

    public void Pause()
    {
        playerUI.SetAsPlayVisual();
        playState = PlayState.Pausing;

        audioPlayer.Pause();
    }

    private void Update()
    {

        if (!initialized) return;

        // タイムコード受信モード有効時はタイムコード駆動で再生位置を更新する (タスク3.5)
        if (isTimecodeMode)
        {
            ProcessTimecodeFrame();
            return;
        }

        if (playState == PlayState.Pausing) return;

        // 終端到達の判定とループ再生処理 (タスク2.2)
        if (LoopPlaybackLogic.IsEndOfPlayback(header, endTime))
        {
            HandleEndOfPlayback();
            return;
        }

        header += Time.deltaTime * 1000;    // millisec

        visualizer.Exec(artNetPlayer.ReadAndSend(header));

        playerUI.SetHeader(header);

    }

    /// <summary>
    /// 再生位置が終端に到達した際の処理。
    /// ループ有効時は先頭にリセットして再生を継続し、
    /// ループ無効時は再生を停止してシークバーを終端位置に保持する。
    /// (タスク2.2: Requirements 2.2, 2.3)
    /// </summary>
    private void HandleEndOfPlayback()
    {
        var action = LoopPlaybackLogic.DetermineEndOfPlaybackAction(isLoopEnabled);

        switch (action)
        {
            case EndOfPlaybackAction.ResetToBeginning:
                // ループ有効: 先頭にリセットして再生を継続する
                header = 0;
                audioPlayer.Resume(2566.667f);
                break;

            case EndOfPlaybackAction.StopPlayback:
                // ループ無効: 再生を停止し、シークバーを終端位置に保持する
                header = endTime;
                playerUI.SetHeader(endTime);
                Pause();
                break;
        }
    }

    /// <summary>
    /// タイムコード受信モードを有効化する。
    /// 録画データがロード済みであることを検証してから有効化する。
    /// モード有効時は手動再生コントロールを無効化し、タイムコード受信を開始する。
    /// (タスク3.5: Requirements 3.2, 3.3, 3.5, 3.6)
    /// </summary>
    private void EnableTimecodeMode()
    {
        // 録画データがロード済みであることを検証する
        if (!TimecodeSyncLogic.CanEnableTimecodeMode(initialized))
        {
            Debug.LogWarning("タイムコードモード: 録画データがロードされていないため有効化できません");
            return;
        }

        isTimecodeMode = true;
        isPausedByTimeout = false;

        // 手動再生コントロールを無効化する (Requirements 3.3)
        playerUI.SetManualControlEnabled(false);

        // タイムコード表示を有効化する
        playerUI.SetTimecodeDisplayVisible(true);
        playerUI.SetTimecodeDisplay(TimecodeDisplayFormatter.DefaultDisplayText);

        // 手動再生中の場合は一時停止する
        if (playState == PlayState.Playing)
        {
            Pause();
        }

        // タイムコード受信を開始する
        timecodeReceiver.StartReceiving(this.GetCancellationTokenOnDestroy());

        Debug.Log("タイムコード受信モードを有効化しました");
    }

    /// <summary>
    /// タイムコード受信モードを無効化する。
    /// 手動再生コントロールを復元し、現在の再生位置を保持する。
    /// (タスク3.5: Requirements 3.6)
    /// </summary>
    private void DisableTimecodeMode()
    {
        isTimecodeMode = false;
        isPausedByTimeout = false;

        // タイムコード受信を停止する
        timecodeReceiver.StopReceiving();

        // 手動再生コントロールを復元する (Requirements 3.6)
        playerUI.SetManualControlEnabled(true);

        // タイムコード表示を非表示にする
        playerUI.SetTimecodeDisplayVisible(false);

        // 現在の再生位置を保持し、一時停止状態にする
        playerUI.SetAsPlayVisual();
        playState = PlayState.Pausing;

        Debug.Log("タイムコード受信モードを無効化しました");
    }

    /// <summary>
    /// 毎フレームのUpdate内でタイムコードキューからデータを取得し、
    /// 対応する再生位置にシークしてDMXデータを送信する。
    /// タイムアウト検出時は最後のタイムコード位置で再生を一時停止する。
    /// (タスク3.5: Requirements 3.2, 3.5)
    /// </summary>
    private void ProcessTimecodeFrame()
    {
        // タイムアウト判定: タイムアウト時は最後のタイムコード位置で再生を一時停止する
        if (TimecodeSyncLogic.ShouldPauseOnTimeout(timecodeReceiver.IsTimedOut, isPausedByTimeout))
        {
            isPausedByTimeout = true;
            playerUI.SetAsPlayVisual();
            Debug.Log("タイムコード受信タイムアウト: 最後の位置で一時停止します");
            return;
        }

        // タイムアウトからの回復: 再度タイムコードが受信された場合
        if (TimecodeSyncLogic.ShouldResumeFromTimeout(timecodeReceiver.IsTimedOut, isPausedByTimeout))
        {
            isPausedByTimeout = false;
            playerUI.SetAsPauseVisual();
            Debug.Log("タイムコード受信回復: 再生を再開します");
        }

        // タイムアウト一時停止中はキューの処理をスキップする
        if (isPausedByTimeout) return;

        // タイムコードキューからデータを取得する
        TimecodeData latestTimecode = default;
        bool hasTimecode = false;

        // キュー内の全データを取得し、最新のタイムコードを使用する
        while (timecodeReceiver.TimecodeQueue.TryDequeue(out var timecodeData))
        {
            latestTimecode = timecodeData;
            hasTimecode = true;
        }

        if (!hasTimecode) return;

        // メインスレッドで最終受信時刻を更新する
        timecodeReceiver.NotifyTimecodeReceived();

        // タイムコード値を再生範囲内にクランプする
        var targetMilliseconds = TimecodeSyncLogic.ClampTimecodeToPlaybackRange(
            latestTimecode.MillisecondsFromStart, endTime);

        // 再生位置を更新する
        header = targetMilliseconds;

        // DMXデータを送信し、ビジュアライザーを更新する
        visualizer.Exec(artNetPlayer.ReadAndSend(header));

        // UIのシークバーを更新する
        playerUI.SetHeader(header);

        // タイムコード表示を更新する (Requirements 3.4)
        playerUI.SetTimecodeDisplay(TimecodeDisplayFormatter.FormatTimecode(latestTimecode));
    }

}
