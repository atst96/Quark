namespace NEUTRINO_Test;

public partial class Form1 : Form
{
    public Form1()
    {
        this.InitializeComponent();

        var f0File = @"path/to/your/f0_file";
        var mgcFile = @"path/to/your/mgc_file";
        var labFile = @"path/to/your/lab_file";
        var scoreFile = @"path/to/your/no/compressed/musicxml_file";

        //// ‰¹‹¿î•ñ‰ğÍ
        //var accoustic = SoundFileAnalyzer.Analyze(f0File, mgcFile, labFile);

        //// Šy•ˆî•ñ‰ğÍ
        //var scores = MusicXMLAnalyzer.Analyzer(scoreFile);

        this.scoreEditor1.Load(f0File, mgcFile, labFile, scoreFile);
    }
}