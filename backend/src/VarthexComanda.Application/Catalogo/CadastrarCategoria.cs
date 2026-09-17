using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class CadastrarCategoria
{
    private readonly ICategoriaRepository _repositorio;
    private readonly IClock _relogio;

    public CadastrarCategoria(ICategoriaRepository repositorio, IClock relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public Resultado<Categoria> Executar(string nome)
    {
        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Categoria>.Falha("Informe o nome da categoria.");
        }
        if (_repositorio.ExisteNome(nomeNormalizado))
        {
            return Resultado<Categoria>.Falha("Já existe uma categoria com esse nome.");
        }

        var agora = _relogio.UtcNow;
        var categoria = new Categoria
        {
            Id = 0,
            Nome = nomeNormalizado,
            Ativo = true,
            CriadoEm = agora,
            AtualizadoEm = agora
        };
        return Resultado<Categoria>.Ok(_repositorio.Salvar(categoria));
    }
}
