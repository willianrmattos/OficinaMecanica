using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class VeiculoConfiguration : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.ToTable("Veiculos");
        builder.HasKey(v => v.Id);

        builder.OwnsOne(v => v.Placa, p =>
        {
            p.Property(x => x.Valor).HasColumnName("Placa").IsRequired().HasMaxLength(7);
            p.HasIndex(x => x.Valor).IsUnique();
        });

        builder.Property(v => v.Marca).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Modelo).IsRequired().HasMaxLength(100);
        builder.Property(v => v.Ano).IsRequired();
        builder.Property(v => v.ClienteId).IsRequired();

        builder.Ignore(v => v.DomainEvents);
    }
}
