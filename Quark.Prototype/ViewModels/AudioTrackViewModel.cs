using Quark.Mvvm;
using Quark.Projects.Tracks;

namespace Quark.ViewModels;

internal class AudioTrackViewModel(AudioFileTrack track) : ViewModelBase
{
    /// <summary>トラック</summary>
    private AudioFileTrack _track = track;

    /// <summary>トラックのボリュームを取得または変更する</summary>
    public float Volumne
    {
        get => this._track.Volume;
        set
        {
            var track = this._track;

            if (track.Volume != value)
            {
                track.Volume = value;
                this.RaisePropertyChanged();
            }
        }
    }

    /// <summary>トラックのミュート状態を取得または変更する</summary>
    public bool IsMute
    {
        get => this._track.IsMute;
        set
        {
            var track = this._track;

            if (track.IsMute != value)
            {
                track.IsMute = value;
                this.RaisePropertyChanged();
            }
        }
    }
}
