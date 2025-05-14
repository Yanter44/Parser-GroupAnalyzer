using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Models;
namespace MpParserAPI.DbContext
{
    public class ParserDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public ParserDbContext()
        {
            
        }
        public ParserDbContext(DbContextOptions<ParserDbContext> options)
         : base(options) { }

        public DbSet<ParserLogs> ParserLogsTable { get; set; }
        public DbSet<TelegramUser> TelegramUsers { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
