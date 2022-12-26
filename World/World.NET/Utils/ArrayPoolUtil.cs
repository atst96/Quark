using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace World.NET.Utils;

internal static class ArrayUtil
{
    public static T[] Rent<T>(int minimumLength)
        => ArrayPool<T>.Shared.Rent(minimumLength);

    public static void Return<T>(ref T[] array)
        => ArrayPool<T>.Shared.Return(array);
}
