using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Diagnostics;
using Quark.Components;
using Quark.Data.Projects;
using Quark.Extensions;
using Quark.Models.Neutrino;
using Quark.Projects.Tracks;

namespace Quark.Utils;

/// <summary>
/// NEUTRINOに関するUtilクラス
/// </summary>
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
    public static (PhraseInfo[], T[]) ParsePhrases<T>(
        string phraseContent, TimingInfo[] timing,
        Func<int, int, int, string, PhraseStatus, T> func)
        where T : INeutrinoPhrase
    {
        var parsedPhrases = ParseWithRegex<PhraseInfo>(phraseContent, GetPhraseRegex(),
            r => new(
                r.GetValue<int>("no"),
                r.GetValue<int>("time"),
                r.GetValue<int>("flag") != 0,
                r.GetValue("label")))
            .ToArray();

        var phrases = new T[parsedPhrases.Count(p => p.IsVoiced)];

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

            phrases[destIdx++] = func(no, beginTime, endFrameIdx, label, PhraseStatus.WaitEstimate);
        }

        return (parsedPhrases, phrases);
    }

    public static byte[] ToString(TimingInfo[] timings)
        => Encoding.UTF8.GetBytes(string.Join("\n", timings.Select(i => string.Join(" ", i.BeginTimeNs, i.EndTimeNs, i.Phoneme))));

    public static byte[] ToString(PhraseInfo[] phrases)
        => Encoding.UTF8.GetBytes(string.Join("\n", phrases.Select(i => string.Join(" ", i.No, i.Time, (i.IsVoiced ? 0 : 1), i.Label))));

    /// <summary>標準出力される進捗情報をパースするための正規表現</summary>
    private static readonly Regex ProgressRegex = new(@"^.+Progress\s*=\s*(?<progress>\d+)\s*%.+$", RegexOptions.Compiled);

    /// <summary>
    /// NEUTRINOを実行する
    /// </summary>
    /// <param name="command">実行ファイル</param>
    /// <param name="args">コマンドライン引数</param>
    /// <param name="workdir">作業ディレクトリ</param>
    /// <param name="progress">進捗通知</param>
    /// <param name="cancellationToken">CancellationToken</param>
    /// <returns><see cref="Task"/></returns>
    /// <exception cref="NeutrinoExecuteException">実行失敗情報</exception>
    public static async Task Execute(string command, string? args = null, string? workdir = null, IProgress<ProgressReport>? progress = null, CancellationToken cancellationToken = default)
    {
        // 実行開始から終了までの一連の流れを特定するための識別子
        var guid = Guid.NewGuid().ToString("N");

        // 実行開始ログ
        Trace.WriteLine($"{guid}: === START NEUTRINO ===");
        Trace.WriteLine($"{guid}: Pwd: {workdir}");
        Trace.WriteLine($"{guid}: Execute: {command} {args}");

        bool isInitializing = true;

        // 出力情報を保持しておく
        StringBuilder output = new StringBuilder();

        try
        {
            await foreach (var line in ProcessX.StartAsync(command, args, workdir).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                Trace.WriteLine($"{guid}: {line}");
                output.AppendLine(line);

                double? progressValue = null;

                var m = ProgressRegex.Match(line);
                if (m.Success)
                {
                    isInitializing = false;
                    progressValue = double.Parse(m.Groups["progress"].Value);
                }

                progress?.Report(new(isInitializing ? ProgressReportType.Idertimate : ProgressReportType.InProgress, line, progressValue));
            }
        }
        catch (ProcessErrorException pee)
        {
            // 実行失敗時
            progress?.Report(new(ProgressReportType.Error, null, 100));

            throw new NeutrinoExecuteException(
                command, workdir, args, pee.ExitCode, output.ToString(), pee);
        }
        finally
        {
            // 実行終了ログ
            Trace.WriteLine($"{guid}: === END NEUTRINO ===");
        }
    }
}
