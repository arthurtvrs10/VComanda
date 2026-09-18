namespace VarthexComanda.Desktop.Atendimento;

public class EncerramentoDialog : IEncerramentoDialog
{
    private readonly Func<EncerramentoViewModel> _fabricaViewModel;

    public EncerramentoDialog(Func<EncerramentoViewModel> fabricaViewModel)
    {
        _fabricaViewModel = fabricaViewModel;
    }

    public bool Abrir(int comandaId)
    {
        var viewModel = _fabricaViewModel();
        viewModel.Carregar(comandaId);
        var view = new EncerramentoView(viewModel) { Owner = System.Windows.Application.Current.MainWindow };
        return view.ShowDialog() == true;
    }
}
