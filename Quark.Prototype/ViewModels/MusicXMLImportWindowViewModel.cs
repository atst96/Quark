using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using Livet.Messaging.Windows;
using Microsoft.Extensions.DependencyInjection;
using Quark.Models.MusicXML;
using Quark.Models.Neutrino;
using Quark.Mvvm;
using Quark.Services;

namespace Quark.ViewModels;

/// <summary>
/// <see cref="MusicXMLImportWindow"/>のViewModel
/// </summary>
internal class MusicXMLImportWindowViewModel : ViewModelBase
{
    public class PartSelectInfo : ViewModelBase
    {
        /// <summary>インデックス</summary>
        public int Index { get; }

        /// <summary>パート番号</summary>
        public int No { get; }

        /// <summary>パート名</summary>
        public string PartName { get; }

        private string _trackName;
        /// <summary>トラック名</summary>
        public string TrackName
        {
            get => this._trackName;
            set => this.RaisePropertyChangedIfSet(ref this._trackName, value);
        }

        private bool _isImport;
        /// <summary>インポートの有無</summary>
        public bool IsImport
        {
            get => this._isImport;
            set => this.RaisePropertyChangedIfSet(ref this._isImport, value);
        }

        private ModelInfo? _singer;
        /// <summary>歌声</summary>
        public ModelInfo? Singer
        {
            get => this._singer;
            set => this.RaisePropertyChangedIfSet(ref this._singer, value);
        }

        public PartSelectInfo(int index, string? name)
        {
            this.Index = index;
            this.No = index + 1;
            this.PartName = name ?? string.Empty;
            this._trackName = name ?? string.Empty;
        }

        public bool IsValid()
            => !string.IsNullOrEmpty(this.TrackName) && this.Singer != null;
    }

    /// <summary>V1向けサービス</summary>
    private NeutrinoV1Service _v1Service;

    /// <summary>V2向けサービス</summary>
    private NeutrinoV2Service _v2Service;

    /// <summary>ファイルパス</summary>
    public string FilePath { get; set; }

    /// <summary>ファイル名</summary>
    public string FileName { get; set; }

    /// <summary>パート情報</summary>
    public PartSelectInfo[] Parts { get; }

    /// <summary>選択情報</summary>
    public PartSelectInfo[]? Response { get; private set; } = null;

    private ListCollectionView _models = new(Array.Empty<ModelInfo>());
    /// <summary>モデル情報</summary>
    public ListCollectionView Models
    {
        get => this._models;
        private set => this.RaisePropertyChangedIfSet(ref this._models, value);
    }

    private string _projectName = string.Empty;
    /// <summary>プロジェクト名</summary>
    public string ProjectName
    {
        get => this._projectName;
        set
        {
            if (this.RaisePropertyChangedIfSet(ref this._projectName, value))
                this.OnInputParameterUpdated();
        }
    }

    public MusicXMLImportWindowViewModel(string filePath, IEnumerable<PartList.ScorePartElement> partInfoList)
    {
        this.FilePath = filePath;
        this.FileName = Path.GetFileName(filePath);
        var parts = this.Parts = partInfoList.Select((p, i) => new PartSelectInfo(i, p.PartName)).ToArray();

        // 1パートしかない場合は選択済みにする
        if (parts.Length > 0 || parts.Length < 2)
            this.SelectedPart = parts[0];

        var provider = ServiceLocator.ServiceProvider;
        this._v1Service = provider.GetService<NeutrinoV1Service>()!;
        this._v2Service = provider.GetService<NeutrinoV2Service>()!;

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
            if (this.RaisePropertyChangedIfSet(ref this._selectedPart, value!))
            {
                foreach (var item in this.Parts)
                    // TODO: 複数選択できるようになったら単一選択は削除する
                    item.IsImport = item == value;

                this.EditPartPanelVisibility = value != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private Visibility _editPartPanelVisibility;

    public Visibility EditPartPanelVisibility
    {
        get => this._editPartPanelVisibility;
        private set => this.RaisePropertyChangedIfSet(ref this._editPartPanelVisibility, value);
    }

    /// <summary>
    /// モデル情報を更新する
    /// </summary>
    private void UpdateModels()
    {
        var models = Enumerable.Concat(
            this._v2Service.GetModels(),
            this._v1Service.GetModels()
        ).ToArray();

        var viewCollection = new ListCollectionView(models);
        viewCollection.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ModelInfo.ModelType)));
        this.Models = viewCollection;
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
            this.Messenger.Raise(new WindowActionMessage(WindowAction.Close));
        });

    /// <summary>
    /// 入力パラメータ更新時
    /// </summary>
    public void OnInputParameterUpdated()
    {
        this.CompleteCommand.RaiseCanExecute();
    }
}
