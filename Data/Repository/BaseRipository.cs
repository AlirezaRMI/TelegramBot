using System.Linq.Expressions;
using Data.Context;
using Domain.Entities.Common;
using Domain.IRipository;
using Microsoft.EntityFrameworkCore;

namespace Data.Repository;

public class BaseRipository<T>(BotContext context) : IBaseRepository<T> where T : BaseEntity
{
    public IQueryable<T> GetQueryable()
    {
        return context.Set<T>().AsQueryable();
    }

    public async Task<List<T>> GetAllAsync()
    {
        return await context.Set<T>().ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
    {
        return await context.Set<T>().Where(predicate).ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate,
        IOrderedQueryable<T> order = null, Expression<Func<T, object>>[]? includes = null)
    {
        IQueryable<T> query = context.Set<T>();
        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        query = query.Where(predicate);
        if (order != null)
        {
            return await order.ToListAsync();
        }

        return await query.ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate,
        IOrderedQueryable<T> order = null, string? includes = null)
    {
        IQueryable<T> query = context.Set<T>();
        if (!string.IsNullOrWhiteSpace(includes))
        {
            var navigationProperties = includes.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var include in navigationProperties)
            {
                query = query.Include(include.Trim());
            }
        }

        query = query.Where(predicate);

        return order != null
            ? await order.ToListAsync()
            : await query.ToListAsync();
    }

    public async Task<IReadOnlyList<T>> GetAllAsync(Expression<Func<T, bool>> predicate, IOrderedQueryable<T> order)
    {
        return await context.Set<T>().Where(predicate).ToListAsync();
    }

    public async Task<T?> GetByIdAsync(string id)
    {
        return await context.Set<T>().FindAsync(id);
    }

    public async Task<T?> GetByIdAsync(string id, Expression<Func<T, object>>[]? includes = null)
    {
        IQueryable<T> query = context.Set<T>();
        if (includes != null)
        {
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
        }

        return await query.FirstOrDefaultAsync(e =>
            EF.Property<string>(e, "Id") == id);
    }

    public async Task<T?> GetByIdAsync(string id, string? includes = null)
    {
        return await context.Set<T>().Include(includes).SingleOrDefaultAsync(x => x.Id == id);
    }

    public async Task AddAsync(T entity)
    {
        await context.Set<T>().AddAsync(entity);
        await context.SaveChangesAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        context.Set<T>().Remove(entity);
        await context.SaveChangesAsync();
    }
}