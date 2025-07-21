using System.Reflection.Emit;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MpParserAPI.Models;

namespace MpParserAPI.Configurations
{
    public class ParserStateTableConfiguration : IEntityTypeConfiguration<ParserStateTable>
    {
        public void Configure(EntityTypeBuilder<ParserStateTable> modelbuilder)
        {
            modelbuilder.HasKey(x => x.Id);
            modelbuilder
                        .Property(x => x.SpamWords)
                        .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

            modelbuilder.Property(x => x.TargetGroups).HasConversion(
                p => JsonSerializer.Serialize(p, (JsonSerializerOptions?)null),
                p => JsonSerializer.Deserialize<List<GroupReference>>(p, (JsonSerializerOptions?)null));
        }
    }
}
