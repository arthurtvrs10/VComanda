using System.Windows;

namespace VarthexComanda.Desktop.Atendimento;

public class MessageBoxConfirmador : IConfirmador
{
    public bool Confirmar(string titulo, string mensagem) =>
        MessageBox.Show(mensagem, titulo, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
