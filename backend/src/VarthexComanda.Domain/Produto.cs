namespace VarthexComanda.Domain;

public class Produto
{
    public required int Id { get; set; }
    public required int CategoriaId { get; set; }
    public required string Nome { get; set; }
    public required long PrecoCentavos { get; set; }
    public required bool Ativo { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
    public string? FotoArquivo { get; set; }
}
