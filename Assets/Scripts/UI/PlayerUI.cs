using System;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUI : MonoBehaviour
{

    [SerializeField] private PlayToggleButton playButton;
    [SerializeField] private Slider slider;
    [SerializeField] private Text headerText;
    [SerializeField] private Text endText;

    // ループ再生トグル
    [SerializeField] private Toggle loopToggle;

    // タイムコード受信モードトグル (タスク3.4)
    [SerializeField] private Toggle timecodeToggle;

    // タイムコード表示テキスト (タスク3.4)
    [SerializeField] private Text timecodeDisplayText;

    private double endTimeMillisec;

    public IObservable<Unit> OnPlayButtonPressedAsObservable => playButton.OnClickAsObservable;

    // ループトグル変更イベントをリアクティブストリームとして公開する
    public IObservable<bool> OnLoopToggleChangedAsObservable => loopToggle.OnValueChangedAsObservable();

    // タイムコードトグル変更イベントをリアクティブストリームとして公開する (タスク3.4)
    public IObservable<bool> OnTimecodeToggleChangedAsObservable => timecodeToggle.OnValueChangedAsObservable();

    private void Awake()
    {
        SetAsPlayVisual();

        slider.OnValueChangedAsObservable().Subscribe(value =>
        {

            var headerMillisec = value * endTimeMillisec;
            
            var sec = headerMillisec * 0.001d;
            var min = (int) (sec / 60);
            sec = sec - (min * 60);

            headerText.text = $"{min:D2}:{(int)sec:D2};{(int)headerMillisec % 1000:D3}";
        }).AddTo(this);
    }
    
    public void SetAsPauseVisual()
    {
        playButton.SetAsPauseVisual();
    }

    public void SetAsPlayVisual()
    {
        playButton.SetAsPlayVisual();
    }

    public float GetSliderPosition()
    {
        return slider.value;
    }
    
    public void Initialize(double endTimeMillisec)
    {
        var sec = endTimeMillisec / 1000d;
        var min = (int) (sec / 60);
        sec = sec - (min * 60);

        endText.text = $"{min:D2}:{(int)sec:D2}";

        this.endTimeMillisec = endTimeMillisec;
        
        slider.value = 0;
    }
    
    public void SetHeader(double headerMillisec)
    {
        slider.value = (float)(headerMillisec / endTimeMillisec);
    }

    /// <summary>
    /// 手動再生コントロール（再生ボタン・スライダー）の有効/無効を切り替える。
    /// タイムコード受信モード有効時に手動操作を無効化し、
    /// モード解除時に復元するために使用する。
    /// (タスク3.4: Requirements 3.3, 3.6)
    /// </summary>
    /// <param name="enabled">true で有効化、false で無効化</param>
    public void SetManualControlEnabled(bool enabled)
    {
        playButton.SetInteractable(enabled);
        slider.interactable = enabled;
    }

    /// <summary>
    /// 受信中のタイムコード値をリアルタイム表示するテキストを更新する。
    /// (タスク3.4: Requirement 3.4)
    /// </summary>
    /// <param name="timecodeText">タイムコード表示文字列 (例: "01:05:30:12")</param>
    public void SetTimecodeDisplay(string timecodeText)
    {
        if (timecodeDisplayText != null)
        {
            timecodeDisplayText.text = timecodeText;
        }
    }

    /// <summary>
    /// タイムコード表示テキストの表示/非表示を切り替える。
    /// (タスク3.4: Requirement 3.4)
    /// </summary>
    /// <param name="visible">true で表示、false で非表示</param>
    public void SetTimecodeDisplayVisible(bool visible)
    {
        if (timecodeDisplayText != null)
        {
            timecodeDisplayText.gameObject.SetActive(visible);
        }
    }

}
