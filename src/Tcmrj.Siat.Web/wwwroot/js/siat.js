/* SIAT: funções de navegador chamadas pelos componentes Blazor (window.siat).
   O tema é aplicado antes da primeira pintura pelo script inline de App.razor; aqui ficam
   a troca de tema pelo SeletorTema, a cópia para a área de transferência, os atalhos de teclado
   das listas (só movem o foco) e o download dos arquivos exportados. */
window.siat = (function () {
  'use strict';

  const raiz = document.documentElement;

  // ---------- Tema: claro, escuro ou do sistema ----------
  // Mesma chave do protótipo (design/siat.js). Os valores antigos (light/dark/system) ainda são aceitos.
  const CHAVE_TEMA = 'siat-tema';
  const TEMAS = ['claro', 'escuro', 'sistema'];
  const EQUIVALENTES = { light: 'claro', dark: 'escuro', system: 'sistema' };
  const consultaEscuro = window.matchMedia('(prefers-color-scheme: dark)');

  function normalizarTema(valor) {
    const tema = EQUIVALENTES[valor] || valor;
    return TEMAS.includes(tema) ? tema : 'sistema';
  }

  function temaSalvo() {
    try { return normalizarTema(localStorage.getItem(CHAVE_TEMA)); } catch { return 'sistema'; }
  }

  // data-theme alimenta o siat.css; a classe "dark" alimenta o Blazor Blueprint.
  function aplicarTema(tema) {
    const escuro = tema === 'escuro' || (tema === 'sistema' && consultaEscuro.matches);
    raiz.dataset.theme = escuro ? 'dark' : 'light';
    raiz.classList.toggle('dark', escuro);
  }

  function definirTema(tema) {
    const valor = normalizarTema(tema);
    try { localStorage.setItem(CHAVE_TEMA, valor); } catch { /* sem armazenamento: vale só nesta aba */ }
    aplicarTema(valor);
    return valor;
  }

  consultaEscuro.addEventListener('change', () => aplicarTema(temaSalvo()));

  // ---------- Copiar ----------
  async function copiar(texto) {
    const valor = String(texto ?? '');
    try {
      await navigator.clipboard.writeText(valor);
      return true;
    } catch {
      // Sem a API assíncrona (contexto não seguro ou permissão negada): tenta o caminho antigo.
      const area = document.createElement('textarea');
      area.value = valor;
      area.setAttribute('readonly', '');
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.appendChild(area);
      area.select();
      let ok = false;
      try { ok = document.execCommand('copy'); } catch { ok = false; }
      area.remove();
      return ok;
    }
  }

  // ---------- Teclado (contracts/rotas-ui.md, Atalhos de teclado) ----------
  // Só move o foco: "/" vai para o filtro local ([data-busca]) fora de campos de texto; ↑/↓ andam entre as linhas
  // da lista expansível (button.item-linha[data-linha]) e ↓ no filtro vai para a primeira linha. Enter e Espaço na
  // linha abrem ou fecham o detalhe pelo próprio botão. Esc no filtro é tratado pelo componente FiltroLocal.
  function emCampoDeTexto(elemento) {
    if (!elemento) return false;
    if (elemento.isContentEditable) return true;
    if (elemento.tagName === 'TEXTAREA' || elemento.tagName === 'SELECT') return true;
    if (elemento.tagName !== 'INPUT') return false;
    return !['button', 'checkbox', 'radio', 'submit', 'reset', 'file', 'range', 'color', 'image'].includes(elemento.type);
  }

  function linhasDaLista(linha) {
    const lista = linha.closest('.lista-itens') || document;
    return Array.from(lista.querySelectorAll('button.item-linha[data-linha]'));
  }

  document.addEventListener('keydown', ev => {
    if (ev.defaultPrevented || ev.ctrlKey || ev.metaKey || ev.altKey) return;
    const atual = document.activeElement;

    if (ev.key === '/' && !emCampoDeTexto(atual)) {
      const busca = document.querySelector('[data-busca]');
      if (!busca) return;
      ev.preventDefault();
      busca.focus();
      busca.select();
      return;
    }

    if (ev.key !== 'ArrowDown' && ev.key !== 'ArrowUp') return;

    if (atual && atual.matches('[data-busca]') && ev.key === 'ArrowDown') {
      const primeira = document.querySelector('.lista-itens button.item-linha[data-linha]');
      if (primeira) { ev.preventDefault(); primeira.focus(); }
      return;
    }

    if (!atual || !atual.matches('button.item-linha[data-linha]')) return;
    const linhas = linhasDaLista(atual);
    const indice = linhas.indexOf(atual) + (ev.key === 'ArrowDown' ? 1 : -1);
    if (indice < 0 || indice >= linhas.length) return;
    ev.preventDefault();
    linhas[indice].focus();
    linhas[indice].scrollIntoView({ block: 'nearest' });
  });

  // ---------- Seletor com busca (SeletorPesquisavel.razor) ----------
  // O componente trata as teclas no servidor; aqui só se impede o efeito padrão delas: ↑/↓ não rolam a página e o
  // Enter na busca não envia o formulário. Roda antes do atalho de listas acima (registrado na captura).
  document.addEventListener('keydown', ev => {
    const alvo = ev.target;
    if (!(alvo instanceof Element)) return;
    if (alvo.matches('[data-combo-busca]') && ['ArrowDown', 'ArrowUp', 'Enter'].includes(ev.key)) ev.preventDefault();
    else if (alvo.matches('[data-combo]') && (ev.key === 'ArrowDown' || ev.key === 'ArrowUp')) ev.preventDefault();
  }, true);

  function mostrarOpcao(id) {
    const opcao = document.getElementById(id);
    if (opcao) opcao.scrollIntoView({ block: 'nearest' });
  }

  // ---------- Download de arquivo gerado no servidor (exportação, research.md R12) ----------
  // Recebe um DotNetStreamReference; o arquivo vira um Blob local, sem nova requisição ao SIAT.
  async function baixarArquivo(nome, referencia, tipo) {
    const dados = await referencia.arrayBuffer();
    const url = URL.createObjectURL(new Blob([dados], { type: tipo || 'text/html;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = nome || 'arquivo';
    link.hidden = true;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  }

  return { temaSalvo, definirTema, aplicarTema, copiar, mostrarOpcao, baixarArquivo };
})();
