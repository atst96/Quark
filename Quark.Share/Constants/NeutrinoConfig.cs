namespace Quark.Constants;

/// <summary>
/// NEUTRINO共通設定
/// </summary>
public static class NeutrinoConfig
{
    /// <summary>1フレームあたりの間隔(ミリ秒)</summary>
    public const int FramePeriod = 5; /* 1000ms / 200frames */

    /// <summary>スペクトル包絡の下限値</summary>
    public const double MgcLower = -60.0d;

    /// <summary>スペクトル包絡の下限値</summary>
    public const float MgcLowerF = -60.0f;

    /// <summary>スペクトル包絡の1フレームあたりの次元数</summary>
    public const int MgcDimension = 60;

    /// <summary>非同期成分の1フレームあたりのデータ数</summary>
    public const int BapDimension = 5;

    /// <summary>メルスペクトログラムの1フレームあたりのデータ数</summary>
    public const int MspecDimension = 100;
}
