using Quark.Models.Neutrino;
using Quark.Neutrino;
using Quark.Utils;

namespace Quark.Share.Test.Utils;

public class NeutrinoUtilTest
{
    [Fact]
    public void TestParseTiming2()
    {
        // 入力データ作成

        // フレーズ情報
        List<PhraseInfo> phrases = [
            new PhraseInfo(0, 0, false, [["sil", "sil", "pau"]]),
            new PhraseInfo(1, 6, true, [["a", "i", "sil"]]),
            new PhraseInfo(2, 12, true, [["k", "a", "k", "i"], ["k", "u", "sil"]]),
            new PhraseInfo(3, 26, false, [["pau"]])
        ];

        // タイミング情報
        //  <開始時間> <終了時間> <音素>
        var timing = """
            00000 10000 sil
            20000 30000 sil
            40000 50000 pau
            60000 70000 a
            80000 90000 i
            100000 110000 sil
            120000 130000 k
            140000 150000 a
            160000 170000 k
            180000 190000 i
            200000 210000 k
            220000 230000 u
            240000 250000 sil
            260000 270000 pau
            """;

        // 期待する戻り値
        List<PhonemeTiming> expected = [
            // phrase-0
            new(0, 0, "sil", 0),
            new(2, 2, "sil", 0),
            new(4, 4, "pau", 0),
            // phrase-1
            new(6, 6, "a", 1),
            new(8, 8, "i", 1),
            new(10, 10, "sil", 1),
            // phrase-2
            new(12, 12, "k", 2),
            new(14, 14, "a", 2),
            new(16, 16, "k", 2),
            new(18, 18, "i", 2),
            new(20, 20, "k", 2),
            new(22, 22, "u", 2),
            new(24, 24, "sil", 2),
            // phrase-3
            new(26, 26, "pau", 3)
        ];

        // 実行
        var actual = NeutrinoUtil.ParseTiming(phrases, timing);

        // 要素数の試験
        Assert.Equal(expected.Count, actual.Count);

        // リスト要素をすべて検査
        Assert.All(expected.Zip(actual), t =>
        {
            var (expected, actual) = t;

            Assert.Equal(expected.TimeMs, actual.TimeMs);
            Assert.Equal(expected.Phoneme, actual.Phoneme);
            Assert.Equal(expected.PhraseIndex, actual.PhraseIndex);
        });
    }
}
