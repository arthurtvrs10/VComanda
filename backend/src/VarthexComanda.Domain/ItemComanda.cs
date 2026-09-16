namespace VarthexComanda.Domain;

public class ItemComanda
{
    public required int Id { get; set; }
    public required int ComandaId { get; set; }
    public required int ProdutoId { get; set; }
    public required string NomeProduto { get; set; }
    public required long PrecoUnitarioCentavos { get; set; }
    public required int Quantidade { get; set; }
    public required long SubtotalCentavos { get; set; }
    public string? Observacao { get; set; }
    public required DateTime CriadoEm { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
