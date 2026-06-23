namespace ServiceLayer.Interfaces;

public interface IDefaultAdminSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
