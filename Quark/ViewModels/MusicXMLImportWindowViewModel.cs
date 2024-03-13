using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Quark.Models.MusicXML;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Services;
using Quark.UI.Mvvm;

namespace Quark.ViewModels;

/// <summary>
/// <see cref="MusicXMLImportWindow"/>のViewModel
/// </summary>
internal class MusicXMLImportWindowViewModel : ViewModelBase, IDialogServiceViewModel
{
    /// <summary>V1向けサービス</summary>
    private NeutrinoV1Service _v1Service;

    /// <summary>V2向けサービス</summary>
    private NeutrinoV2Service _v2Service;

    /// <inheritdoc/>
    public DialogService DialogService { get; }

    /// <summary>ファイルパス</summary>
    public string FilePath { get; set; }

    /// <summary>ファイル名</summary>
    public string FileName { get; set; }

    /// <summary>パート情報</summary>
    public PartSelectInfo[] Parts { get; }

    /// <summary>選択情報</summary>
    public PartSelectInfo[]? Response { get; private set; } = null;

    private IList<ModelInfo> _models = [];
    /// <summary>モデル情報</summary>
    public IList<ModelInfo> Models
    {
        get => this._models;
        private set => this.SetProperty(ref this._models, value);
    }

    private string _projectName = string.Empty;
    /// <summary>プロジェクト名</summary>
    public string ProjectName
    {
        get => this._projectName;
        set
        {
            if (this.SetProperty(ref this._projectName, value))
                this.OnInputParameterUpdated();
        }
    }

    public MusicXMLImportWindowViewModel(
        DialogService dialogService,
        NeutrinoV1Service v1Service, NeutrinoV2Service v2Service,
        string filePath, IEnumerable<ScorePartElement> partInfoList)
    {
        this.DialogService = dialogService;
        this._v1Service = v1Service;
        this._v2Service = v2Service;

        this.FilePath = filePath;
        this.FileName = Path.GetFileName(filePath);
        var parts = this.Parts = partInfoList.Select((p, i) => new PartSelectInfo(i, p.PartName)).ToArray();

        // 1パートしかない場合は選択済みにする
        if (parts.Length > 0 || parts.Length < 2)
            this.SelectedPart = parts[0];

        this.UpdateModels();
    }

    private PartSelectInfo? _selectedPart;

    /// <summary>
    /// 選択中のパート情報
    /// </summary>
    public PartSelectInfo? SelectedPart
    {
        get => this._selectedPart;
        set
        {
            if (this.SetProperty(ref this._selectedPart, value!))
            {
                foreach (var item in this.Parts)
                    // TODO: 複数選択できるようになったら単一選択は削除する
                    item.IsImport = item == value;

                this.IsEditPartPanelVisible = value != null;
            }
        }
    }

    private bool _editPartPanelVisibility;

    public bool IsEditPartPanelVisible
    {
        get => this._editPartPanelVisibility;
        private set => this.SetProperty(ref this._editPartPanelVisibility, value);
    }

    /// <summary>
    /// モデル情報を更新する
    /// </summary>
    private void UpdateModels()
    {
        // TODO: 現時点ではv2モデルのみ。将来的にはv1も選べるようにしたい。
        this.Models = this._v2Service.GetModels();
    }

    private bool IsValid()
    {
        return !string.IsNullOrEmpty(this.ProjectName)
            && this.Parts.Where(x => x.IsImport).All(x => x.IsValid());
    }

    private Command? _completeCommand;

    /// <summary>
    /// 入力完了コマンド
    /// </summary>
    public Command CompleteCommand => this._completeCommand ??= this.AddCommand(
        this.IsValid,
        () =>
        {
            this.Response = this.Parts.Where(x => x.IsImport).ToArray();
            this.DialogService.Close();
        });

    public void OnInputParameterUpdated()
    {
        this.CompleteCommand.RaiseCanExecute();
    }

    private ICommand? _onInputParameterUpdatedCommand;
    public ICommand OnInputParameterUpdatedCommand => this._onInputParameterUpdatedCommand ??= this.AddCommand(() =>
    {
        this.OnInputParameterUpdated();
    });
}
