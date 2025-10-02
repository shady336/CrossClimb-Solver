using System;

namespace CrossclimbBackend.Utils
{
    public static class Words
    {
        public static int Hamming(string a, string b)
        {
            if (a is null || b is null) throw new ArgumentNullException();
            if (a.Length != b.Length) throw new ArgumentException("Lengths differ.");
            var d = 0;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) d++;
            return d;
        }

        public static bool MatchesPattern(string word, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (word.Length != pattern.Length) return false;
            for (int i = 0; i < word.Length; i++)
            {
                var p = pattern[i];
                if (p == '?') continue;
                if (char.ToUpperInvariant(word[i]) != char.ToUpperInvariant(p)) return false;
            }
            return true;
        }
    }
}