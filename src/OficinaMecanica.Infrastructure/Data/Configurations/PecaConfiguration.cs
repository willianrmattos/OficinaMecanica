using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class PecaConfiguration : IEntityTypeConfiguration<Peca>
{
    public void Configure(EntityTypeBuilder<Peca> builder)
    {
        builder.ToTable("Pecas");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descricao).HasMaxLength(500);
        builder.Property(p => p.CodigoReferencia).HasMaxLength(50);
        builder.Property(p => p.PrecoUnitario).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.QuantidadeEstoque).IsRequired();
        builder.Property(p => p.EstoqueMinimo).IsRequired();
        builder.Property(p => p.Ativo).IsRequired();
        builder.Property(p => p.Version).IsConcurrencyToken();

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.EstoqueAbaixoDoMinimo);
    }
}
