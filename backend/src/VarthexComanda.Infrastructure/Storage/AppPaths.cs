namespace VarthexComanda.Infrastructure.Storage;

public class AppPaths
{
    public AppPaths(string? rootOverride = null)
    {
        var baseDir = rootOverride
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Root = rootOverride is null ? Path.Combine(baseDir, "VarthexComanda") : baseDir;
        DataDirectory = Path.Combine(Root, "data");
        LogsDirectory = Path.Combine(Root, "logs");
        BackupsDirectory = Path.Combine(Root, "backups");
        FotosDirectory = Path.Combine(Root, "fotos");
        DatabasePath = Path.Combine(DataDirectory, "varthex-comanda.db");
    }

    public string Root { get; }
    public string DataDirectory { get; }
    public string LogsDirectory { get; }
    public string BackupsDirectory { get; }
    public string FotosDirectory { get; }
    public string DatabasePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(FotosDirectory);
    }
}
