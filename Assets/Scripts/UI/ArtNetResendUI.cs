using System.Collections;
using System.Collections.Generic;
using System.Net;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

public class ArtNetResendUI : MonoBehaviour
{

    [SerializeField] private Toggle enableToggle;
    [SerializeField] private InputField ipInputField;
    [SerializeField] private InputField portInputField;

    public bool IsEnabled => isValidated && enableToggle.isOn;
    public int Port => port;
    public IPAddress IPAddress => ipAddress;

    private bool isValidated;

    private int port;
    private IPAddress ipAddress;

    private void Start()
    {
        // 起動時にPlayerPrefsから保存済み設定を読み込む
        LoadSettings();

        ipInputField.OnValueChangedAsObservable().Subscribe(t =>
        {
            if (IPAddress.TryParse(t, out var address))
            {
                isValidated = true;
                ipAddress = address;
                ipInputField.image.color = Color.cyan;

                // 検証成功時のみ保存する
                SaveIP(t);
            }
            else
            {
                isValidated = false;
                ipInputField.image.color = Color.red;
            }
        }).AddTo(this);

        portInputField.OnValueChangedAsObservable().Subscribe(t =>
        {
            if (int.TryParse(t, out var value))
            {
                isValidated = true;
                port = value;
                portInputField.image.color = Color.cyan;

                // 検証成功時のみ保存する
                SavePort(value);
            }
            else
            {
                isValidated = false;
                portInputField.image.color = Color.red;
            }
        }).AddTo(this);

        // トグル状態の変更を購読し、即時保存する
        enableToggle.OnValueChangedAsObservable().Subscribe(isOn =>
        {
            SaveToggleState(isOn);
        }).AddTo(this);
    }

    /// <summary>
    /// PlayerPrefsから保存済みの設定を読み込み、各フィールドに反映する。
    /// 不正な値はデフォルト値にフォールバックする。
    /// </summary>
    private void LoadSettings()
    {
        // IPアドレスの読み込みと検証
        var savedIP = PlayerPrefs.GetString(ResendSettingsKeys.DstIP, ResendSettingsDefaults.DstIP);
        var validatedIP = ResendSettingsValidator.GetValidatedIP(savedIP);
        ipInputField.text = validatedIP;

        // ポート番号の読み込みと検証
        var savedPort = PlayerPrefs.GetInt(ResendSettingsKeys.DstPort, ResendSettingsDefaults.DstPort);
        var validatedPort = ResendSettingsValidator.GetValidatedPort(savedPort);
        portInputField.text = validatedPort.ToString();

        // トグル状態の読み込み
        var savedEnabled = PlayerPrefs.GetInt(ResendSettingsKeys.Enabled, ResendSettingsDefaults.Enabled);
        enableToggle.isOn = ResendSettingsValidator.IntToToggleState(savedEnabled);
    }

    /// <summary>
    /// IPアドレスをPlayerPrefsに保存する。
    /// 検証成功時のみ呼び出される。
    /// </summary>
    /// <param name="ip">保存するIPアドレス文字列</param>
    private void SaveIP(string ip)
    {
        PlayerPrefs.SetString(ResendSettingsKeys.DstIP, ip);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// ポート番号をPlayerPrefsに保存する。
    /// 検証成功時のみ呼び出される。
    /// </summary>
    /// <param name="portValue">保存するポート番号</param>
    private void SavePort(int portValue)
    {
        PlayerPrefs.SetInt(ResendSettingsKeys.DstPort, portValue);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// トグル状態をPlayerPrefsに保存する。
    /// 値の変更時に即時保存される。
    /// </summary>
    /// <param name="isOn">トグルのON/OFF状態</param>
    private void SaveToggleState(bool isOn)
    {
        PlayerPrefs.SetInt(ResendSettingsKeys.Enabled, ResendSettingsValidator.ToggleStateToInt(isOn));
        PlayerPrefs.Save();
    }

}
