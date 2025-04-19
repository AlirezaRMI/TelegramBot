using System.Transactions;
using Domain.Entities.Transaction;
using Microsoft.EntityFrameworkCore;

namespace Data.Context;

public class BotContext : DbContext
{
    public BotContext(DbContextOptions<BotContext> options) : base(options)
    {
    }

    public DbSet<UserTransaction> Transactions { get; set; }
    public DbSet<User> Users { get; set; }
}