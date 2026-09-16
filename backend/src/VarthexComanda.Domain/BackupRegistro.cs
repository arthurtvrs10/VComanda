namespace VarthexComanda.Domain;

public class BackupRegistro
{
    public required int Id { get; set; }
    public required string Arquivo { get; set; }
    public required string Destino { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required StatusBackup Status { get; set; }
    public string? Checksum { get; set; }
    public string? Mensagem { get; set; }
}
