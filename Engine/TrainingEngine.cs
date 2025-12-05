using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RebIQ.Models;

namespace RebIQ.Engine
{
    public class TrainingEngine
    {
        private readonly VectorizationEngine _vectorEngine;

        public TrainingEngine()
        {
            _vectorEngine = new VectorizationEngine();
        }

        public TrainingData Train(string jsonData)
        {
            Console.WriteLine("🎓 Eğitim başlıyor...");
            
            var trainingData = new TrainingData();
            
            // JSON'u parse et
            var parsedJson = JsonConvert.DeserializeObject<JToken>(jsonData);
            
            if (parsedJson == null)
            {
                throw new Exception("JSON verisi boş veya geçersiz!");
            }

            // Nested JSON'u düzleştir
            var dataArray = FlattenJson(parsedJson);
            
            if (dataArray.Count == 0)
            {
                throw new Exception("JSON verisi boş veya geçersiz!");
            }

            trainingData.OriginalData = dataArray;
            Console.WriteLine($"📊 {dataArray.Count} kayıt yüklendi");

            // Her field için vektör oluştur
            var allFields = new HashSet<string>();
            foreach (var item in dataArray)
            {
                foreach (var key in item.Keys)
                {
                    allFields.Add(key);
                }
            }

            Console.WriteLine($"🔍 {allFields.Count} alan tespit edildi");

            foreach (var field in allFields)
            {
                var vector = _vectorEngine.VectorizeWord(field);
                var dataType = DetermineDataType(dataArray, field);
                
                trainingData.FieldVectors[field] = new FieldVector
                {
                    FieldName = field,
                    Vector = vector,
                    DataType = dataType,
                    Synonyms = GenerateSynonyms(field)
                };

                Console.WriteLine($"  ✓ {field} → {vector} (Tip: {dataType})");
            }

            // Tüm benzersiz kelimeleri vektörize et
            var allWords = new HashSet<string>();
            foreach (var item in dataArray)
            {
                foreach (var value in item.Values)
                {
                    if (value != null)
                    {
                        var words = value.ToString()!.Split(new[] { ' ', ',', '.', ';', ':', '-' }, 
                            StringSplitOptions.RemoveEmptyEntries);
                        foreach (var word in words)
                        {
                            allWords.Add(word.ToLowerInvariant());
                        }
                    }
                }
            }

            foreach (var word in allWords)
            {
                trainingData.WordVectors[word] = _vectorEngine.VectorizeWord(word);
            }

            Console.WriteLine($"📝 {allWords.Count} kelime vektörize edildi");
            Console.WriteLine("✅ Eğitim tamamlandı!");

            return trainingData;
        }

        private List<Dictionary<string, object>> FlattenJson(JToken token)
        {
            var result = new List<Dictionary<string, object>>();

            if (token is JArray array)
            {
                // Eğer array ise, her elemanı işle
                foreach (var item in array)
                {
                    if (item is JObject obj)
                    {
                        result.AddRange(FlattenObject(obj));
                    }
                    else
                    {
                        // Primitive array ise, direkt obje olarak ekle
                        var dict = new Dictionary<string, object> { { "value", item.ToString() } };
                        result.Add(dict);
                    }
                }
            }
            else if (token is JObject rootObj)
            {
                // Eğer root bir object ise
                result.AddRange(FlattenObject(rootObj));
            }

            return result;
        }

        private List<Dictionary<string, object>> FlattenObject(JObject obj, string prefix = "")
        {
            var result = new List<Dictionary<string, object>>();
            var currentDict = new Dictionary<string, object>();
            var hasNestedArray = false;

            foreach (var prop in obj.Properties())
            {
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}_{prop.Name}";

                if (prop.Value is JArray nestedArray)
                {
                    // Nested array bulundu - bu array'in her elemanını ayrı kayıt olarak işle
                    hasNestedArray = true;
                    foreach (var item in nestedArray)
                    {
                        if (item is JObject nestedObj)
                        {
                            var flatDict = new Dictionary<string, object>();
                            FlattenObjectProperties(nestedObj, flatDict, prop.Name);
                            result.Add(flatDict);
                        }
                    }
                }
                else if (prop.Value is JObject nestedObj)
                {
                    // Nested object - özelliklerini düzleştir
                    FlattenObjectProperties(nestedObj, currentDict, key);
                }
                else
                {
                    // Primitive değer
                    currentDict[key] = prop.Value.ToString();
                }
            }

            // Eğer nested array yoksa ve currentDict doluysa, onu ekle
            if (!hasNestedArray && currentDict.Count > 0)
            {
                result.Add(currentDict);
            }

            return result;
        }

        private void FlattenObjectProperties(JObject obj, Dictionary<string, object> target, string prefix = "")
        {
            foreach (var prop in obj.Properties())
            {
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}_{prop.Name}";

                if (prop.Value is JObject nestedObj)
                {
                    FlattenObjectProperties(nestedObj, target, key);
                }
                else if (prop.Value is JArray)
                {
                    // Array'i string olarak sakla
                    target[key] = prop.Value.ToString();
                }
                else
                {
                    target[key] = prop.Value.ToString();
                }
            }
        }

        private string DetermineDataType(List<Dictionary<string, object>> data, string field)
        {
            var sample = data.FirstOrDefault(d => d.ContainsKey(field))?[field];
            
            if (sample == null) return "string";
            
            if (int.TryParse(sample.ToString(), out _)) return "int";
            if (double.TryParse(sample.ToString(), out _)) return "double";
            if (bool.TryParse(sample.ToString(), out _)) return "bool";
            if (DateTime.TryParse(sample.ToString(), out _)) return "datetime";
            
            return "string";
        }

        private List<string> GenerateSynonyms(string field)
        {
            var synonyms = new List<string> { field.ToLowerInvariant() };
            
            // Türkçe alan adları için eşanlamlılar
            var synonymMap = new Dictionary<string, string[]>
            {
                { "kod", new[] { "kodu", "kodlar", "kodları", "code", "id", "köd", "ködları" } },
                { "yaş", new[] { "yas", "yaşı", "yası", "age", "yash" } },
                { "ad", new[] { "isim", "name", "adı", "adi" } },
                { "soyad", new[] { "soyisim", "surname", "soyadı", "soyadi" } },
                { "email", new[] { "eposta", "e-posta", "mail", "e-mail" } },
                { "telefon", new[] { "tel", "phone", "gsm", "telefonu" } },
                { "adres", new[] { "address", "adresi" } },
                { "şehir", new[] { "sehir", "city", "il" } },
                { "ülke", new[] { "ulke", "country" } }
            };

            var fieldLower = field.ToLowerInvariant();
            foreach (var entry in synonymMap)
            {
                if (fieldLower.Contains(entry.Key) || entry.Key.Contains(fieldLower))
                {
                    synonyms.AddRange(entry.Value);
                }
            }

            return synonyms.Distinct().ToList();
        }
    }
}
