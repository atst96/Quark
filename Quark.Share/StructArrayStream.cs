namespace Quark;

public class StructArrayStream
{
    public static async Task CopyToAsync<T>(T[] data, Stream destination)
        where T : unmanaged
    {
        using var stream = new StructArrayStream<T>(data);
        await stream.CopyToAsync(destination).ConfigureAwait(false);
        await destination.FlushAsync().ConfigureAwait(false);
    }
}
