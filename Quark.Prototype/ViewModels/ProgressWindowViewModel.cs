using System;
using Quark.Data.Projects;
using Quark.Mvvm;

namespace Quark.ViewModels;

internal class ProgressWindowViewModel : ViewModelBase, IProgress<ProgressReport>
{
    public ProgressWindowViewModel(string title, bool closeable = true) : base()
    {
        this._title = title;
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
        App.Instance.Dispatcher.InvokeAsync(() =>
        {
            double? progress = report.Progress;
            if (progress is not null)
            {
                this.Status = $"処理中... {progress}%";
                this.Progress = progress.Value;
            }

            var line = report.Line;
            if (line is not null)
            {
                this.Details = string.Concat(this.Details, report.Line, "\n");
            }

            this.IsFail = report.ReprotType == ProgressReportType.Error;
            this.IsWaiting = report.ReprotType == ProgressReportType.Idertimate;
            if (this.IsWaiting)
            {
                this.Status = "処理中...";
            }
        });
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
