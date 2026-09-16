namespace VarthexComanda.Infrastructure.Storage;

public static class DatabaseBackupService
{
    public static string? BackupIfExists(string databasePath, string backupsDirectory, DateTime timestampUtc)
    {
        if (!File.Exists(databasePath))
        {
            return null;
        }

        var fileName = $"varthex-comanda-{timestampUtc:yyyy-MM-dd-HHmmss}.db";
        var destination = Path.Combine(backupsDirectory, fileName);
        File.Copy(databasePath, destination, overwrite: false);
        return destination;
    }
}
