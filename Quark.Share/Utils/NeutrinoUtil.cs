using System.Text;
using System.Text.RegularExpressions;
using Quark.Extensions;
using Quark.Models.Neutrino;

namespace Quark.Utils;

/// <summary>NEUTRINOに関するUtilクラス</summary>
public static partial class NeutrinoUtil
{
    /// <summary>フレーズ情報パース用の正規表現</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"^(?<beginTime>\d+)\s(?<endTime>\d+)\s(?<phoneme>.+)$", RegexOptions.Compiled)]
    private static partial Regex GetTimingRegex();

    /// <summary>文字列を改行で分割して正規表現で解析する</summary>
    /// <param name="input">入力</param>
    /// <param name="regex">正規表現</param>
    /// <returns>正規表現に一致した要素</returns>
    private static IEnumerable<Match> SplitWithRegex(string input, Regex regex)
        => input
            .Split('\n', '\r')
            .Select(s => regex.Match(s)).Where(r => r.Success);

    /// <summary>
    /// </summary>
    /// <typeparam name="T">出力の型</typeparam>
    /// <param name="input">入力</param>
    /// <param name="regex">正規表現</param>
    /// <param name="conv">変換用関数</param>
    /// <returns></returns>
    private static IEnumerable<T> ParseWithRegex<T>(string input, Regex regex, Func<Match, T> conv)
        => SplitWithRegex(input, regex).Select(conv);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timing"></param>
    /// <returns></returns>
    public static TimingInfo[] ParseTiming(string timing)
    {
        return ParseWithRegex(timing, GetTimingRegex(),
            r => new TimingInfo(
                r.GetValue<long>("beginTime"),
                r.GetValue<long>("endTime"),
                r.GetValue("phoneme")))
            .ToArray();
    }

    /// <summary>フレーズ情報パース用の正規表現</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"^(?<no>\d+)\s(?<time>\d+)\s(?<flag>\d)\s(?<label>.+)$", RegexOptions.Compiled)]
    private static partial Regex GetPhraseRegex();

    /// <summary>
    /// TODO: メソッドを分解する
    /// フレーズ情報をパースする
    /// </summary>
    /// <param name="phraseContent">NEUTRINOから出力されたフレーズ情報</param>
    /// <returns></returns>
    public static (PhraseInfo[], PhraseInfo2[]) ParsePhrases(string phraseContent, TimingInfo[] timing)
    {
        var parsedPhrases = ParseWithRegex<PhraseInfo>(phraseContent, GetPhraseRegex(),
            r => new(
                r.GetValue<int>("no"),
                r.GetValue<int>("time"),
                r.GetValue<int>("flag") != 0,
                r.GetValue("label")))
            .ToArray();

        var phrases = new PhraseInfo2[parsedPhrases.Count(p => p.IsVoiced)];

        for (int idx = 0, destIdx = 0; idx < parsedPhrases.Length; ++idx)
        {
            // idx: parsedPhrasesの要素インデックス
            // destIdx: phrasesの要素インデックス

            (int no, int beginTime, bool isVoiced, string label) = parsedPhrases[idx];

            // TODO: 有声でない場合は省く(今後の実装で変わるかも)
            if (!isVoiced)
                continue;

            // 終了するフレーム番号を取得する
            // 最後の要素の場合はタイミング情報の最後から終了位置を取得する。
            int endFrameIdx = parsedPhrases.Length > (idx + 1)
                ? (parsedPhrases[idx + 1].Time - 1)
                : (int)Math.Ceiling(timing.Last().EndTimeNs / 10000d);

            phrases[destIdx++] = new(no, beginTime, endFrameIdx, label, PhraseStatus.WaitEstimate);
        }

        return (parsedPhrases, phrases);
    }

    public static byte[] ToString(TimingInfo[] timings)
        => Encoding.UTF8.GetBytes(string.Join("\n", timings.Select(i => string.Join(" ", i.BeginTimeNs, i.EndTimeNs, i.Phoneme))));

    public static byte[] ToString(PhraseInfo[] phrases)
        => Encoding.UTF8.GetBytes(string.Join("\n", phrases.Select(i => string.Join(" ", i.No, i.Time, (i.IsVoiced ? 0 : 1), i.IsVoiced))));
}
