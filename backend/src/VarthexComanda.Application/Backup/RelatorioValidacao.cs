namespace VarthexComanda.Application.Backup;

public class RelatorioValidacao
{
    public required bool FormatoValido { get; init; }
    public required bool VersaoCompativel { get; init; }
    public required bool IntegridadeOk { get; init; }
    public bool? ChecksumConfere { get; init; }
    public required string Motivo { get; init; }

    public bool Aprovado => FormatoValido && VersaoCompativel && IntegridadeOk && ChecksumConfere != false;
}
