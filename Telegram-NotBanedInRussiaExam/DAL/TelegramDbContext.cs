using Microsoft.EntityFrameworkCore;
using Telegram_NotBanedInRussiaExam.Enteties;

namespace Telegram_NotBanedInRussiaExam.DAL;

public class TelegramDbContext:DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(@"Data Source=DESKTOP-LJTCG3Q\SQLEXPRESS;Initial Catalog=Telegram;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;");
    }
    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
}
