using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class ConfiguracaoConfiguration : IEntityTypeConfiguration<VarthexComanda.Domain.Configuracao>
{
    public void Configure(EntityTypeBuilder<VarthexComanda.Domain.Configuracao> builder)
    {
        builder.ToTable("configuracao");
        builder.HasKey(c => c.Chave);
        builder.Property(c => c.Chave).HasColumnName("chave");
        builder.Property(c => c.Valor).HasColumnName("valor").IsRequired();
        builder.Property(c => c.AtualizadoEm).HasColumnName("atualizado_em").IsRequired();
    }
}
