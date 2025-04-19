using System.Linq.Expressions;
using Domain.Entities.Common;

namespace Domain.IRipository;

public interface IBaseRepository<T> where T : BaseEntity
{
    IQueryable<T> GetQueryable();
    Task<List<T>> GetAllAsync();
    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate);

    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>>? predicate, IOrderedQueryable<T> order = null,
        Expression<Func<T, object>>[]? includes = null);

    Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate, IOrderedQueryable<T> order = null,
        string? includes = null);

    Task<T?> GetByIdAsync(string id);
    Task<T?> GetByIdAsync(string id, Expression<Func<T, object>>[]? includes = null);
    Task<T?> GetByIdAsync(string id, string? includes = null);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}