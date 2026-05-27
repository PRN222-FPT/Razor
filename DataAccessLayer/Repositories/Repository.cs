using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace DataAccessLayer.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
        => await DbSet.ToListAsync();

    public async Task<T?> GetByIdAsync(int id)
        => await DbSet.FindAsync(id);

    public async Task AddAsync(T entity)
        => await DbSet.AddAsync(entity);

    public void Update(T entity)
        => DbSet.Update(entity);

    public void Delete(T entity)
        => DbSet.Remove(entity);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await DbSet.Where(predicate).ToListAsync();

    public IQueryable<T> Query()
        => DbSet.AsQueryable();
}
