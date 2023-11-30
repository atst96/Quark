using System;
using Quark.Data.Projects;
using Quark.Mvvm;

namespace Quark.ViewModels;

internal class ProgressWindowViewModel : ViewModelBase, IProgress<ProgressReport>
{
    public ProgressWindowViewModel(string title, bool closeable = true) : base()
    {
        this._title = title;
        this.Closeable = closeable;
    }

    private record ProgressLine(int BeginIndex, int Length)
    {
        public int EndIndex { get; } = BeginIndex + Length;
    }

    private ProgressLine? _progressLine = null;

    private void AppendLine(string text)
    {
        this.Details = string.Concat(this.Details, text, "\n");
    }



    private bool _closeable = true;
    public bool Closeable
    {
        get => this._closeable;
        set => this.RaisePropertyChangedIfSet(ref this._closeable, value);
    }

    private string _title = "Progress";
    public string Title
    {
        get => this._title;
        set => this.RaisePropertyChangedIfSet(ref this._title, value);
    }

    private double _progress;
    public double Progress
    {
        get => this._progress;
        set => this.RaisePropertyChangedIfSet(ref this._progress, value);
    }

    private string _status = string.Empty;
    public string Status
    {
        get => this._status;
        set => this.RaisePropertyChangedIfSet(ref this._status, value);
    }

    private string _details = string.Empty;
    public string Details
    {
        get => this._details;
        set => this.RaisePropertyChangedIfSet(ref this._details, value);
    }

    private bool _isWaiting;
    public bool IsWaiting
    {
        get => this._isWaiting;
        set => this.RaisePropertyChangedIfSet(ref this._isWaiting, value);
    }

    private bool _isFail;
    public bool IsFail
    {
        get => this._isFail;
        set => this.RaisePropertyChangedIfSet(ref this._isFail, value);
    }
    void IProgress<ProgressReport>.Report(ProgressReport report)
    {
        // UIスレッドで実行する
        App.Instance.Dispatcher.InvokeAsync((Action)(() =>
        {
            var progress = report.Progress;
            if (progress != null)
            {
                this.Status = $"処理中... {progress}%";
                this.Progress = progress.Value;
            }

            if (report.Line is { } line)
            {
                if (progress != null)
                {
                    // 進捗率の出力行

                    if (this._progressLine is { } prevProgressLine)
                    {
                        // 前回 進捗率を出力した文字位置がわかるなら、その部分だけ新しい文字列に置き換える
                        var prevText = this.Details;
                        this.Details = string.Concat(prevText[..prevProgressLine.BeginIndex], line, prevText[prevProgressLine.EndIndex..]);
                        // 進捗率の出力行の文字位置を更新する
                        this._progressLine = new(prevProgressLine.BeginIndex, line.Length);
                    }
                    else
                    {
                        // 進捗率の出力が初めて、もしくは一旦リセットされているなら、
                        // 現在の出力情報を追記して進捗率の出力情報を更新する
                        this._progressLine = new(this.Details.Length, line.Length);
                        this.AppendLine(line);
                    }
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    // 0文字または空白文字のみの行かつ、進捗率の出力情報がなければそのまま追記する
                    if (this._progressLine == null)
                    {
                        // 進捗率が出力された行がない場合、そのまま追加する
                        this.AppendLine(line);
                    }
                }
                else
                {
                    // 進捗率以外が出力された場合は、後続で次タスクの進捗率を受信する可能性を考慮して出力情報をクリアする
                    this.AppendLine(line);
                    this._progressLine = null;
                }
            }

            this.IsFail = report.ReprotType == ProgressReportType.Error;
            this.IsWaiting = report.ReprotType == ProgressReportType.Idertimate;
            if (this.IsWaiting)
            {
                this.Status = "処理中...";
            }
        }));
    }

    public void CanClose() => this.Closeable = true;

    public void Close()
    {
        this.Messenger.Raise(new("WindowClose"));
    }

    public void Clear(double progress = 0, bool isWaiting = false, bool isFail = false, bool? closeable = null)
    {
        this.Status = string.Empty;
        this.Details = string.Empty;
        this.Progress = progress;
        this.IsWaiting = isWaiting;
        this.IsFail = isFail;
        if (closeable.HasValue)
        {
            this.Closeable = closeable.Value;
        }
    }
}
