using Quark.Projects.Tracks;

namespace Quark.Models.Neutrino;

public class NeutrinoV1Phrase : INeutrinoPhrase
{
    public int No { get; }

    public int BeginTime { get; }

    public int EndTime { get; }

    public string Label { get; }

    public PhraseStatus Status { get; private set; }

    public double[]? F0 { get; private set; }

    public double[]? Mgc { get; private set; }

    public double[]? Bap { get; private set; }

    public NeutrinoV1Phrase(int no, int beginTime, int endTime, string label, PhraseStatus status)
    {
        this.No = no;
        this.BeginTime = beginTime;
        this.EndTime = endTime;
        this.Label = label;
        this.Status = status;
    }

    public override string ToString()
        => $"{nameof(NeutrinoV1Phrase)} {{ No: {this.No}, BeginTime: {this.BeginTime}, EndTime: {this.EndTime}, Label: {this.Label}, Status: {this.Status} }}";

    public void SetAudioFeatures(double[] f0, double[] mgc, double[] bap)
        => (this.F0, this.Mgc, this.Bap) = (f0, mgc, bap);

    public void SetStatus(PhraseStatus status)
        => this.Status = status;
}
