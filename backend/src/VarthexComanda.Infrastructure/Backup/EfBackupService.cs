using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Infrastructure.Backup;

public class EfBackupService : IBackupService
{
    private readonly AppPaths _paths;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;
    private readonly int _retencaoMaxima;

    public EfBackupService(AppPaths paths, IBackupRegistroRepository registros, IClock relogio, int retencaoMaxima = 30)
    {
        _paths = paths;
        _registros = registros;
        _relogio = relogio;
        _retencaoMaxima = retencaoMaxima;
    }

    public Resultado<BackupRegistro> CriarBackupGerenciado() =>
        CriarBackupInterno(_paths.BackupsDirectory, aplicarRetencao: true);

    public Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna) =>
        CriarBackupInterno(pastaExterna, aplicarRetencao: false);

    private Resultado<BackupRegistro> CriarBackupInterno(string pastaDestino, bool aplicarRetencao)
    {
        var agora = _relogio.UtcNow;
        var nomeArquivo = $"varthex-comanda-{agora:yyyy-MM-dd-HHmmss}.db";
        var destinoFinal = Path.Combine(pastaDestino, nomeArquivo);
        var destinoTemporario = destinoFinal + ".tmp";

        try
        {
            Directory.CreateDirectory(pastaDestino);

            using (var origem = new SqliteConnection($"Data Source={_paths.DatabasePath}"))
            using (var destino = new SqliteConnection($"Data Source={destinoTemporario};Pooling=False"))
            {
                origem.Open();
                destino.Open();
                origem.BackupDatabase(destino);
            }

            if (!VerificarIntegridade(destinoTemporario))
            {
                File.Delete(destinoTemporario);
                var registroFalha = new BackupRegistro
                {
                    Id = 0,
                    Arquivo = nomeArquivo,
                    Destino = pastaDestino,
                    CriadoEm = agora,
                    Status = StatusBackup.Falha,
                    Checksum = null,
                    Mensagem = "Falha na verificação de integridade do backup."
                };
                _registros.Registrar(registroFalha);
                return Resultado<BackupRegistro>.Falha(registroFalha.Mensagem!);
            }

            var checksum = CalcularChecksumSha256(destinoTemporario);
            File.Move(destinoTemporario, destinoFinal);
            File.WriteAllText(destinoFinal + ".sha256", checksum);

            var registro = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Sucesso,
                Checksum = checksum,
                Mensagem = null
            };
            _registros.Registrar(registro);

            if (aplicarRetencao)
            {
                AplicarRetencao(pastaDestino);
            }

            return Resultado<BackupRegistro>.Ok(registro);
        }
        catch (Exception ex)
        {
            if (File.Exists(destinoTemporario))
            {
                File.Delete(destinoTemporario);
            }

            var registroFalha = new BackupRegistro
            {
                Id = 0,
                Arquivo = nomeArquivo,
                Destino = pastaDestino,
                CriadoEm = agora,
                Status = StatusBackup.Falha,
                Checksum = null,
                Mensagem = ex.Message
            };
            try { _registros.Registrar(registroFalha); } catch { /* nao mascarar a falha original */ }
            return Resultado<BackupRegistro>.Falha(ex.Message);
        }
    }

    private static bool VerificarIntegridade(string caminhoArquivo)
    {
        using var conexao = new SqliteConnection($"Data Source={caminhoArquivo};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = "PRAGMA integrity_check";
        return (string?)comando.ExecuteScalar() == "ok";
    }

    private void AplicarRetencao(string pasta)
    {
        var arquivos = Directory.GetFiles(pasta, "varthex-comanda-*.db")
            .OrderByDescending(f => f)
            .Skip(_retencaoMaxima)
            .ToList();

        foreach (var arquivo in arquivos)
        {
            File.Delete(arquivo);
            var companheiro = arquivo + ".sha256";
            if (File.Exists(companheiro))
            {
                File.Delete(companheiro);
            }
        }
    }

    private static string CalcularChecksumSha256(string caminhoArquivo)
    {
        using var stream = File.OpenRead(caminhoArquivo);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public RelatorioValidacao Validar(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo) => throw new NotImplementedException("Implementado na Task 3.");
}
