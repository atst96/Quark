using System.Buffers;
using System.Runtime.CompilerServices;

namespace World.NET;

internal struct ArrayRent<T> : IDisposable
    where T : struct
{
    private bool _isDisposed;
    private readonly int _size;
    private T[]? _array;

    private ArrayRent(int size)
    {
        this._size = size;
        this._array = ArrayPool<T>.Shared.Rent(size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ArrayRent<T> Create(int size)
        => new(size);

    public Span<T> GetSpan() => this._array!.AsSpan(..this._size);

    public void Dispose()
    {
        if (this._isDisposed)
            return;

        this._isDisposed = true;

        ArrayPool<T>.Shared.Return(this._array!);
    }

    //~ArrayRent()
    //{
    //    this.Dispose();
    //}
}
