using Quark.Models.Neutrino;

namespace Quark.Projects.Tracks;

public interface INeutrinoPhrase
{
    public int No { get; }

    public int BeginTime { get; }

    public int EndTime { get; }

    public string Label { get; }

    public PhraseStatus Status { get; }
}
