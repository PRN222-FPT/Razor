using ServiceLayer.DTOs;

namespace ServiceLayer.Interfaces;

public interface IStudentDocumentService
{
    Task<StudentDocumentDetailsDto?> GetDocumentDetailsAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}
