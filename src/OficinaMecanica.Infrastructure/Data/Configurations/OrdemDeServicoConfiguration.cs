using OficinaMecanica.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Infrastructure.Data.Configurations;

public class OrdemDeServicoConfiguration : IEntityTypeConfiguration<OrdemDeServico>
{
    public void Configure(EntityTypeBuilder<OrdemDeServico> builder)
    {
        builder.ToTable("OrdensDeServico");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Numero).IsRequired().HasMaxLength(30);
        builder.HasIndex(o => o.Numero).IsUnique();

        builder.Property(o => o.ClienteId).IsRequired();
        builder.Property(o => o.VeiculoId).IsRequired();
        builder.Property(o => o.Status).IsRequired();
        builder.Property(o => o.ValorTotal).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.Observacoes).HasMaxLength(1000);
        builder.Property(o => o.DataAbertura).IsRequired();
        builder.Property(o => o.Version).IsConcurrencyToken();

        // O default false e o que mantem as OS anteriores a saga fora dela quando a coluna for adicionada.
        builder.Property(o => o.ParticipaDaSaga).IsRequired().HasDefaultValue(false);
        builder.Property(o => o.EstoqueReservado).IsRequired().HasDefaultValue(false);
        builder.Property(o => o.PagamentoId);
        builder.Property(o => o.LinkPagamento).HasMaxLength(OrdemDeServico.TamanhoMaximoDoLinkDePagamento);
        builder.Property(o => o.PagamentoExpiraEm);
        builder.Property(o => o.MotivoCancelamento).HasMaxLength(OrdemDeServico.TamanhoMaximoDoMotivo);

        builder.HasMany(o => o.ItensServico)
            .WithOne()
            .HasForeignKey(i => i.OrdemDeServicoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.ItensPeca)
            .WithOne()
            .HasForeignKey(i => i.OrdemDeServicoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.HistoricoStatus)
            .WithOne()
            .HasForeignKey(h => h.OrdemDeServicoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(o => o.DomainEvents);
    }
}
