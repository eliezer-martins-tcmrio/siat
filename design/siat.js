/* SIAT: comportamento compartilhado das telas de exemplo.
   Protótipo estático: os dados são fictícios e as consultas são simuladas. */
const SIAT = (function () {
  const raiz = document.documentElement;

  // ---------- Ícones (traço do Lucide, o mesmo conjunto do gestao-copilot) ----------
  const TRACOS = {
    sistemas: '<rect width="20" height="8" x="2" y="2" rx="2"/><rect width="20" height="8" x="2" y="14" rx="2"/><path d="M6 6h.01M6 18h.01"/>',
    versoes: '<path d="M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z"/><circle cx="7.5" cy="7.5" r=".5" fill="currentColor"/>',
    mailqueue: '<rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>',
    auditlog: '<path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01"/>',
    documentacao: '<path d="M12 7v14"/><path d="M3 18a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1h5a4 4 0 0 1 4 4 4 4 0 0 1 4-4h5a1 1 0 0 1 1 1v13a1 1 0 0 1-1 1h-6a3 3 0 0 0-3 3 3 3 0 0 0-3-3z"/>',
    copilot: '<path d="M3 3v16a2 2 0 0 0 2 2h16"/><path d="M18 17V9M13 17V5M8 17v-3"/>',
    builds: '<path d="M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z"/><path d="M12 22V12"/><path d="m3.3 7 8.7 5 8.7-5"/>',
    copiar: '<rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>',
    certo: '<path d="M20 6 9 17l-5-5"/>',
    seta: '<path d="m9 18 6-6-6-6"/>',
    busca: '<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>',
    atualizar: '<path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>',
    baixar: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m7 10 5 5 5-5"/><path d="M12 15V3"/>',
    tema: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/>',
    sair: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5"/><path d="M21 12H9"/>',
    design: '<rect width="7" height="7" x="3" y="3" rx="1"/><rect width="7" height="7" x="14" y="3" rx="1"/><rect width="7" height="7" x="14" y="14" rx="1"/><rect width="7" height="7" x="3" y="14" rx="1"/>',
  };
  function icone(nome, tamanho) {
    const t = tamanho || 16;
    return `<svg class="${nome === 'seta' ? 'seta' : ''}" width="${t}" height="${t}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${TRACOS[nome]}</svg>`;
  }

  // ---------- Utilitários ----------
  function esc(texto) {
    return String(texto ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }
  function normaliza(texto) {
    return String(texto).toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
  }
  // Todos os termos precisam aparecer, sem diferenciar acentos e maiúsculas.
  function casa(textoNormalizado, busca) {
    return normaliza(busca).split(/\s+/).filter(Boolean).every(t => textoNormalizado.includes(t));
  }
  function segundos(ms) {
    return (ms / 1000).toFixed(1).replace('.', ',') + ' s';
  }
  function hora(data) {
    return (data || new Date()).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  }
  const url = {
    ler: chave => new URLSearchParams(location.search).get(chave) || '',
    gravar(valores) {
      const p = new URLSearchParams(location.search);
      Object.entries(valores).forEach(([k, v]) => (v ? p.set(k, v) : p.delete(k)));
      const q = p.toString();
      history.replaceState(null, '', location.pathname + (q ? '?' + q : ''));
    },
  };

  // ---------- Tema: claro, escuro ou do sistema ----------
  const TEMAS = { system: 'Sistema', light: 'Claro', dark: 'Escuro' };
  function temaSalvo() {
    try { return localStorage.getItem('siat-tema') || 'system'; } catch { return 'system'; }
  }
  function aplicarTema(tema) {
    const escuro = tema === 'dark' || (tema === 'system' && matchMedia('(prefers-color-scheme: dark)').matches);
    raiz.dataset.theme = escuro ? 'dark' : 'light';
    const botao = document.getElementById('tema');
    if (botao) botao.querySelector('span').textContent = 'Tema: ' + TEMAS[tema];
  }
  function alternarTema() {
    const ordem = Object.keys(TEMAS);
    const proximo = ordem[(ordem.indexOf(temaSalvo()) + 1) % ordem.length];
    try { localStorage.setItem('siat-tema', proximo); } catch { /* sem armazenamento: vale só nesta página */ }
    aplicarTema(proximo);
  }
  matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => aplicarTema(temaSalvo()));

  // ---------- Menu lateral ----------
  const GRUPOS = [
    { titulo: 'Catálogo', itens: [
      { id: 'sistemas', rotulo: 'Sistemas', href: 'sistemas.html' },
      { id: 'versoes', rotulo: 'Versões', href: 'versoes.html' },
    ] },
    { titulo: 'Visor', itens: [
      { id: 'mailqueue', rotulo: 'MailQueue', href: 'mailqueue.html' },
      { id: 'auditlog', rotulo: 'AuditLog', href: 'auditlog.html' },
    ] },
    { titulo: 'Conhecimento', itens: [
      { id: 'documentacao', rotulo: 'Documentação', fase: 5 },
    ] },
    { titulo: 'Gestão', itens: [
      { id: 'copilot', rotulo: 'Copilot', fase: 4 },
      { id: 'builds', rotulo: 'Builds', fase: 7 },
    ] },
  ];

  function montarMenu() {
    const app = document.querySelector('.app');
    if (!app) return;
    const atual = document.body.dataset.pagina;
    const grupos = GRUPOS.map(g => `
      <div class="menu-grupo" role="group" aria-label="${g.titulo}">
        <span class="menu-titulo" aria-hidden="true">${g.titulo}</span>
        ${g.itens.map(i => i.href
          ? `<a class="menu-item" href="${i.href}"${i.id === atual ? ' aria-current="page"' : ''}>${icone(i.id, 17)}${i.rotulo}</a>`
          : `<span class="menu-item" aria-disabled="true" title="Entra na fase ${i.fase} do roteiro">${icone(i.id, 17)}${i.rotulo}<span class="menu-fase">fase ${i.fase}</span></span>`).join('')}
      </div>`).join('');

    const menu = document.createElement('aside');
    menu.className = 'menu';
    menu.innerHTML = `
      <a class="marca" href="sistemas.html" aria-label="SIAT, Sistema Integrado de Apoio Técnico do TCMRio">
        <img class="logo-claro" src="img/tcmrio-sigla.png" width="109" height="20" alt="">
        <img class="logo-escuro" src="img/tcmrio-sigla-negativo.png" width="109" height="20" alt="">
        <span>SIAT</span>
      </a>
      <nav class="menu-nav" aria-label="Navegação principal">${grupos}</nav>
      <div class="menu-rodape">
        <a class="so-desktop" href="index.html"${atual === 'design' ? ' aria-current="page"' : ''}>${icone('design', 14)}Sistema de design</a>
        <button type="button" id="tema">${icone('tema', 14)}<span></span></button>
        <div class="conta">
          <span class="avatar" aria-hidden="true">EM</span>
          <span><strong>Eliezer Martins</strong><span>Desenvolvedor · TCMRio</span></span>
        </div>
        <button type="button" class="so-desktop" title="Protótipo: o login é pelo SSO do UserManager">${icone('sair', 14)}Sair</button>
      </div>`;
    app.prepend(menu);
    document.getElementById('tema').addEventListener('click', alternarTema);
    aplicarTema(temaSalvo());
  }

  // ---------- Atalho "/" para a busca e botões de copiar ----------
  document.addEventListener('keydown', ev => {
    const busca = document.querySelector('[data-busca]');
    if (!busca) return;
    const digitando = /^(INPUT|SELECT|TEXTAREA)$/.test(document.activeElement.tagName);
    if (ev.key === '/' && !digitando) { ev.preventDefault(); busca.focus(); busca.select(); }
    if (ev.key === 'Escape' && document.activeElement === busca && busca.value) {
      busca.value = '';
      busca.dispatchEvent(new Event('input', { bubbles: true }));
    }
  });

  function botaoCopiar(valor, rotulo) {
    return `<button type="button" class="copiar" data-copiar="${esc(valor)}" aria-label="Copiar ${esc(rotulo)}" title="Copiar">${icone('copiar', 13)}</button>`;
  }
  document.addEventListener('click', async ev => {
    const botao = ev.target.closest('[data-copiar], [data-copiar-de]');
    if (!botao) return;
    const texto = botao.dataset.copiarDe
      ? document.getElementById(botao.dataset.copiarDe).dataset.original
      : botao.dataset.copiar;
    let ok = true;
    try { await navigator.clipboard.writeText(texto); } catch { ok = false; }
    const original = botao.innerHTML;
    clearTimeout(botao._timer);
    if (botao.classList.contains('copiar')) {
      botao.classList.toggle('copiado', ok);
      botao.innerHTML = icone(ok ? 'certo' : 'copiar', 13);
    } else {
      botao.textContent = ok ? 'Copiado' : 'Falhou';
    }
    botao._timer = setTimeout(() => {
      botao.classList.remove('copiado');
      botao.innerHTML = botao._original || original;
    }, 1500);
    botao._original = botao._original || original;
  });

  // ---------- Seletor de servidor: lista fechada, cada servidor com seu ambiente ----------
  const AMBIENTES = { dsv: 'Desenvolvimento', hml: 'Homologação', prd: 'Produção', cont: 'Contingência' };
  const SERVIDORES = [
    { nome: 'TCMBD3\\DEV', amb: 'dsv' },
    { nome: 'TCMBD4\\HML', amb: 'hml' },
    { nome: 'TCMBD', amb: 'prd' },
    { nome: 'TCMBD2', amb: 'prd' },
  ];
  function servidor(nome) {
    return SERVIDORES.find(s => s.nome === nome) || SERVIDORES[0];
  }
  function seletorServidor(container, inicial, aoMudar) {
    const grupos = Object.keys(AMBIENTES).map(amb => {
      const lista = SERVIDORES.filter(s => s.amb === amb);
      return lista.length
        ? `<optgroup label="${AMBIENTES[amb]}">${lista.map(s => `<option>${esc(s.nome)}</option>`).join('')}</optgroup>` : '';
    }).join('');
    container.classList.add('seletor-servidor');
    container.innerHTML = `<span class="amb"></span><select aria-label="Servidor">${grupos}</select>`;
    const select = container.querySelector('select');
    const pintar = () => {
      const s = servidor(select.value);
      container.dataset.amb = s.amb;
      container.querySelector('.amb').textContent = s.amb.toUpperCase();
    };
    select.value = servidor(inicial).nome;
    pintar();
    select.addEventListener('change', () => { pintar(); if (aoMudar) aoMudar(servidor(select.value)); });
    return { valor: () => servidor(select.value) };
  }

  // Faixa de contexto: deixa o ambiente consultado sempre à vista.
  function contexto(el, s, modulo) {
    el.dataset.amb = s.amb;
    el.innerHTML = `<span class="amb">${s.amb.toUpperCase()}</span>
      <span>Consultando <strong class="mono">${esc(s.nome)}</strong> · ${esc(modulo)}</span>
      <span class="contexto-aviso">${s.amb === 'prd'
        ? 'Produção: esta consulta fica registrada em log com o seu usuário.'
        : AMBIENTES[s.amb]}</span>`;
  }

  // ---------- Consulta simulada: tempo decorrido e cancelamento ----------
  function consulta({ status, progresso, consultar, cancelar }) {
    let espera, relogio, inicio, emCurso = false;
    function fim() {
      clearTimeout(espera); clearInterval(relogio);
      emCurso = false;
      progresso.hidden = true; cancelar.hidden = true; consultar.disabled = false;
    }
    cancelar.addEventListener('click', () => {
      if (!emCurso) return;
      const duracao = performance.now() - inicio;
      fim();
      status.className = 'status alerta';
      status.textContent = `Consulta cancelada após ${segundos(duracao)} · mostrando o resultado anterior`;
    });
    return {
      iniciar(rotulo, ms, pronto) {
        fim();
        emCurso = true; inicio = performance.now();
        progresso.hidden = false; cancelar.hidden = false; consultar.disabled = true;
        status.className = 'status';
        const pulso = () => { status.textContent = `${rotulo}… ${segundos(performance.now() - inicio)}`; };
        pulso(); relogio = setInterval(pulso, 100);
        espera = setTimeout(() => { const d = performance.now() - inicio; fim(); pronto(d); }, ms);
      },
    };
  }

  // ---------- Lista expansível de resultados ----------
  function listaExpansivel({ raiz: el, id, linha, detalhe, aoAbrir }) {
    let itens = [];
    function alternar(botao, abrir) {
      const painel = botao.nextElementSibling;
      const aberto = abrir ?? botao.getAttribute('aria-expanded') !== 'true';
      botao.setAttribute('aria-expanded', aberto);
      painel.hidden = !aberto;
      if (aberto && !painel.dataset.pronto) {
        detalhe(itens[Number(botao.dataset.i)], painel);
        painel.dataset.pronto = '1';
      }
      if (aoAbrir) aoAbrir(aberto ? botao.dataset.id : '');
    }
    el.addEventListener('click', ev => {
      const botao = ev.target.closest('.item-linha');
      if (botao) alternar(botao);
    });
    return {
      itens: () => itens,
      mostrar(novos, selecionado) {
        itens = novos;
        el.innerHTML = itens.map((item, i) => {
          const l = linha(item);
          return `<div class="item" data-busca-texto="${esc(normaliza(l.busca))}">
            <button type="button" class="item-linha" aria-expanded="false" data-i="${i}" data-id="${esc(id(item))}">${icone('seta', 15)}${l.celulas}</button>
            <div class="item-detalhe" hidden></div>
          </div>`;
        }).join('');
        const alvo = selecionado && [...el.querySelectorAll('.item-linha')].find(b => b.dataset.id === String(selecionado));
        if (alvo) { alternar(alvo, true); alvo.scrollIntoView({ block: 'nearest' }); }
      },
      filtrar(termo) {
        let visiveis = 0;
        el.querySelectorAll('.item').forEach(item => {
          const ok = casa(item.dataset.buscaTexto, termo);
          item.hidden = !ok;
          if (ok) visiveis++;
        });
        return visiveis;
      },
    };
  }

  function esqueleto(linhas) {
    const larguras = [[70, 46, 18], [70, 58, 14], [70, 38, 22], [70, 52, 16], [70, 44, 20], [70, 60, 12]];
    return '<div class="esqueleto" aria-hidden="true">' + Array.from({ length: linhas }, (_, i) => {
      const [a, b, c] = larguras[i % larguras.length];
      return `<div><span style="width:${a}px"></span><span style="width:${b}%"></span><span style="width:${c}%; margin-left:auto"></span></div>`;
    }).join('') + '</div>';
  }

  // ---------- Datas ----------
  function dataCompleta(iso) {
    return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'medium' });
  }
  // "hoje 14:32" para o dia corrente; "08/11 09:10" para os demais.
  function quando(iso) {
    const d = new Date(iso), hoje = new Date();
    const hm = d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
    return d.toDateString() === hoje.toDateString()
      ? 'hoje ' + hm
      : d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit' }) + ' ' + hm;
  }
  // Data fictícia: N minutos antes de agora.
  function haMinutos(n) {
    return new Date(Date.now() - n * 60000).toISOString();
  }

  // ---------- Exportar o resultado em HTML autocontido ----------
  function exportar(arquivo, titulo, resumo, secoes) {
    const html = `<!doctype html><html lang="pt-BR"><meta charset="utf-8"><title>${esc(titulo)}</title>
<style>body{font:14px/1.5 "Segoe UI",system-ui,sans-serif;color:#1b2a3d;max-width:1100px;margin:24px auto;padding:0 20px}
h1{font-size:20px;margin:0}p.resumo{color:#56657a;margin:4px 0 20px}
section{border:1px solid #d9e0ea;border-radius:8px;padding:14px 16px;margin-bottom:12px}
h2{font-size:15px;margin:0 0 8px}dl{display:grid;grid-template-columns:120px 1fr;gap:4px 14px;margin:0}dt{color:#56657a}dd{margin:0;overflow-wrap:anywhere}
pre{background:#f4f6f9;border:1px solid #d9e0ea;border-radius:6px;padding:10px 12px;white-space:pre-wrap;word-break:break-word;font:12px/1.6 Consolas,monospace}
iframe{width:100%;height:240px;border:1px solid #d9e0ea;border-radius:6px}</style>
<h1>${esc(titulo)}</h1><p class="resumo">${esc(resumo)}</p>${secoes.join('')}</html>`;
    const link = document.createElement('a');
    link.href = URL.createObjectURL(new Blob([html], { type: 'text/html' }));
    link.download = arquivo;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
  }

  montarMenu();

  return { dataCompleta, quando, haMinutos, exportar, icone, esc, normaliza, casa, segundos, hora, url, botaoCopiar, servidor, seletorServidor, contexto, consulta, listaExpansivel, esqueleto, AMBIENTES };
})();
