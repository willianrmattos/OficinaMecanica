using OficinaMecanica.Domain.Entities;
using OficinaMecanica.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nome)
            .IsRequired()
            .HasMaxLength(200);

        builder.OwnsOne(c => c.Documento, d =>
        {
            d.Property(p => p.Numero).HasColumnName("Documento").IsRequired().HasMaxLength(14);
            d.Property(p => p.Tipo).HasColumnName("TipoDocumento").IsRequired();
            d.HasIndex(p => p.Numero).IsUnique();
        });

        builder.Property(c => c.Email).HasMaxLength(200);
        builder.Property(c => c.Telefone).HasMaxLength(20);
        builder.Property(c => c.DataCadastro).IsRequired();

        builder.HasMany(c => c.Veiculos)
            .WithOne()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(c => c.Version).IsConcurrencyToken();

        builder.Ignore(c => c.DomainEvents);
    }
}
