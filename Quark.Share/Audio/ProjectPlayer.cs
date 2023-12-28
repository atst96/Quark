using NAudio.Wave;
using Quark.Projects;

namespace Quark.Audio;

public class ProjectPlayer : IDisposable
{
    private readonly Project _project;
    private readonly AudioTrackMixer _mixer;
    private IWavePlayer? _device;

    public EventHandler<StoppedEventArgs> PlaybackStopped;

    internal ProjectPlayer(Project project)
    {
        this._project = project;
        this._mixer = new(project.Tracks);
    }

    public void BindDevice(Func<IWavePlayer> deviceResolver)
    {
        ArgumentNullException.ThrowIfNull(deviceResolver, nameof(deviceResolver));

        var device = deviceResolver();

        bool isPlay = this._device?.PlaybackState == PlaybackState.Playing;
        this.UnboundDevice();

        this._device = device;
        device.Init(this._mixer);

        if (isPlay)
            device.Play();
    }

    public void UnboundDevice()
    {
        var device = this._device;
        if (device is null)
            return;

        device.Stop();
        device.Dispose();
        this._device = null;
    }

    public void Dispose()
    {
        this.UnboundDevice();
    }

    public void Seek(TimeSpan position)
    {
        this._mixer.Seek(position);
    }

    public PlaybackState PlaybackState
        => this._device?.PlaybackState ?? PlaybackState.Stopped;

    public void Play()
    {
        this._device?.Play();
    }

    public void Stop()
    {
        this._device?.Stop();
    }

    public void Pause()
    {
        this._device?.Pause();
    }
}
