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

        if (header > endTime)
        {
            Pause();
        }
        
        header += Time.deltaTime * 1000;    // millisec

        visualizer.Exec(artNetPlayer.ReadAndSend(header));

        playerUI.SetHeader(header);

    }
    
}
