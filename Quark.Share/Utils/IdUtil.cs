namespace Quark.Utils;

/// <summary>
/// IDに関するユーティリティ
/// </summary>
public static class IdUtil
{
    private static Random _random = new();

    /// <summary>
    /// 新しいUUIDを生成する
    /// </summary>
    /// <returns>GUID</returns>
    public static string NewGuidId() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// ランダムな文字列を生成する
    /// </summary>
    /// <param name="length">文字数</param>
    /// <returns>ランダムな文字列</returns>
    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        char[] value = new char[length];

        for (int idx = 0; idx < value.Length; idx++)
            value[idx] = chars[_random.Next(chars.Length)];

        return new string(value);
    }
}
