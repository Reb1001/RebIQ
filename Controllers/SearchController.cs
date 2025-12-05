using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RebIQ.Engine;
using RebIQ.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RebIQ.Controllers
{
    [ApiController]
    [Route("api")]
    public class SearchController : ControllerBase
    {
        private static TrainingEngine _trainingEngine = new TrainingEngine();
        private static SearchEngine _searchEngine = new SearchEngine();
        private static Dictionary<string, TrainingData> _sessionTrainingData = new Dictionary<string, TrainingData>();
        private const string TRAINED_DATA_FILE = "trained_vectors.json";

        [HttpPost("train")]
        public IActionResult Train([FromBody] object jsonData, [FromQuery] string? sessionId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    sessionId = Guid.NewGuid().ToString();

                var jsonString = JsonConvert.SerializeObject(jsonData);
                
                // Eğitimi gerçekleştir
                var trainingData = _trainingEngine.Train(jsonString);
                
                // Session'a kaydet
                _sessionTrainingData[sessionId] = trainingData;
                
                // Eğitilmiş veriyi kaydet
                var trainedJson = JsonConvert.SerializeObject(trainingData, Formatting.None);
                System.IO.File.WriteAllText(TRAINED_DATA_FILE, trainedJson, Encoding.UTF8);
                
                // Search engine'e yükle
                _searchEngine.LoadTrainingData(trainingData);

                return Ok(new
                {
                    success = true,
                    sessionId = sessionId,
                    message = "Model başarıyla eğitildi!",
                    stats = new
                    {
                        recordCount = trainingData.OriginalData.Count,
                        fieldCount = trainingData.FieldVectors.Count,
                        wordCount = trainingData.WordVectors.Count,
                        savedTo = TRAINED_DATA_FILE
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("search")]
        public IActionResult Search([FromBody] SearchRequest request, [FromQuery] string? sessionId = null)
        {
            try
            {
                TrainingData? trainingData = null;

                // Session'dan al
                if (!string.IsNullOrEmpty(sessionId) && _sessionTrainingData.ContainsKey(sessionId))
                {
                    trainingData = _sessionTrainingData[sessionId];
                }
                // Eğer session'da yoksa, dosyadan yükle
                else if (System.IO.File.Exists(TRAINED_DATA_FILE))
                {
                    var trainedJson = System.IO.File.ReadAllText(TRAINED_DATA_FILE, Encoding.UTF8);
                    trainingData = JsonConvert.DeserializeObject<TrainingData>(trainedJson);
                }

                if (trainingData == null)
                {
                    return BadRequest(new { success = false, error = "Eğitim verisi bulunamadı. Lütfen önce modeli eğitin." });
                }

                _searchEngine.LoadTrainingData(trainingData);
                var result = _searchEngine.Search(request.Query);

                return Ok(new
                {
                    success = true,
                    sessionId = sessionId,
                    query = request.Query,
                    interpretation = result.Interpretation,
                    action = result.Action,
                    resultCount = result.Results.Count,
                    results = result.Results
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("check-training")]
        public IActionResult CheckTraining()
        {
            try
            {
                if (System.IO.File.Exists(TRAINED_DATA_FILE))
                {
                    var trainedJson = System.IO.File.ReadAllText(TRAINED_DATA_FILE, Encoding.UTF8);
                    var trainingData = JsonConvert.DeserializeObject<TrainingData>(trainedJson);
                    
                    if (trainingData != null && trainingData.OriginalData != null)
                    {
                        // Eğitilmiş veriyi search engine'e yükle
                        _searchEngine.LoadTrainingData(trainingData);
                        
                        return Ok(new
                        {
                            isTrainingAvailable = true,
                            recordCount = trainingData.OriginalData.Count,
                            fieldCount = trainingData.FieldVectors.Count
                        });
                    }
                }
                
                return Ok(new { isTrainingAvailable = false, recordCount = 0, fieldCount = 0 });
            }
            catch (Exception ex)
            {
                return Ok(new { isTrainingAvailable = false, recordCount = 0, fieldCount = 0, error = ex.Message });
            }
        }

        [HttpGet("status")]
        public IActionResult Status([FromQuery] string? sessionId = null)
        {
            TrainingData? trainingData = null;

            if (!string.IsNullOrEmpty(sessionId) && _sessionTrainingData.ContainsKey(sessionId))
            {
                trainingData = _sessionTrainingData[sessionId];
            }

            return Ok(new
            {
                trained = trainingData != null || System.IO.File.Exists(TRAINED_DATA_FILE),
                recordCount = trainingData?.OriginalData.Count ?? 0,
                fieldCount = trainingData?.FieldVectors.Count ?? 0,
                trainedFile = System.IO.File.Exists(TRAINED_DATA_FILE),
                activeSessions = _sessionTrainingData.Count
            });
        }
    }
}
