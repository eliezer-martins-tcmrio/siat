namespace Tcmrj.Siat.App.Web.ViewModels.Shared;

/// <summary>Resultado de uma consulta concluída: o servidor consultado, os itens na ordem do SQL e o momento da consulta.</summary>
public sealed record ResultadoConsultaVM<T>(ServidorVM Servidor, IReadOnlyList<T> Itens, DateTimeOffset ConsultadoEm);
