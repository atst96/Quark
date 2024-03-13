using Quark.Models.Neutrino;
using Quark.Mvvm;

namespace Quark.ViewModels;

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
        set => this.SetProperty(ref this._trackName, value);
    }

    private bool _isImport;
    /// <summary>インポートの有無</summary>
    public bool IsImport
    {
        get => this._isImport;
        set => this.SetProperty(ref this._isImport, value);
    }

    private ModelInfo? _singer;
    /// <summary>歌声</summary>
    public ModelInfo? Singer
    {
        get => this._singer;
        set => this.SetProperty(ref this._singer, value);
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
