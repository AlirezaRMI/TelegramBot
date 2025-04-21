using Domain.Entities.Common;
using Domain.Entities.Transaction;

public class User : BaseEntity
{
    public User()
    {
        
    }
    public  string? AccountCode { get; set; }

    public required string? UserName { get; set; }

    public DateTime CreateDate { get; set; }=DateTime.UtcNow;
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public long ChatId { get; set; }

    public bool IsDelete { get; set; }

    public bool IsActive { get; set; }
    

    #region Relation

    public ICollection<UserTransaction>? Transactions { get; set; }

    #endregion
    
    
  
   
  
  
    
}