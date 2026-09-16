using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Configurations;

public class BackupRegistroConfiguration : IEntityTypeConfiguration<BackupRegistro>
{
    public void Configure(EntityTypeBuilder<BackupRegistro> builder)
    {
        builder.ToTable("backup_registro", t => t.HasCheckConstraint("CK_backup_registro_status", "status IN ('SUCESSO', 'FALHA')"));
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.Arquivo).HasColumnName("arquivo").IsRequired();
        builder.Property(b => b.Destino).HasColumnName("destino").IsRequired();
        builder.Property(b => b.CriadoEm).HasColumnName("criado_em").IsRequired();
        builder.Property(b => b.Status)
            .HasColumnName("status")
            .HasConversion(
                s => s.ToString().ToUpperInvariant(),
                s => Enum.Parse<StatusBackup>(s, ignoreCase: true))
            .IsRequired();
        builder.Property(b => b.Checksum).HasColumnName("checksum");
        builder.Property(b => b.Mensagem).HasColumnName("mensagem");
    }
}
