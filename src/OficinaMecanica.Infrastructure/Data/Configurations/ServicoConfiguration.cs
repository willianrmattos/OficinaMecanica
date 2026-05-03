using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class ServicoConfiguration : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> builder)
    {
        builder.ToTable("Servicos");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nome).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Descricao).HasMaxLength(500);
        builder.Property(s => s.Preco).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(s => s.TempoEstimadoMinutos).IsRequired();
        builder.Property(s => s.Ativo).IsRequired();
        builder.Property(s => s.Version).IsConcurrencyToken();

        builder.Ignore(s => s.DomainEvents);
    }
}
