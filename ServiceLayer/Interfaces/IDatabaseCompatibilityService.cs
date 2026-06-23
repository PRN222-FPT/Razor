namespace ServiceLayer.Interfaces;

public interface IDatabaseCompatibilityService
{
    Task ApplyAsync(CancellationToken cancellationToken = default);
}
