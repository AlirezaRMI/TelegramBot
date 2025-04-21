using System.ComponentModel.DataAnnotations;

namespace Domain.Enum;

public enum TransactionStatus
{
    [Display(Name = "موفق")]
    Success,
    [Display(Name = "نا موفق")]
    Fail,
    [Display(Name = "لغو شده")]
    Canceled,
    [Display(Name = "خطا")]
    Error
}