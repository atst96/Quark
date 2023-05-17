using System;
using NAudio.Wave;

namespace Quark.Audio;
internal class WaveDataStream : WaveStream
{
    private WaveData _waveData;

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
        int actual = this._waveData.Read((int)this.Position, buffer.AsSpan(offset, count));

        if (actual < count)
            buffer.AsSpan(offset + actual).Clear();

        this.Position += count;

        return count;
    }
}
