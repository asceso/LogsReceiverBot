using Microsoft.EntityFrameworkCore;
using Models.Database;

namespace DataAdapter
{
    public class DataContext : DbContext
    {
        public DbSet<UserModel> Users { get; set; }
        public DbSet<LogModel> Logs { get; set; }
        public DbSet<ManualCheckModel> ManualChecks { get; set; }
        public DbSet<PayoutModel> Payouts { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseMySql(
                "server=localhost;user=root;password=admin;database=bot_receiver_db;",
                new MySqlServerVersion(new Version(8, 0, 20))
            );
    }
}