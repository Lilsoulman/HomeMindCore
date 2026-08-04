using System.Linq.Expressions;

namespace HomeMind.Common.IRepository;

/// <summary>所有表的通用仓储约定，屏蔽 ORM 的数据访问细节。</summary>
public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    ValueTask<TEntity?> FindAsync(params object[] keyValues);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Remove(TEntity entity);
}
