using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IDocumentChunker
{
    IReadOnlyList<DocumentChunkDraft> CreateChunks(ParsedDocumentContent content);
}
