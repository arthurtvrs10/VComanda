using System.Windows;

namespace VarthexComanda.Desktop.Atendimento;

public class JanelaConfirmador : IConfirmador
{
    public bool Confirmar(string titulo, string mensagem)
    {
        var janela = new ConfirmacaoView(titulo, mensagem);
        // Qualificado: dentro de VarthexComanda.Desktop, "Application" solto resolve para o namespace VarthexComanda.Application.
        var dono = System.Windows.Application.Current?.MainWindow;
        if (dono is not null && dono.IsLoaded)
        {
            janela.Owner = dono;
        }
        else
        {
            janela.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        return janela.ShowDialog() == true;
    }
}
