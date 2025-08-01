using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MpParserAPI.Models;

namespace MpParserAPI.Configurations
{
    public class ParserLogConfiguration : IEntityTypeConfiguration<ParserLogs>
    {
        public void Configure(EntityTypeBuilder<ParserLogs> modelbuilder)
        {
            modelbuilder
                   .HasOne(p => p.TelegramUser)
                   .WithMany(u => u.ParserLogs)
                   .HasForeignKey(p => p.TelegramUserId)
                   .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
