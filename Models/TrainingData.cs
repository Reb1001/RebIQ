using System.Collections.Generic;

namespace RebIQ.Models
{
    public class TrainingData
    {
        public Dictionary<string, FieldVector> FieldVectors { get; set; } = new();
        public Dictionary<string, double> WordVectors { get; set; } = new();
        public List<Dictionary<string, object>> OriginalData { get; set; } = new();
    }

    public class FieldVector
    {
        public string FieldName { get; set; } = string.Empty;
        public double Vector { get; set; }
        public List<string> Synonyms { get; set; } = new();
        public string DataType { get; set; } = string.Empty;
    }

    public class SearchRequest
    {
        public string Query { get; set; } = string.Empty;
    }

    public class SearchResponse
    {
        public string Interpretation { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public List<Dictionary<string, object>> Results { get; set; } = new();
        public Dictionary<string, double> MatchScores { get; set; } = new();
    }
}
