using System.Collections.Generic;

namespace Quark.Models.MusicXML;

public record MusicXmlPhrase(IList<MusicXmlPhrase.Frame> Frames)
{
    public class Frame
    {
        /// <summary>
        /// 開始フレーム
        /// </summary>
        public int BeginFrame { get; init; }

        /// <summary>
        /// 終了フレーム
        /// </summary>
        public int EndFrame { get; private set; }

        /// <summary>
        /// 歌詞
        /// </summary>
        public string Lyrics { get; init; }

        /// <summary>
        /// ピッチ
        /// </summary>
        public int Pitch { get; init; }

        /// <summary>
        /// ブレス記号
        /// </summary>
        public bool Breath { get; private set; }

        public Frame(int beginFrame, int endFrame, string lyrics, int pitch, bool breath)
        {
            this.BeginFrame = beginFrame;
            this.EndFrame = endFrame;
            this.Lyrics = lyrics;
            this.Pitch = pitch;
            this.Breath = breath;
        }

        public void SetEndFrame(int endFrame)
        {
            this.EndFrame = endFrame;
        }

        public void SetBreath(bool breath)
        {
            this.Breath = breath;
        }
    }
};
