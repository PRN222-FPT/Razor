namespace ServiceLayer.Interfaces;

public interface IDefaultStudentSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
