using MemoryPack;

namespace Quark.Models.Neutrino;

[MemoryPackable]
public partial record TimingInfo(long BeginTimeNs, long EndTimeNs, string Phoneme);
