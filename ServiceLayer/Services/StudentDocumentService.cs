using DataAccessLayer.Entities;
using DataAccessLayer.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using ServiceLayer.DTOs;
using ServiceLayer.Interfaces;

namespace ServiceLayer.Services;

public sealed class StudentDocumentService(IUnitOfWork unitOfWork) : IStudentDocumentService
{
    private const string PendingStatus = "pending";

    public async Task<StudentDocumentDetailsDto?> GetDocumentDetailsAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var document = await unitOfWork.Repository<Document>()
            .Query()
            .AsNoTracking()
            .Include(item => item.Subject)
            .Include(item => item.Chapter)
            .Include(item => item.Chunks)
            .Include(item => item.ProcessingJobs)
            .SingleOrDefaultAsync(item => item.DocumentId == documentId, cancellationToken);
        if (document is null)
        {
            return null;
        }

        var status = string.IsNullOrWhiteSpace(document.Status)
            ? PendingStatus
            : document.Status;
        var latestJob = document.ProcessingJobs
            .OrderByDescending(job => job.StartedAt)
            .ThenByDescending(job => job.FinishedAt)
            .FirstOrDefault();

        return new StudentDocumentDetailsDto(
            document.DocumentId,
            document.Title,
            document.Subject.SubjectCode,
            document.Subject.SubjectName,
            string.IsNullOrWhiteSpace(document.Chapter.ChapterTitle) ? "General" : document.Chapter.ChapterTitle,
            document.CreatedAt,
            status,
            document.FileType,
            latestJob?.ErrorMessage,
            document.Chunks
                .OrderBy(chunk => chunk.ChunkIndex)
                .Select(chunk => new StudentDocumentChunkDto(
                    chunk.ChunkId,
                    chunk.ChunkIndex,
                    chunk.Content,
                    chunk.CreatedAt))
                .ToList());
    }
}
