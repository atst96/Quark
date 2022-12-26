namespace NEUTRINO_Test;

public partial class Form1 : Form
{
    public Form1()
    {
        this.InitializeComponent();

        var f0File = @"path/to/your/f0_file.f0";
        var mgcFile = @"path/to/your/mgc_file.mgc";
        var labFile = @"path/to/your/lab_file.lab";
        var scoreFile = @"path/to/your/no/compressed/musicxml_file.musicxml";
        var output = @"path/to/your/no/compressed/waves.png";

        //// 音響情報解析
        //var accoustic = SoundFileAnalyzer.Analyze(f0File, mgcFile, labFile);

        //// 楽譜情報解析
        //var scores = MusicXMLAnalyzer.Analyzer(scoreFile);

        this.scoreEditor1.Load(f0File, mgcFile, labFile, scoreFile);
    }
}
