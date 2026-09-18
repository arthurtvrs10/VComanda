namespace VarthexComanda.Application.Atendimento;

public static class FusoBrasilia
{
    // Offset fixo (Brasil não observa horário de verão atualmente) — nunca usar
    // DateTime.ToLocalTime()/ToUniversalTime(), que dependem do fuso do SO, não
    // do fuso do negócio (RN20).
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-3);

    public static DateTime ParaUtc(DateTime local) => local - Offset;

    public static DateTime ParaLocal(DateTime utc) => utc + Offset;
}
