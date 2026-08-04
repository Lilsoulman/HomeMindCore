using HomeMind.Common.IRepository;
using Microsoft.EntityFrameworkCore.Storage;

namespace HomeMind.Common.Repository;

/// <summary>共用 DbContext 的工作单元实现，多个实体变更可一次提交。</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly HomeMindDbContext _db;
    public UnitOfWork(HomeMindDbContext db) => _db = db;
    public IRepository<TEntity> Repository<TEntity>() where TEntity : class => new EfRepository<TEntity>(_db);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
    public async Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default) => await _db.Database.BeginTransactionAsync(cancellationToken);
}
