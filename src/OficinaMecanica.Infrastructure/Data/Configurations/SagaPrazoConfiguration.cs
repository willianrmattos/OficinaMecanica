using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Infrastructure.Saga;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class SagaPrazoConfiguration : IEntityTypeConfiguration<SagaPrazo>
{
    // O sweeper cita este nome na dica INDEX(...) do SQL cru: as duas pontas precisam concordar.
    public const string IndiceDoSweeper = "IX_SagaPrazos_Processado_ExpiraEm";

    public void Configure(EntityTypeBuilder<SagaPrazo> builder)
    {
        builder.ToTable("SagaPrazos");
        builder.HasKey(p => p.PrazoId);
        builder.Property(p => p.PrazoId).ValueGeneratedNever();

        builder.Property(p => p.CorrelationId).IsRequired();

        // Texto e nao numero: o valor do enum nunca muda de significado se alguem reordenar o tipo, e a tabela
        // continua legivel para quem investiga uma saga presa.
        builder.Property(p => p.Tipo).IsRequired().HasMaxLength(20).HasConversion<string>();
        builder.Property(p => p.ExpiraEm).IsRequired();
        builder.Property(p => p.Processado).IsRequired().HasDefaultValue(false);

        builder.HasIndex(p => new { p.Processado, p.ExpiraEm }).HasDatabaseName(IndiceDoSweeper);
    }
}
