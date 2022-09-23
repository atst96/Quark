namespace NEUTRINO_Test;

public record MusicXmlPhrase(IList<MusicXmlPhrase.Frame> Frames)
{
    public class Frame
    {
        public int BeginFrame { get; set; }

        public int EndFrame { get; set; }

        public string Lyrics { get; set; }

        public int Pitch { get; set; }

        public Frame()
        {
        }

        public Frame(int beginFrame, int endFrame, string lyrics, int pitch)
        {
            this.BeginFrame = beginFrame;
            this.EndFrame = endFrame;
            this.Lyrics = lyrics;
            this.Pitch = pitch;
        }
    }
};
