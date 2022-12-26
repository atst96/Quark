namespace NEUTRINO_Test;

public partial class Form1 : Form
{
    public Form1()
    {
        this.InitializeComponent();

        var f0File = @"C:\Users\Shota\Desktop\NEUTRINO‰ğÍ\Setsuna.f0";
        var mgcFile = @"C:\Users\Shota\Desktop\NEUTRINO‰ğÍ\Setsuna.mgc";
        var labFile = @"C:\Users\Shota\Desktop\NEUTRINO‰ğÍ\score\timing\Setsuna.lab";
        var scoreFile = @"C:\Users\Shota\Desktop\NEUTRINO‰ğÍ\Setsuna.musicxml";
        var output = @"C:\Users\Shota\Desktop\NEUTRINO‰ğÍ\waves.png";

        //// ‰¹‹¿î•ñ‰ğÍ
        //var accoustic = SoundFileAnalyzer.Analyze(f0File, mgcFile, labFile);

        //// Šy•ˆî•ñ‰ğÍ
        //var scores = MusicXMLAnalyzer.Analyzer(scoreFile);

        this.scoreEditor1.Load(f0File, mgcFile, labFile, scoreFile);
    }
}