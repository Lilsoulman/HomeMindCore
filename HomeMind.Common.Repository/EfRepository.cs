using HomeMind.Common.IRepository;
using Microsoft.EntityFrameworkCore;

namespace HomeMind.Common.Repository;

/// <summary>EF Core 通用仓储实现，控制器不得直接依赖该实现。</summary>
public sealed class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly HomeMindDbContext _db;
    public EfRepository(HomeMindDbContext db) => _db = db;
    public IQueryable<TEntity> Query() => _db.Set<TEntity>();
    public async ValueTask<TEntity?> FindAsync(params object[] keyValues) => await _db.Set<TEntity>().FindAsync(keyValues);
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) => _db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
    public void Remove(TEntity entity) => _db.Set<TEntity>().Remove(entity);
}
