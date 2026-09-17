using VarthexComanda.Application.Abstractions;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Catalogo;

public class AlterarCategoria
{
    private readonly ICategoriaRepository _repositorio;
    private readonly IClock _relogio;

    public AlterarCategoria(ICategoriaRepository repositorio, IClock relogio)
    {
        _repositorio = repositorio;
        _relogio = relogio;
    }

    public Resultado<Categoria> Executar(int id, string nome, bool ativo)
    {
        var categoria = _repositorio.BuscarPorId(id);
        if (categoria is null)
        {
            return Resultado<Categoria>.Falha("Categoria não encontrada.");
        }

        var nomeNormalizado = (nome ?? string.Empty).Trim();
        if (nomeNormalizado.Length == 0)
        {
            return Resultado<Categoria>.Falha("Informe o nome da categoria.");
        }
        if (_repositorio.ExisteNome(nomeNormalizado, ignorarId: id))
        {
            return Resultado<Categoria>.Falha("Já existe uma categoria com esse nome.");
        }

        categoria.Nome = nomeNormalizado;
        categoria.Ativo = ativo;
        categoria.AtualizadoEm = _relogio.UtcNow;
        return Resultado<Categoria>.Ok(_repositorio.Salvar(categoria));
    }
}
