namespace Quark.Utils;

/// <summary>
/// Task操作に関するUtilクラス
/// </summary>
public static class TaskUtil
{
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
}
