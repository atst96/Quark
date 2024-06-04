using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IF0PhraseTrack<T>
    where T : IFloatingPointIeee754<T>
{
    public IF0Phrase<T>[] Phrases { get; }
}
