using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class VendaConfiguration : IEntityTypeConfiguration<Venda>
{
    public void Configure(EntityTypeBuilder<Venda> builder)
    {
        builder.ToTable("venda", t =>
        {
            t.HasCheckConstraint("CK_venda_total_centavos_positivo", "total_centavos > 0");
            t.HasCheckConstraint("CK_venda_status_concluida", "status = 'CONCLUIDA'");
        });
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.ComandaId).HasColumnName("comanda_id").IsRequired();
        builder.Property(v => v.Numero).HasColumnName("numero").IsRequired();
        builder.Property(v => v.TotalCentavos).HasColumnName("total_centavos").IsRequired();
        builder.Property(v => v.FinalizadaEm).HasColumnName("finalizada_em").IsRequired();
        builder.Property(v => v.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<StatusVenda>(s, ignoreCase: true))
            .HasDefaultValue(StatusVenda.Concluida)
            .IsRequired();

        builder.HasOne<Comanda>().WithMany().HasForeignKey(v => v.ComandaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(v => v.ComandaId).IsUnique();
        builder.HasIndex(v => v.Numero).IsUnique();
        builder.HasIndex(v => v.FinalizadaEm).HasDatabaseName("idx_venda_finalizada_em");
    }
}
