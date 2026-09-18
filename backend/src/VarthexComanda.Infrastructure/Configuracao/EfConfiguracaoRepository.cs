using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;

namespace VarthexComanda.Infrastructure.Configuracao;

public class EfConfiguracaoRepository : IConfiguracaoRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfConfiguracaoRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public string? ObterValor(string chave)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Configuracoes.SingleOrDefault(c => c.Chave == chave)?.Valor;
    }

    public void Definir(string chave, string valor, DateTime atualizadoEm)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var existente = contexto.Configuracoes.SingleOrDefault(c => c.Chave == chave);
        if (existente is null)
        {
            // Qualificado totalmente: dentro deste namespace, "Configuracao" solto
            // resolveria para o namespace VarthexComanda.Infrastructure.Configuracao
            // (o próprio namespace deste arquivo), não para a classe de domínio —
            // mesma colisão já vista com System.Windows.Application em App.xaml.cs.
            contexto.Configuracoes.Add(new VarthexComanda.Domain.Configuracao
            {
                Chave = chave,
                Valor = valor,
                AtualizadoEm = atualizadoEm
            });
        }
        else
        {
            existente.Valor = valor;
            existente.AtualizadoEm = atualizadoEm;
        }

        contexto.SaveChanges();
    }
}
