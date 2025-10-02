using CrossclimbBackend.Utils;
using Xunit;

namespace CrossclimbBackend.UnitTests
{
    public class WordsTests
    {
        [Fact]
        public void Hamming_SameWords_ShouldReturn0()
        {
            Assert.Equal(0, Words.Hamming("TEST","TEST"));
        }

        [Fact]
        public void Hamming_DifferentWords_ShouldReturnCount()
        {
            Assert.Equal(2, Words.Hamming("TEST","TOST"));
        }

        [Fact]
        public void MatchesPattern_Wildcards_ShouldMatch()
        {
            Assert.True(Words.MatchesPattern("COLD","C?LD"));
            Assert.False(Words.MatchesPattern("WARM","C?LD"));
        }
    }
}
