using System.Transactions;
using Domain.Entities.Transaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data.Context;

public class BotContext : DbContext
{
    public class BotContextDesignTimeFactory : IDesignTimeDbContextFactory<BotContext>
    {
        public BotContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<BotContext>();
            optionsBuilder.UseSqlServer(
                "Data Source=localhost;Initial Catalog=TelegramBot_DB;Integrated Security=True;TrustServerCertificate=True");

            return new BotContext(optionsBuilder.Options);
        }
    }

    public BotContext(DbContextOptions<BotContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Data Source=localhost;Initial Catalog=TelegramBot_DB;Integrated Security=True;TrustServerCertificate=True");
        }
    }

    public DbSet<UserTransaction> Transactions { get; set; }
    public DbSet<User> Users { get; set; }
}