namespace VarthexComanda.Domain;

public class Configuracao
{
    public required string Chave { get; set; }
    public required string Valor { get; set; }
    public required DateTime AtualizadoEm { get; set; }
}
