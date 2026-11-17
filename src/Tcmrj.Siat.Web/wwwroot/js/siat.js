/* SIAT: funções de navegador chamadas pelos componentes Blazor (window.siat).
   O tema é aplicado antes da primeira pintura pelo script inline de App.razor; aqui ficam
   a troca de tema pelo SeletorTema e a cópia para a área de transferência. */
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

  return { temaSalvo, definirTema, aplicarTema, copiar };
})();
