namespace Quark.Models.MusicXML;

public static class MusicXmlPhrase
{
    public class Frame
    {
        /// <summary>開始フレーム</summary>
        public required int BeginTime { get; init; }

        /// <summary>終了フレーム</summary>
        public required int EndTime { get; set; }

        /// <summary>歌詞</summary>
        public required string Lyrics { get; init; }

        /// <summary>ピッチ</summary>
        public required int Pitch { get; init; }

        /// <summary>ブレス記号</summary>
        public required bool Breath { get; set; }

        public void SetEndFrame(int endFrame)
            => this.EndTime = endFrame;

        public void SetBreath(bool breath)
            => this.Breath = breath;
    }
};
