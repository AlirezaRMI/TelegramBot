using Domain.Entities.Common;
using Domain.Entities.Transaction;

public class User : BaseEntity
{
    public required string AccountCode { get; set; }

    public required string UserName { get; set; }

    public string? Address { get; set; }

    public bool IsDelete { get; set; }

    public bool IsActive { get; set; }

    public required string Password { get; set; }

    #region Relation

    public ICollection<UserTransaction>? Transactions { get; set; }

    #endregion
}