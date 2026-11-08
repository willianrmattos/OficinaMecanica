using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Domain.Entities;

namespace OficinaMecanica.Infrastructure.Saga;

// A saga vive no MESMO DbContext da OS, que e o que permite saga, OS, estoque e outbox comitarem juntos. O mapa e aplicado
// por AppDbContext.OnModelCreating: o SagaClassMap do MassTransit nao e um IEntityTypeConfiguration, entao o
// ApplyConfigurationsFromAssembly nao o enxerga.
public class OrdemDeServicoSagaStateMap : SagaClassMap<OrdemDeServicoSagaState>
{
    protected override void Configure(EntityTypeBuilder<OrdemDeServicoSagaState> entity, ModelBuilder model)
    {
        entity.ToTable("OrdemDeServicoSagas");

        entity.Property(x => x.CurrentState).IsRequired().HasMaxLength(64);
        entity.Property(x => x.Numero).IsRequired().HasMaxLength(30);
        entity.Property(x => x.CancelamentoPorStaff).IsRequired().HasDefaultValue(false);
        entity.Property(x => x.MotivoCancelamento).HasMaxLength(OrdemDeServico.TamanhoMaximoDoMotivo);
        entity.Property(x => x.CriadaEm).IsRequired();
        entity.Property(x => x.AtualizadaEm).IsRequired();

        // O estado e o que as consultas operacionais filtram ("quantas OS presas em Estornando").
        entity.HasIndex(x => x.CurrentState);
    }
}
