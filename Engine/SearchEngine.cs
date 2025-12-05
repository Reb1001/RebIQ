using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RebIQ.Models;

namespace RebIQ.Engine
{
    public class SearchEngine
    {
        private readonly VectorizationEngine _vectorEngine;
        private TrainingData? _trainingData;

        public SearchEngine()
        {
            _vectorEngine = new VectorizationEngine();
        }

        public void LoadTrainingData(TrainingData trainingData)
        {
            _trainingData = trainingData;
        }

        public SearchResponse Search(string query)
        {
            if (_trainingData == null)
            {
                throw new Exception("Model henüz eğitilmedi! Önce /api/train endpoint'ini çağırın.");
            }

            Console.WriteLine($"\n🔍 Arama başlıyor: \"{query}\"");
            
            var response = new SearchResponse
            {
                MatchScores = new Dictionary<string, double>()
            };

            // Sorguyu vektörize et
            var queryWords = TokenizeQuery(query);
            Console.WriteLine($"📝 Kelimeler: {string.Join(", ", queryWords)}");

            var wordVectors = new Dictionary<string, double>();
            foreach (var word in queryWords)
            {
                var vector = _vectorEngine.VectorizeWord(word);
                wordVectors[word] = vector;
                Console.WriteLine($"  {word} → {vector}");
            }

            // Field eşleştirmeleri bul
            var matchedFields = new List<string>();
            var filterConditions = new Dictionary<string, object>();

            Console.WriteLine("\n🎯 Alan eşleştirmeleri:");
            
            foreach (var word in queryWords)
            {
                foreach (var fieldVector in _trainingData.FieldVectors)
                {
                    // Direkt eşleşme
                    if (fieldVector.Value.Synonyms.Any(s => 
                        _vectorEngine.SimilarityScore(s, word) > 0.7))
                    {
                        if (!matchedFields.Contains(fieldVector.Key))
                        {
                            matchedFields.Add(fieldVector.Key);
                            Console.WriteLine($"  ✓ '{word}' → '{fieldVector.Key}' alanı");
                        }
                    }
                }
            }

            // Filtre değerlerini yakala (hem sayısal hem string)
            Console.WriteLine("\n🔢 Filtre koşulları:");
            foreach (var word in queryWords)
            {
                // Sayısal değerler
                if (int.TryParse(word, out int numValue))
                {
                    foreach (var field in matchedFields)
                    {
                        if (_trainingData.FieldVectors[field].DataType == "int" || 
                            _trainingData.FieldVectors[field].DataType == "double")
                        {
                            filterConditions[field] = numValue;
                            Console.WriteLine($"  ✓ {field} = {numValue}");
                        }
                    }
                }
                // String değerler - veri setindeki değerlerle eşleştir
                else if (_trainingData.WordVectors.ContainsKey(word.ToLowerInvariant()))
                {
                    var wordVector = _trainingData.WordVectors[word.ToLowerInvariant()];
                    
                    // Bu kelime hangi alana ait olabilir?
                    foreach (var item in _trainingData.OriginalData)
                    {
                        foreach (var field in item.Keys)
                        {
                            if (item[field] != null)
                            {
                                var valueStr = item[field].ToString().ToLowerInvariant();
                                if (_vectorEngine.SimilarityScore(valueStr, word) > 0.85)
                                {
                                    if (!filterConditions.ContainsKey(field))
                                    {
                                        filterConditions[field] = item[field];
                                        Console.WriteLine($"  ✓ {field} = {item[field]} ('{word}' ile eşleşti)");
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Sonuç alanlarını belirle - hangi field'lar gösterilmeli?
            Console.WriteLine("\n📋 Gösterilecek alanlar:");
            var selectFields = new List<string>();
            var usedFieldsForFilter = new HashSet<string>(filterConditions.Keys);

            // matchedFields'dan filtre olarak kullanılanları çıkar
            foreach (var field in matchedFields)
            {
                if (!usedFieldsForFilter.Contains(field))
                {
                    selectFields.Add(field);
                    Console.WriteLine($"  ✓ {field}");
                }
            }

            // Eğer hiç alan seçilmediyse (örnek: "ahmet" yazıp hiç field belirtmemişse), tüm alanları göster
            if (selectFields.Count == 0 && matchedFields.Count > 0)
            {
                selectFields = matchedFields;
                Console.WriteLine("  ℹ Tüm eşleşen alanlar gösteriliyor");
            }
            
            // Eğer hiç field eşleşmemişse, tüm kayıt göster
            if (selectFields.Count == 0)
            {
                Console.WriteLine("  ℹ Tüm kayıt gösteriliyor");
            }

            Console.WriteLine($"\n📊 Seçili alanlar: {string.Join(", ", selectFields)}");

            // Filtreleme yap
            var results = _trainingData.OriginalData.ToList();

            foreach (var condition in filterConditions)
            {
                results = results.Where(item =>
                {
                    if (item.ContainsKey(condition.Key))
                    {
                        var value = item[condition.Key];
                        if (value != null)
                        {
                            var itemValue = value.ToString().ToLowerInvariant();
                            var conditionValue = condition.Value.ToString().ToLowerInvariant();
                            
                            // Tam eşleşme veya benzerlik kontrolü
                            return itemValue == conditionValue || 
                                   _vectorEngine.SimilarityScore(itemValue, conditionValue) > 0.85;
                        }
                    }
                    return false;
                }).ToList();
            }

            Console.WriteLine($"✅ {results.Count} sonuç bulundu");

            // Sonuçları hazırla
            if (selectFields.Count > 0)
            {
                response.Results = results.Select(item =>
                {
                    var result = new Dictionary<string, object>();
                    foreach (var field in selectFields)
                    {
                        if (item.ContainsKey(field))
                        {
                            result[field] = item[field];
                        }
                    }
                    return result;
                }).ToList();
            }
            else
            {
                response.Results = results;
            }

            // Yorumlama oluştur
            var filterDesc = filterConditions.Count > 0 
                ? $"WHERE {string.Join(" AND ", filterConditions.Select(c => $"{c.Key}={c.Value}"))}" 
                : "";
            var selectDesc = selectFields.Count > 0 
                ? $"SELECT {string.Join(", ", selectFields)}" 
                : "SELECT *";

            response.Interpretation = $"{selectDesc} {filterDesc}".Trim();
            response.Action = $"{results.Count} kayıt bulundu";

            return response;
        }

        private List<string> TokenizeQuery(string query)
        {
            // Noktalama işaretlerini temizle
            var cleaned = Regex.Replace(query, @"[^\w\s]", " ");
            
            // Kelimelere ayır ve küçült
            var words = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(w => w.ToLowerInvariant())
                               .ToList();

            // Stop words'leri filtrele (opsiyonel)
            var stopWords = new[] { "bir", "bana", "tüm", "olan" };
            return words.Where(w => !stopWords.Contains(w)).ToList();
        }
    }
}
