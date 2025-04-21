using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Entities.Common;
using Domain.Enum;


namespace Domain.Entities.Transaction;

[Table("Transactions")]
public class UserTransaction : BaseEntity
{
    public string? Description { get; set; }

    public TransactionStatus Status { get; set; }

    public TransactionType TransactionType { get; set; }
    public long Price { get; set; }

    public bool IsConfirmed { get; set; }
    
    public new DateTime CreateDate { get; set; }
    public long ChatId { get; set; }


    public UserTransaction()
    {
    }

    #region Relation

    [ForeignKey(nameof(UserId))] public string? UserId { get; set; }
    public User? User { get; set; }
    #endregion
}