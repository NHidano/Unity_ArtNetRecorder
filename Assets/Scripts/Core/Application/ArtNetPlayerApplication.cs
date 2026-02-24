using System;
using System.IO;
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

    private bool initialized = false;

    private double header = 0;
    private double endTime;

    private PlayState playState = PlayState.Pausing;

    // ループ再生フラグ: トグルUIからのイベントで切り替える (タスク2.2)
    private bool isLoopEnabled = false;
    
    
    public override void OnClose()
    {
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

    }


    
    private async void Initialize(string path)
    {
        
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
    
}
