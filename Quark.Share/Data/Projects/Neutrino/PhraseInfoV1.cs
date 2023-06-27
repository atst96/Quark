using MemoryPack;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial record class PhraseInfoV1(int No, int BeginTime, int EndTime, string[][] Phonemes, double[]? F0, double[]? Mgc, double[]? Bap);
