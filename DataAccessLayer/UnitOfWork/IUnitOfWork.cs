using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

public interface IUnitOfWork : IDisposable
{
    IRepository<Category> Categories { get; }

    IRepository<Product> Products { get; }

    Task<int> SaveChangesAsync();
}
