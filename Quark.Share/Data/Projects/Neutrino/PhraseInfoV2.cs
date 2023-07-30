using MemoryPack;

namespace Quark.Data.Projects.Neutrino;

[MemoryPackable]
public partial record class PhraseInfoV2(
    int No, int BeginTime, int EndTime, string[][] Phonemes, float[]? F0, float[]? Mspec, float[]? Mgc, float[]? Bap,
    float?[]? EditedF0, float?[]? EditedDynamics);
