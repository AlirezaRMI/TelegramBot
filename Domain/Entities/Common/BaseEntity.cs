using Domain.IRipository;

namespace Domain.Entities.Common;

public class BaseEntity<T>
{
    public T? Id { get; set; }

    public DateOnly CreateDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public TimeOnly CreateTime { get; set; } = TimeOnly.FromDateTime(DateTime.Now);
}

public class BaseEntity : BaseEntity<string>
{
    protected BaseEntity()
    {
        Id = Guid.NewGuid().ToString("N");
    }
};