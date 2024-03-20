using NAudio.CoreAudioApi;
using NAudio.Wave;
using Quark.DependencyInjection;

namespace Quark.Services;

/// <summary>
/// オーディオ関連の管理クラス<br />
/// TODO: デバイス変更時の通知処理もここで行う
/// </summary>
[Singleton]
internal class AudioSessionManager()
{
    /// <summary>オーディオデバイスを取得する</summary>
    /// <returns></returns>
    public IWavePlayer GetDevice()
    {
        // TODO: 設定情報からデバイスを選択できるようにする
        return GetDefaultDevice();
    }

    /// <summary>オーディオデバイスを自動的に選択する</summary>
    /// <returns></returns>
    private static IWavePlayer GetDefaultDevice()
        => new WasapiOut(AudioClientShareMode.Shared, 48 * 2);
}
