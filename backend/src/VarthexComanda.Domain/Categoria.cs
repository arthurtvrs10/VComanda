namespace VarthexComanda.Domain;

public class Categoria
{
    public required int Id { get; set; }
    public required string Nome { get; set; }
    public required bool Ativo { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
