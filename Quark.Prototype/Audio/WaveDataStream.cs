using System;
using NAudio.Wave;

namespace Quark.Audio;

/// <summary>
/// <see cref="WaveData"/>に蓄積した音声データをNAudioで再生するための処理
/// </summary>
internal class WaveDataStream : WaveStream
{
    private readonly WaveData _waveData;

    /// <summary>インスタンスを生成する</summary>
    /// <param name="waveData">音声データ</param>
    public WaveDataStream(WaveData waveData) : base()
    {
        this._waveData = waveData;
    }

    /// <summary>音声フォーマット</summary>
    public override WaveFormat WaveFormat { get; } = new WaveFormat(48000, 16, 1);

    /// <summary>音声データの現在位置</summary>
    public override long Position { get; set; }

    /// <summary>音声データの総データ量</summary>
    public override long Length { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer">バッファ</param>
    /// <param name="offset">書込みオフセット</param>
    /// <param name="count">データ数</param>
    /// <returns></returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        // 指定の位置、データ量の音声データを要求し、実際に取得できたデータのバイト数を得る
        int actual = this._waveData.Read((int)this.Position, buffer.AsSpan(offset, count));

        // 書き込まなかった領域をゼロ埋め
        // TODO: NAudio側でゼロフィル済みだったり、新規配列を渡している場合は不要かもしれない
        if (actual < count)
            buffer.AsSpan(offset + actual).Clear();

        // 現在位置をカウントアップする
        // MEMO: 再生を停止させないために要求データ量返却したことにする
        this.Position += count;
        return count;
    }
}
