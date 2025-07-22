using System.Reflection.Emit;
using System.Text.Json;
using System.Text.Json.Serialization;
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

            modelbuilder.Property(x => x.SpamWords)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null));

            modelbuilder.Property(x => x.TargetGroups)
                  .HasColumnType("jsonb")
                  .HasConversion(
                      v => JsonSerializer.Serialize(v, new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                          DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                      }),
                      v => JsonSerializer.Deserialize<List<GroupReference>>(v, new JsonSerializerOptions
                      {
                          PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                      }));
        }
    }
}
