using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class ItemPecaConfiguration : IEntityTypeConfiguration<ItemPeca>
{
    public void Configure(EntityTypeBuilder<ItemPeca> builder)
    {
        builder.ToTable("ItensPeca");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.PecaId).IsRequired();
        builder.Property(i => i.NomePeca).IsRequired().HasMaxLength(200);
        builder.Property(i => i.PrecoUnitario).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();

        builder.Ignore(i => i.Subtotal);
        builder.Ignore(i => i.DomainEvents);
    }
}
