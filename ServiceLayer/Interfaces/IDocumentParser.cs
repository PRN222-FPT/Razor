using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IDocumentParser
{
    Task<ParsedDocumentContent> ParseAsync(
        string filePath,
        string fileType,
        CancellationToken cancellationToken = default);
}
