namespace Quark.Neutrino;

public class NSFV2Model
{
    /// <summary>ファイル名</summary>
    public string FileName { get; }

    private NSFV2Model(string fileName)
    {
        this.FileName = fileName;
    }

    public static NSFV2Model VA { get; } = new NSFV2Model("va.bin");

    public static NSFV2Model VE { get; } = new NSFV2Model("ve.bin");

    public static NSFV2Model VS { get; } = new NSFV2Model("vs.bin");

    public static NSFV2Model Get(NeutrinoV2InferenceMode mode) => mode switch
    {
        NeutrinoV2InferenceMode.Advanced => VA,
        NeutrinoV2InferenceMode.Standard => VS,
        NeutrinoV2InferenceMode.Elements => VE,
        _ => throw new NotImplementedException(),
    };
}
