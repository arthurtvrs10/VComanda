using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ItemComandaConfiguration : IEntityTypeConfiguration<ItemComanda>
{
    public void Configure(EntityTypeBuilder<ItemComanda> builder)
    {
        builder.ToTable("item_comanda", t =>
        {
            t.HasCheckConstraint("CK_item_comanda_preco_unitario_positivo", "preco_unitario_centavos > 0");
            t.HasCheckConstraint("CK_item_comanda_quantidade_positiva", "quantidade > 0");
            t.HasCheckConstraint("CK_item_comanda_subtotal_positivo", "subtotal_centavos > 0");
            t.HasCheckConstraint("CK_item_comanda_subtotal_igual_preco_vezes_quantidade", "subtotal_centavos = preco_unitario_centavos * quantidade");
        });

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ComandaId).HasColumnName("comanda_id").IsRequired();
        builder.Property(i => i.ProdutoId).HasColumnName("produto_id").IsRequired();
        builder.Property(i => i.NomeProduto).HasColumnName("nome_produto").IsRequired();
        builder.Property(i => i.PrecoUnitarioCentavos).HasColumnName("preco_unitario_centavos").IsRequired();
        builder.Property(i => i.Quantidade).HasColumnName("quantidade").IsRequired();
        builder.Property(i => i.SubtotalCentavos).HasColumnName("subtotal_centavos").IsRequired();
        builder.Property(i => i.Observacao).HasColumnName("observacao");
        builder.Property(i => i.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(i => i.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasOne<Comanda>().WithMany().HasForeignKey(i => i.ComandaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Produto>().WithMany().HasForeignKey(i => i.ProdutoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(i => i.ComandaId).HasDatabaseName("idx_item_comanda_comanda");
    }
}
