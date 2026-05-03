using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class HistoricoStatusConfiguration : IEntityTypeConfiguration<HistoricoStatus>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus> builder)
    {
        builder.ToTable("HistoricoStatus");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Status).IsRequired();
        builder.Property(h => h.Observacao).IsRequired().HasMaxLength(500);
        builder.Property(h => h.DataAlteracao).IsRequired();

        builder.Ignore(h => h.DomainEvents);
    }
}
