using System.Numerics;
using Quark.Projects.Tracks;

namespace Quark.Renderers;

public interface IMgcDynamicsPhraseTrack<TPhrase, TNumber>
    where TPhrase : IMgcDynamicsPhrase<TNumber>
    where TNumber : IFloatingPointIeee754<TNumber>
{
    public TPhrase[] Phrases { get; }
}
