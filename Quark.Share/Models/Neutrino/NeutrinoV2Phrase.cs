using Quark.Projects.Tracks;

namespace Quark.Models.Neutrino;

internal class NeutrinoV2Phrase : INeutrinoPhrase
{
    public int No { get; }

    public int BeginTime { get; }

    public int EndTime { get; }

    public string Label { get; }

    public PhraseStatus Status { get; private set; }

    public float[]? Mspec { get; private set; }

    public float[]? F0 { get; private set; }

    public float[]? Mgc { get; private set; }

    public float[]? Bap { get; private set; }

    public NeutrinoV2Phrase(int no, int beginTime, int endTime, string label, PhraseStatus status)
    {
        this.No = no;
        this.BeginTime = beginTime;
        this.EndTime = endTime;
        this.Label = label;
        this.Status = status;
    }

    public override string ToString()
        => $"{nameof(NeutrinoV1Phrase)} {{ No: {this.No}, BeginTime: {this.BeginTime}, EndTime: {this.EndTime}, Label: {this.Label}, Status: {this.Status} }}";

    public void SetAudioFeatures(float[] f0, float[] mspec, float[] mgc, float[] bap)
        => (this.F0, this.Mspec, this.Mgc, this.Bap) = (f0, mspec, mgc, bap);

    public void SetStatus(PhraseStatus status)
        => this.Status = status;
}
