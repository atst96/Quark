using System;
using Avalonia.Threading;
using NAudio.Wave;
using Quark.Audio;
using Quark.Mvvm;
using Quark.Projects;
using Quark.Projects.Tracks;
using Quark.Services;

namespace Quark.ViewModels;

internal class ProjectViewModel : ViewModelBase
{
    private readonly ViewModelFactory _viewModelFactory;

    /// <summary>プロジェクト</summary>
    public Project Project { get; }

    private readonly ProjectPlayer _player;

    private readonly DispatcherTimer _playerTimer;

    public ProjectViewModel(ViewModelFactory viewModelFactory, Project project)
    {
        this._viewModelFactory = viewModelFactory;
        this.Project = project;
        this._player = project.Player;
        this._playerTimer = new(
            TimeSpan.FromMilliseconds(1000d / 60),
            DispatcherPriority.Normal,
            this.OnMonitoringTimerTicked)
        {
            IsEnabled = false
        };
    }

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => this._isPlaying;
        private set => this.SetProperty(ref this._isPlaying, value);
    }

    private TimeSpan _playingTime = TimeSpan.Zero;
    public TimeSpan PlayingTime
    {
        get => this._playingTime;
        private set => this.SetProperty(ref this._playingTime, value);
    }

    private TimeSpan _selectionTime = TimeSpan.Zero;
    public TimeSpan SelectionTime
    {
        get => this._selectionTime;
        set
        {
            this.SetProperty(ref this._selectionTime, value);
            this.UpdatePlayingTime(value);
        }
    }

    private void UpdatePlayingTime(TimeSpan playingTime)
    {
        this.PlayingTime = playingTime;
        if (this.IsPlaying)
        {
            this.SeekPlayer(playingTime);
        }
    }

    private Command? _playCommand;
    public Command PlayCommand => this._playCommand ??= this.AddCommand(() => this.StartPlayer());


    private Command? _togglePlayCommand;
    public Command TogglePlayCommand => this._togglePlayCommand ??= this.AddCommand(() =>
    {
        var player = this._player;

        if (player.PlaybackState != PlaybackState.Playing)
            this.StartPlayer();
        else
            this.StopPlayer(false);
    });

    private Command? _togglePlayResumeCommand;
    public Command TogglePlayResumeCommand => this._togglePlayResumeCommand ??= this.AddCommand(() =>
    {
        if (this._player is { } player)
        {
            if (player.PlaybackState != PlaybackState.Playing)
                this.StartPlayer(this._beginPlayTime ?? this.PlayingTime);
            else
                this.StopPlayer(true);
        }
    });

    private Command? _stopCommand;
    public Command StopCommand => this._stopCommand ??= this.AddCommand(() => this.StopPlayer(false));

    private Command? _stopRestoreCommand;
    public Command StopRestoreCommand => this._stopRestoreCommand ??= this.AddCommand(() => this.StopPlayer(true));

    private TimeSpan? _beginPlayTime = null;

    /// <summary>
    /// 再生開始する。
    /// </summary>
    /// <param name="beginTime">再生開始位置</param>
    private void StartPlayer(TimeSpan? beginTime = null)
    {
        // 再生開始時間を設定
        if (beginTime != null)
        {
            this._beginPlayTime = beginTime.Value;
            this.PlayingTime = beginTime.Value;
        }
        else
        {
            this._beginPlayTime = this.PlayingTime;
        }

        // 再生開始
        if (this._player is { } player)
        {
            this.SeekPlayer(this.PlayingTime);
            player.Play();

            this.IsPlaying = true;
            // 再生位置監視タイマを開始
            this.StartMonitoringTimer();
        }
    }

    /// <summary>
    /// プレーやを停止する。
    /// </summary>
    /// <param name="restore">再生位置を戻すフラグ</param>
    private void StopPlayer(bool restore)
    {
        if (this._player is { } player)
        {
            // 再生位置監視タイマを停止
            this.StopMonitoringTimer();
            // 再生停止
            player.Stop();

            if (restore && this._beginPlayTime is { } beginTime)
                this.PlayingTime = beginTime;
        }

        this.IsPlaying = false;
    }

    /// <summary>再生位置監視タイマを開始</summary>
    private void StartMonitoringTimer()
    {
        this._playerTimer.Start();
    }

    /// <summary>再生位置監視タイマを停止</summary>
    private void StopMonitoringTimer()
    {
        this._playerTimer.Stop();
    }

    private NeutrinoTrackViewModelBase? _selectedTrack;
    public NeutrinoTrackViewModelBase? SelectedTrack
    {
        get => this._selectedTrack;
        private set => this.SetProperty(ref this._selectedTrack, value);
    }


    public void SelectTrack(INeutrinoTrack track)
    {
        this.SelectedTrack = this._viewModelFactory.GetTrackViewModel(this, track);
    }

    /// <summary>
    /// 再生位置監視タイマのイベント発火時
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnMonitoringTimerTicked(object? sender, EventArgs e)
    {
        this.PlayingTime = this._player.CurrentTime;
    }

    private void SeekPlayer(TimeSpan time)
    {
        this._player?.Seek(time);
    }
}
