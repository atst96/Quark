using System.Runtime.CompilerServices;
using Quark.Behaviors;
using Quark.Mvvm;

namespace Quark.ViewModels;

internal class NewProjectWindowViewModel : ViewModelBase
{
    private bool _isInvalid = false;

    public bool IsInvalid
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._isInvalid;
        private set => this.RaisePropertyChangedIfSet(ref this._isInvalid, value);
    }

    private string _projectName = string.Empty;

    /// <summary>
    /// プロジェクト名
    /// </summary>
    public string ProjectName
    {
        get => this._projectName;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._projectName, value))
            {
                this.Validate();
            }
        }
    }

    private string _workindDirectory = string.Empty;

    /// <summary>
    /// 作業フォルダ
    /// </summary>
    public string WorkingDirectory
    {
        get => this._workindDirectory;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._workindDirectory, value))
            {
                this.Validate();
            }
        }
    }

    private void Validate()
    {
        this.IsInvalid = !(string.IsNullOrWhiteSpace(this.ProjectName) || string.IsNullOrWhiteSpace(this.WorkingDirectory));
    }


    public void OnWorkDirectorySelected(FolderSelectionMessage message)
    {
        if (message.Response is not null)
        {
            this.WorkingDirectory = message.Response;
        }
    }

    /// <summary>
    /// 入力内容をクリアする
    /// </summary>
    public void Clear()
    {
        this._projectName = string.Empty;
        this._workindDirectory = string.Empty;
    }
}
