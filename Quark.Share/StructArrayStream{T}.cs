using System.Runtime.InteropServices;

namespace Quark;

public class StructArrayStream<T>(T[] data) : Stream
    where T : unmanaged
{
    private ReaderWriterLockSlim _lock = new();
    private T[] _data = data;
    private static readonly int _elementSize = Marshal.SizeOf<T>();
    private long _position = 0;

    /// <summary><inheritdoc/></summary>
    public override bool CanRead { get; } = true;

    /// <summary><inheritdoc/></summary>
    public override bool CanSeek { get; } = true;

    /// <summary><inheritdoc/></summary>
    public override bool CanWrite { get; } = false;

    /// <summary><inheritdoc/></summary>
    public override long Length { get; } = data.Length * _elementSize;

    /// <summary><inheritdoc/></summary>
    public override long Position
    {
        get
        {
            var lockObj = this._lock;

            try
            {
                lockObj.EnterReadLock();
                return this._position;
            }
            finally
            {
                if (lockObj.IsReadLockHeld)
                    lockObj.ExitReadLock();
            }
        }
        set => this.Seek(value, SeekOrigin.Begin);
    }

    /// <summary><inheritdoc/></summary>
    public override void Flush() { }

    /// <summary><inheritdoc/></summary>
    public override int Read(byte[] buffer, int offset, int count)
    {
        var lockObj = this._lock;

        try
        {
            lockObj.EnterUpgradeableReadLock();

            int pos = (int)this._position;
            int total = (int)this.Length;

            // 読み取り位置が総バイト数以上の場合は無効値を返す
            if (pos >= total)
                return 0;

            try
            {
                lockObj.EnterWriteLock();

                // コピーするバイト数を決定
                int actualCount = Math.Min(count, total - pos);

                // バッファにコピー
                var span = MemoryMarshal.Cast<T, byte>(this._data);
                span[pos..(pos + actualCount)].CopyTo(buffer.AsSpan(offset..));

                this._position += actualCount;

                return actualCount;
            }
            finally
            {
                if (lockObj.IsWriteLockHeld)
                    lockObj.ExitWriteLock();
            }
        }
        finally
        {
            if (lockObj.IsUpgradeableReadLockHeld)
                lockObj.ExitUpgradeableReadLock();
        }
    }

    /// <summary><inheritdoc/></summary>
    public override long Seek(long offset, SeekOrigin origin)
    {
        var lockObj = this._lock;

        try
        {
            lockObj.EnterUpgradeableReadLock();

            long newPosition = origin switch
            {
                SeekOrigin.Current => this._position + offset,
                SeekOrigin.Begin => offset,
                _ => this.Length + offset,
            };

            if (newPosition < 0)
                ArgumentOutOfRangeException.ThrowIfLessThan(newPosition, 0, nameof(offset));

            if (newPosition > this.Length)
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newPosition, this.Length, nameof(offset));

            try
            {
                lockObj.EnterWriteLock();

                this._position = newPosition;

                return newPosition;
            }
            finally
            {
                if (lockObj.IsWriteLockHeld)
                    lockObj.ExitWriteLock();
            }
        }
        finally
        {
            if (lockObj.IsUpgradeableReadLockHeld)
                lockObj.ExitUpgradeableReadLock();
        }
    }

    /// <summary><inheritdoc/></summary>
    public override void SetLength(long value)
        => throw new NotSupportedException();

    /// <summary><inheritdoc/></summary>
    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    ~StructArrayStream() => this.Dispose(false);
}
