using System.ComponentModel.DataAnnotations;

namespace Domain.Enum;

public enum TransactionType
{
    [Display(Name = "واریز")] Increase = 1,
    [Display(Name = "برداشت")] Decrease = -1
}