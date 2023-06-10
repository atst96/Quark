namespace Quark.Utils;

/// <summary>
/// Task操作に関するUtilクラス
/// </summary>
public static class TaskUtil
{
    /// <summary>null(バイト配列)を返却するタスク</summary>
    public static Task<byte[]?> NullByteArrayTask { get; } = Task.FromResult<byte[]?>(null);

    /// <summary>
    /// <see cref="Task"/>を同期で待機して返却値を受け取る。
    /// </summary>
    /// <typeparam name="T">返却値の型</typeparam>
    /// <param name="task">Task</param>
    /// <returns>Taskの返却値</returns>
    public static T WaitForResult<T>(this Task<T> task)
    {
        task.Wait();
        return task.Result;
    }

    /// <summary>
    /// 2つのタスクを待機し、それぞれの処理結果をタプルで返却する。
    /// </summary>
    public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
    {
        await Task.WhenAll(task1, task2).ConfigureAwait(false);

        return (await task1.ConfigureAwait(false), await task2.ConfigureAwait(false));
    }

    /// <summary>
    /// 3つのタスクを待機し、それぞれの処理結果をタプルで返却する。
    /// </summary>
    public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3)
    {
        await Task.WhenAll(task1, task2, task3).ConfigureAwait(false);

        return (await task1, await task2, await task3);
    }

    /// <summary>
    /// 4つのタスクを待機し、それぞれの処理結果をタプルで返却する。
    /// </summary>
    public static async Task<(T1, T2, T3, T4)> WhenAll<T1, T2, T3, T4>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4)
    {
        await Task.WhenAll(task1, task2, task3, task4).ConfigureAwait(false);

        return (await task1, await task2, await task3, await task4);
    }

    /// <summary>
    /// 5つのタスクを待機し、それぞれの処理結果をタプルで返却する。
    /// </summary>
    public static async Task<(T1, T2, T3, T4, T5)> WhenAll<T1, T2, T3, T4, T5>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5)
    {
        await Task.WhenAll(task1, task2, task3, task4, task5).ConfigureAwait(false);

        return (await task1, await task2, await task3, await task4, await task5);
    }

    /// <summary>
    /// 6つのタスクを待機し、それぞれの処理結果をタプルで返却する。
    /// </summary>
    public static async Task<(T1, T2, T3, T4, T5, T6)> WhenAll<T1, T2, T3, T4, T5, T6>(Task<T1> task1, Task<T2> task2, Task<T3> task3, Task<T4> task4, Task<T5> task5, Task<T6> task6)
    {
        await Task.WhenAll(task1, task2, task3, task4, task5).ConfigureAwait(false);

        return (await task1, await task2, await task3, await task4, await task5, await task6);
    }
}
