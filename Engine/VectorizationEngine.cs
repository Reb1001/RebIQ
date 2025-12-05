using System;
using System.Linq;
using System.Text;

namespace RebIQ.Engine
{
    public class VectorizationEngine
    {
        // Basit ama etkili hash-based vektörizasyon
        public double VectorizeWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
                return 0;

            word = word.ToLowerInvariant().Trim();
            
            // Türkçe karakterleri normalize et
            word = NormalizeTurkish(word);
            
            long hash = 0;
            foreach (char c in word)
            {
                hash = ((hash << 5) - hash) + c;
            }
            
            return Math.Abs(hash % 10000000000); // 10 milyar modulo
        }

        // Fuzzy matching için Levenshtein distance
        public int LevenshteinDistance(string s1, string s2)
        {
            s1 = NormalizeTurkish(s1.ToLowerInvariant());
            s2 = NormalizeTurkish(s2.ToLowerInvariant());

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;
            for (int j = 0; j <= s2.Length; j++)
                d[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[s1.Length, s2.Length];
        }

        // Benzerlik skoru (0-1 arası)
        public double SimilarityScore(string s1, string s2)
        {
            if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2))
                return 0;

            int distance = LevenshteinDistance(s1, s2);
            int maxLength = Math.Max(s1.Length, s2.Length);
            
            if (maxLength == 0)
                return 1;

            return 1.0 - ((double)distance / maxLength);
        }

        // Türkçe karakter normalizasyonu
        private string NormalizeTurkish(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.Replace('ı', 'i')
                       .Replace('İ', 'i')
                       .Replace('ş', 's')
                       .Replace('Ş', 's')
                       .Replace('ğ', 'g')
                       .Replace('Ğ', 'g')
                       .Replace('ü', 'u')
                       .Replace('Ü', 'u')
                       .Replace('ö', 'o')
                       .Replace('Ö', 'o')
                       .Replace('ç', 'c')
                       .Replace('Ç', 'c');

            return text;
        }

        // İki vektör arasındaki benzerlik
        public double VectorSimilarity(double v1, double v2)
        {
            if (v1 == v2) return 1.0;
            
            double max = Math.Max(v1, v2);
            double min = Math.Min(v1, v2);
            
            if (max == 0) return 0;
            
            return min / max;
        }
    }
}
