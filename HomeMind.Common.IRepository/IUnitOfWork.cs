namespace HomeMind.Common.IRepository;

/// <summary>跨多张表写入时的事务边界。</summary>
public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
