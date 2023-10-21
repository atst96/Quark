using Quark.Services;

namespace Quark.Components;

/// <summary>非同期でタスクを実行するクラス</summary>
/// <typeparam name="TElement">データ要素の型</typeparam>
/// <typeparam name="TPriority">優先度を示す情報</typeparam>
public class TaskQueue<TElement, TPriority>
    where TElement : ITaskQueueElement
{
    /// <summary>排他ロック用オブジェクト</summary>
    private readonly object @_lock = new();

    /// <summary>データ処理時のタスク</summary>
    private readonly Func<TElement, Task> _task;

    /// <summary>データキュー</summary>
    private readonly PriorityQueue<TElement, TPriority> _queue = new();

    /// <summary>セッションの有効/無効フラグ</summary>
    private bool _isEnabled = false;

    /// <summary>現在の実行数</summary>
    private int _currentTaskCount;

    /// <summary>最大実行数</summary>
    private readonly int _maxTaskCount;

    /// <summary>現在実行中のタスク</summary>
    private readonly LinkedList<TElement> _runnings = new();

    /// <summary>キューの件数</summary>
    public int QueueCount => this._queue.Count;

    /// <summary>インスタンスを生成する</summary>
    /// <param name="maxTaskCount">同時に実行できるタスク数</param>
    /// <param name="task">データ処理タスク</param>
    public TaskQueue(int maxTaskCount, Func<TElement, Task> task)
    {
        if (maxTaskCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTaskCount));

        if (task is null)
            throw new ArgumentNullException(nameof(task));

        this._task = task;
        this._currentTaskCount = 0;
        this._maxTaskCount = maxTaskCount;
    }

    /// <summary>セッションを開始すする</summary>
    public void BeginSession()
    {
        lock (this.@_lock)
            this._isEnabled = true;

        this.ExecuteNext();
    }

    /// <summary>セッションを終了する</summary>
    public void EndSession()
    {
        lock (this.@_lock)
            this._isEnabled = false;
    }

    /// <summary>処理データを追加する</summary>
    /// <param name="item">処理対象のデータ</param>
    public void Enqueue(TElement item, TPriority priority)
    {
        lock (this.@_lock)
        {
            this._queue.Enqueue(item, priority);
        }

        this.ExecuteNext();
    }

    /// <summary>次の実行待ちがない事を取得する</summary>
    private bool GetNext(out TElement element)
    {
        lock (this.@_lock)
        {
            if (this._isEnabled && this._currentTaskCount < this._maxTaskCount)
            {
                if (this._queue.TryDequeue(out var item, out _))
                {
                    element = item;
                    return true;
                }
            }
        }

        element = default!;
        return false;
    }

    /// <summary>次の実行を要求する</summary>
    private void ExecuteNext()
    {
        // 次の実行が可能かつキューから取得できればキューから削除する
        while (this.GetNext(out var item))
        {
            // キャンセル済みなら現在要素の処理をスキップする
            if (item is ITaskCancellable cancellable && cancellable.IsCancelled)
                continue;

            //　実行中のタスク数をインクリメントする
            lock (this.@_lock)
            {
                ++this._currentTaskCount;
                this._runnings.AddLast(item);
            }

            this._task(item)
                .ContinueWith(static (_, obj) =>
                {
                    // タスク終了時
                    var (@this, item) = (ValueTuple<TaskQueue<TElement, TPriority>, TElement>)obj!;

                    // 実行中のタスク数をデクリメントする
                    lock (@this.@_lock)
                    {
                        --@this._currentTaskCount;
                        @this._runnings.Remove(item);
                    }

                    // 次の処理
                    @this.ExecuteNext();
                },
                state: (this, item));
        }
    }

    /// <summary>
    /// 実行前または実行中のタスクをキャンセルする
    /// </summary>
    /// <param name="func"></param>
    /// <returns></returns>
    public Task Cancel(Func<TElement, bool> func)
    {
        var canceleldTasks = new List<Task>();

        lock (this.@_lock)
        {
            var queueItems = this._runnings
                .Concat(this._queue.UnorderedItems.Select(i => i.Element))
                .Where(i => i.IsRunningOrRunnable)
                .Where(func);

            foreach (var item in queueItems)
            {
                item.Cancel();

                var task = item.Task;
                if (task != null)
                    canceleldTasks.Add(task);
            }
        }

        return Task.WhenAll(canceleldTasks);
    }
}
