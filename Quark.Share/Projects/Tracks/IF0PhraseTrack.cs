using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IF0PhraseTrack<TPhrase, TNumber>
    where TPhrase : IF0Phrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    public TPhrase[] Phrases { get; }
}
