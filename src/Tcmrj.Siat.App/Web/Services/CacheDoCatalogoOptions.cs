namespace Tcmrj.Siat.App.Web.Services;

/// <summary>
/// Validade do catálogo em memória, lida da mesma seção <c>CatalogoSistemas</c> das opções de leitura do Infra (o
/// App não depende do Infra). A faixa de 1 a 60 minutos é validada na leitura pelo repositório.
/// </summary>
public sealed class CacheDoCatalogoOptions
{
    public const string Secao = "CatalogoSistemas";

    public int MinutosDeCache { get; set; } = 10;
}
