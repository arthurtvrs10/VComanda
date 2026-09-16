using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produto", t =>
        {
            t.HasCheckConstraint("CK_produto_preco_centavos_positivo", "preco_centavos > 0");
            t.HasCheckConstraint("CK_produto_ativo", "ativo IN (0, 1)");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CategoriaId).HasColumnName("categoria_id").IsRequired();
        builder.Property(p => p.Nome).HasColumnName("nome").IsRequired();
        builder.Property(p => p.PrecoCentavos).HasColumnName("preco_centavos").IsRequired();
        builder.Property(p => p.Ativo).HasColumnName("ativo").HasConversion<int>().IsRequired();
        builder.Property(p => p.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(p => p.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();

        builder.HasOne<Categoria>()
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.CategoriaId, p.Ativo }).HasDatabaseName("idx_produto_categoria_ativo");
    }
}
