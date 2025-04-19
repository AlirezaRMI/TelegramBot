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

    [Required(ErrorMessage = "مبلغ تراکنش نمیتواند خالی باشد")]
    public long Price { get; set; }

    public bool IsConfirmed { get; set; }
    public new DateOnly CreateDate { get; set; }
    public TimeOnly CreatTime { get; set; }

    #region Relation

    [ForeignKey(nameof(UserId))] public string? UserId { get; set; }
    public User? User { get; set; }

    [EnumDataType(typeof(TransactionType), ErrorMessage = "نوع تراکنش معتبر نیست")]
    public TransactionType Type { get; set; }

    #endregion
}