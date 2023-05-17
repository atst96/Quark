using MemoryPack;

namespace Quark.Models.Neutrino;

[MemoryPackable]
public partial record PhraseInfo(int No, int Time, bool IsVoiced, string Label);
