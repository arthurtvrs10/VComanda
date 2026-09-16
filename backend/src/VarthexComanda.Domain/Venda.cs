namespace VarthexComanda.Domain;

public class Venda
{
    public required int Id { get; set; }
    public required int ComandaId { get; set; }
    public required int Numero { get; set; }
    public required long TotalCentavos { get; set; }
    public required DateTime FinalizadaEm { get; set; }
    public required StatusVenda Status { get; set; }
}
