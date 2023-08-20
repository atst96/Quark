using Quark.Models.Neutrino;
using System.Text;
using Quark.Extensions;
using System.Text.RegularExpressions;
using Quark.Data;

namespace Quark.Utils;

/// <summary>
/// NEUTRINOの推論モデルに関するUtilクラス
/// </summary>
public static class NeutrinoModelUtil
{
    /// <summary>
    /// モデル名抽出用の正規表現
    /// </summary>
    private static Regex ModelNameRegex = new(@"#\s*.+-\s*(?:(.+)(?:\s*（.+）)\s*|(.+)\s*)$", RegexOptions.Compiled);

    /// <summary>
    /// モデル情報を取得する
    /// </summary>
    /// <param name="path">モデル格納先のディレクトリ</param>
    /// <returns>モデル情報</returns>
    public static IList<ModelInfo> GetModelsV1(string path, ModelType modelType)
        => GetModelDirectories(path)
            .Select(p => new ModelInfo(Path.GetFileName(p), GetModelNameV1(p), p, modelType))
            .OrderBy(i => i.Name, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>
    /// モデル情報を取得する
    /// </summary>
    /// <param name="path">モデル格納先のディレクトリ</param>
    /// <returns>モデル情報</returns>
    public static IList<ModelInfo> GetModelsV2(string path, ModelType modelType)
        => GetModelDirectories(path)
            .Select(p => new ModelInfo(Path.GetFileName(p), GetModelNameV2(p), p, modelType))
            .OrderBy(i => i.Name, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>
    /// バージョン情報のファイル名
    /// </summary>
    private const string VersionInfoFileNameV1 = "VERSION_INFO.txt";
    private const string VersionInfoFileNameV2 = "version_information.txt";

    /// <summary>
    /// モデルのパスからモデル名を取得する
    /// </summary>
    /// <param name="modelPath"></param>
    /// <returns></returns>
    private static string GetModelNameV1(string modelPath)
    {
        var file = new FileInfo(Path.Combine(modelPath, VersionInfoFileNameV1));
        if (file.Exists)
        {
            using var reader = new StreamReader(file.OpenRead(), Encoding.UTF8);
            foreach (var line in reader.EnumerateLines())
            {
                // 正規表現に一致する文字列があれば抽出して返却する
                var m = ModelNameRegex.Match(line);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        // バージョン情報のファイルがない、もしくはパターンに
        // 一致する行がなければモデルのフォルダ名を返す
        return Path.GetFileName(modelPath);
    }

    /// <summary>
    /// モデルのパスからモデル名を取得する
    /// </summary>
    /// <param name="modelPath"></param>
    /// <returns></returns>
    private static string GetModelNameV2(string modelPath)
    {
        var file = new FileInfo(Path.Combine(modelPath, VersionInfoFileNameV2));
        if (file.Exists)
        {
            using var reader = new StreamReader(file.OpenRead(), Encoding.UTF8);
            foreach (var line in reader.EnumerateLines())
            {
                // 正規表現に一致する文字列があれば抽出して返却する
                var m = ModelNameRegex.Match(line);
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        // バージョン情報のファイルがない、もしくはパターンに
        // 一致する行がなければモデルのフォルダ名を返す
        return Path.GetFileName(modelPath);
    }

    /// <summary>
    /// モデルがあるディレクトリを取得する
    /// </summary>
    /// <param name="path">ディレクトリのパス</param>
    /// <returns></returns>
    private static string[] GetModelDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (DirectoryNotFoundException)
        {
            return Array.Empty<string>();
        }
    }
}
