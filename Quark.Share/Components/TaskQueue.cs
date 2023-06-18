using System.Collections;

namespace Quark.Components;

/// <summary>非同期でタスクを実行するクラス</summary>
/// <typeparam name="T">データ要素の型</typeparam>
public class TaskQueue<T> : IEnumerable<T>
{
    /// <summary>排他ロック用オブジェクト</summary>
    private readonly object @_lock = new();

    /// <summary>データ処理時のタスク</summary>
    private readonly Func<T, Task> _task;

    /// <summary>データキュー</summary>
    private readonly Queue<T> _queue = new();

    /// <summary>セッションの有効/無効フラグ</summary>
    private bool _isEnabled = false;

    /// <summary>現在の実行数</summary>
    private int _currentTaskCount;

    /// <summary>最大実行数</summary>
    private readonly int _maxTaskCount;

    /// <summary>現在実行中のタスク</summary>
    public LinkedList<T> Runnings { get; } = new();

    /// <summary>インスタンスを生成する</summary>
    /// <param name="maxTaskCount">同時に実行できるタスク数</param>
    /// <param name="task">データ処理タスク</param>
    public TaskQueue(int maxTaskCount, Func<T, Task> task)
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
    public void Enqueue(T item)
    {
        lock (this._lock)
            this._queue.Enqueue(item);

        this.ExecuteNext();
    }

    /// <summary>次の実行待ちがない事を取得する</summary>
    private bool CanNext()
        => this._isEnabled && this._currentTaskCount < this._maxTaskCount;

    /// <summary>次の実行を要求する</summary>
    private void ExecuteNext()
    {
        lock (this.@_lock)
        {
            // 次の実行が可能かつキューから取得できればキューから削除する
            while (this.CanNext() && this._queue.TryDequeue(out var item))
            {
                // キャンセル済みなら現在要素の処理をスキップする
                if (item is ITaskCancellable cancellable && cancellable.IsCancelled)
                    continue;

                //　実行中のタスク数をインクリメントする
                lock (this.@_lock)
                {
                    ++this._currentTaskCount;
                    this.Runnings.AddLast(item);
                }

                this._task(item)
                    .ContinueWith(_ =>
                    {
                        // タスク終了時

                        // 実行中のタスク数をデクリメントする
                        lock (this.@_lock)
                        {
                            --this._currentTaskCount;
                            this.Runnings.Remove(item);
                        }

                        // 次の処理
                        this.ExecuteNext();
                    });
            }
        }
    }

    /// <summary>Enumeratorを取得する</summary>
    public IEnumerator<T> GetEnumerator()
        => this._queue.GetEnumerator();

    /// <summary>Enumeratorを取得する</summary>
    IEnumerator IEnumerable.GetEnumerator()
        => this.GetEnumerator();
}
