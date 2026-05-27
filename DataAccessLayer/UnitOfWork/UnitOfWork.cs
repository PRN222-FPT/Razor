using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;

namespace DataAccessLayer.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IRepository<Category>? _categories;
    private IRepository<Product>? _products;
    private bool _disposed;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IRepository<Category> Categories
        => _categories ??= new Repository<Category>(_context);

    public IRepository<Product> Products
        => _products ??= new Repository<Product>(_context);

    public async Task<int> SaveChangesAsync()
        => await _context.SaveChangesAsync();

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _context.Dispose();
        }

        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
