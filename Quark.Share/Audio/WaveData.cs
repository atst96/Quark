using Quark.Utils;

namespace Quark.Audio;

/// <summary>
/// WAVEデータ管理
/// 
/// 以下のパラメータに固定：
/// サンプリング周波数: 48KHz
/// 量子化ビット数: 16bit
/// チャンネル数: 1(モノラル)
/// </summary>
public class WaveData
{
    private object @_lock = new();

    /// <summary>データ領域</summary>
    private byte[] _data;

    /// <summary>データ量(バイト数)</summary>
    public int Length { get; private set; }

    /// <summary>初期データ長の初期値</summary>
    private static readonly int DefaultLength = WavUtil.CalcDataPosition48k16bitMono(60 * 1000);

    /// <summary>
    /// <see cref="WaveData"/>を生成する
    /// </summary>
    public WaveData() : this(DefaultLength) { }

    /// <summary>
    /// <see cref="WaveData"/>を生成する
    /// </summary>
    /// <param name="initLength">初期サイズ</param>
    public WaveData(int initLength)
    {
        this._data = new byte[initLength];
        this.Length = 0;
    }

    /// <summary>WAVEデータを初期化する</summary>
    public void Clear()
    {
        lock (this._lock)
        {
            this._data.AsSpan().Clear();
            this.Length = 0;
        }
    }

    /// <summary>
    /// WAVEデータを書き込む
    /// </summary>
    /// <param name="offset">データ位置</param>
    /// <param name="data">書込みデータ</param>
    public void Write(int offset, Span<byte> data)
    {
        lock (this.@_lock)
        {
            int totalPosition = offset + data.Length;

            // データ領域が足りなければ拡張
            this.ExtendDataSpace(totalPosition);

            // データを拡張
            data.CopyTo(this._data.AsSpan(offset..));

            if (this.Length < totalPosition)
                this.Length = totalPosition;
        }
    }

    /// <summary指定箇所にWAVEデータを書き込む</summary>
    /// <param name="positionMs">データ位置(ミリ秒)</param>
    /// <param name="data">書込データ</param>
    public void WriteMs(int positionMs, Span<byte> data)
        => this.Write(WavUtil.CalcDataPosition48k16bitMono(positionMs), data);

    /// <summary>データ領域を拡張する</summary>
    /// <param name="newDataPosition">データ位置</param>
    private void ExtendDataSpace(int newDataPosition)
    {
        var oldData = this._data;

        if (oldData.Length >= newDataPosition)
        {
            // 既に書込むデータより大きな領域を確保できていれば何もしない
            return;
        }

        // 現在の要素数を倍した領域を作成する
        int newDataSize = oldData.Length < 0 ? DefaultLength : oldData.Length;
        do newDataSize *= 2;
        while (newDataSize < newDataPosition);

        byte[] newData = new byte[newDataSize];

        // データをコピー
        oldData.AsSpan().CopyTo(newData);

        this._data = newData;
    }

    /// <summary>WAVEデータを出力する</summary>
    /// <param name="filePath">ファイスパス</param>
    public void Export(string filePath)
    {
        lock (this.@_lock)
        {
            int dataSize = this.Length;
            var data = this._data;

            using (var fs = new FileStream(filePath, FileMode.CreateNew))
            {
                // ヘッダ情報を書き込む
                WavUtil.WritePcmWaveHeader48k16bitMono(fs, dataSize);

                // データを書き込む
                fs.Write(data.AsSpan(..dataSize));
            }
        }
    }

    /// <summary>
    /// WAVデータを読み込む
    /// </summary>
    /// <param name="position">データ位置</param>
    /// <param name="destination">出力先</param>
    public int Read(int position, Span<byte> destination)
    {
        lock (this.@_lock)
        {
            int endPosition = Math.Min(this.Length, position + destination.Length);
            int length = 0;

            if (position < endPosition)
            {
                length = endPosition - position;
                this._data.AsSpan(position..endPosition).CopyTo(destination);
            }

            return length;
        }
    }
}
