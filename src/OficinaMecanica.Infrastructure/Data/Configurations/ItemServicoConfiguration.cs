using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class ItemServicoConfiguration : IEntityTypeConfiguration<ItemServico>
{
    public void Configure(EntityTypeBuilder<ItemServico> builder)
    {
        builder.ToTable("ItensServico");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ServicoId).IsRequired();
        builder.Property(i => i.NomeServico).IsRequired().HasMaxLength(200);
        builder.Property(i => i.PrecoUnitario).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();

        builder.Ignore(i => i.Subtotal);
        builder.Ignore(i => i.DomainEvents);
    }
}
