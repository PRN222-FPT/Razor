using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    private readonly Dictionary<Type, object> _repositories = [];

    public IRepository<TEntity> Repository<TEntity>()
        where TEntity : class
    {
        var entityType = typeof(TEntity);

        if (_repositories.TryGetValue(entityType, out var repository))
        {
            return (IRepository<TEntity>)repository;
        }

        var newRepository = new Repository<TEntity>(dbContext);
        _repositories[entityType] = newRepository;

        return newRepository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return dbContext.DisposeAsync();
    }
}
