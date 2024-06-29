using System.Numerics;

namespace Quark.Projects.Tracks;

public interface IMspecDynamicsPhraseTrack<TPhrase, TNumber>
    where TPhrase : IMspecDynamicsPhrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    public TPhrase[] Phrases { get; }
}
