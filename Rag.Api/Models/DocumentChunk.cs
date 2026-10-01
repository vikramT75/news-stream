using System;

namespace Rag.Api.Models
{
    public class DocumentChunk
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public string Embedding { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime IngestionTime { get; set; }
    }
}
