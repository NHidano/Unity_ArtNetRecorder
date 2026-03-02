using System.Net;

/// <summary>
/// ArtNet再送信先設定のPlayerPrefsキー定数。
/// 設計書で定義されたキー名と一致させる。
/// </summary>
public static class ResendSettingsKeys
{
    /// <summary>再送信先IPアドレスのキー</summary>
    public const string DstIP = "ArtNetResend_DstIP";

    /// <summary>再送信先ポート番号のキー</summary>
    public const string DstPort = "ArtNetResend_DstPort";

    /// <summary>再送信トグル状態のキー (0=OFF, 1=ON)</summary>
    public const string Enabled = "ArtNetResend_Enabled";

    /// <summary>ユニバースオフセットのキー</summary>
    public const string UniverseOffset = "ArtNetResend_UniverseOffset";
}

/// <summary>
/// ArtNet再送信先設定のデフォルト値。
/// 保存データが存在しない場合に使用される。
/// </summary>
public static class ResendSettingsDefaults
{
    /// <summary>デフォルトの再送信先IPアドレス</summary>
    public const string DstIP = "2.0.0.1";

    /// <summary>デフォルトの再送信先ポート番号</summary>
    public const int DstPort = 6454;

    /// <summary>デフォルトのトグル状態 (0=OFF)</summary>
    public const int Enabled = 0;

    /// <summary>デフォルトのユニバースオフセット</summary>
    public const int UniverseOffset = 0;
}

/// <summary>
/// ArtNet再送信先設定の検証ロジック。
/// MonoBehaviourに依存しない純粋な判定ロジックを提供する。
/// IPアドレス・ポート番号の検証と、不正値のデフォルトフォールバックを行う。
/// </summary>
public static class ResendSettingsValidator
{
    /// <summary>
    /// IPアドレス文字列が有効かどうかを検証する。
    /// </summary>
    /// <param name="ip">検証するIPアドレス文字列</param>
    /// <returns>有効な場合は true</returns>
    public static bool IsValidIP(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        return IPAddress.TryParse(ip, out _);
    }

    /// <summary>
    /// ポート番号が有効な範囲 (1-65535) 内かどうかを検証する。
    /// </summary>
    /// <param name="port">検証するポート番号</param>
    /// <returns>有効な場合は true</returns>
    public static bool IsValidPort(int port)
    {
        return port >= 1 && port <= 65535;
    }

    /// <summary>
    /// ポート番号文字列が有効かどうかを検証する。
    /// 数値にパース可能で、かつ有効な範囲内であることを確認する。
    /// </summary>
    /// <param name="portString">検証するポート番号文字列</param>
    /// <returns>有効な場合は true</returns>
    public static bool IsValidPortString(string portString)
    {
        if (string.IsNullOrEmpty(portString)) return false;
        if (!int.TryParse(portString, out var port)) return false;
        return IsValidPort(port);
    }

    /// <summary>
    /// IPアドレスを検証し、不正な場合はデフォルト値を返す。
    /// PlayerPrefsからの読み込み時に使用する。
    /// </summary>
    /// <param name="ip">検証するIPアドレス文字列</param>
    /// <returns>有効な場合はそのまま返す。不正な場合はデフォルト値</returns>
    public static string GetValidatedIP(string ip)
    {
        return IsValidIP(ip) ? ip : ResendSettingsDefaults.DstIP;
    }

    /// <summary>
    /// ポート番号を検証し、不正な場合はデフォルト値を返す。
    /// PlayerPrefsからの読み込み時に使用する。
    /// </summary>
    /// <param name="port">検証するポート番号</param>
    /// <returns>有効な場合はそのまま返す。不正な場合はデフォルト値</returns>
    public static int GetValidatedPort(int port)
    {
        return IsValidPort(port) ? port : ResendSettingsDefaults.DstPort;
    }

    /// <summary>
    /// ユニバースオフセットが有効な範囲 (-512〜512) 内かどうかを検証する。
    /// </summary>
    /// <param name="offset">検証するオフセット値</param>
    /// <returns>有効な場合は true</returns>
    public static bool IsValidUniverseOffset(int offset)
    {
        return offset >= -512 && offset <= 512;
    }

    /// <summary>
    /// ユニバースオフセットを検証し、不正な場合はデフォルト値を返す。
    /// </summary>
    /// <param name="offset">検証するオフセット値</param>
    /// <returns>有効な場合はそのまま返す。不正な場合はデフォルト値</returns>
    public static int GetValidatedUniverseOffset(int offset)
    {
        return IsValidUniverseOffset(offset) ? offset : ResendSettingsDefaults.UniverseOffset;
    }

    /// <summary>
    /// トグル状態のbool値をint値 (0/1) に変換する。
    /// PlayerPrefsへの保存時に使用する。
    /// </summary>
    /// <param name="isEnabled">トグル状態</param>
    /// <returns>ONの場合は1、OFFの場合は0</returns>
    public static int ToggleStateToInt(bool isEnabled)
    {
        return isEnabled ? 1 : 0;
    }

    /// <summary>
    /// int値 (0/1) をトグル状態のbool値に変換する。
    /// PlayerPrefsからの読み込み時に使用する。
    /// 1の場合のみtrueを返し、それ以外はfalseにフォールバックする。
    /// </summary>
    /// <param name="value">PlayerPrefsから読み込んだ値</param>
    /// <returns>1の場合はtrue、それ以外はfalse</returns>
    public static bool IntToToggleState(int value)
    {
        return value == 1;
    }
}
