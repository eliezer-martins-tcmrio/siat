// Comportamento da tela de reconexão (mesmo contrato do ReconnectModal do modelo do .NET 10).
const modal = document.getElementById('components-reconnect-modal');
modal.addEventListener('components-reconnect-state-changed', aoMudarEstado);

document.getElementById('components-reconnect-button').addEventListener('click', tentarDeNovo);
document.getElementById('components-resume-button').addEventListener('click', retomar);
document.getElementById('siat-reconnect-reload').addEventListener('click', () => location.reload());

function aoMudarEstado(evento) {
  const estado = evento.detail.state;
  if (estado === 'show') {
    if (!modal.open) {
      modal.showModal();
    }
  } else if (estado === 'hide') {
    modal.close();
  } else if (estado === 'failed') {
    document.addEventListener('visibilitychange', tentarQuandoVisivel);
  } else if (estado === 'rejected') {
    // O servidor não reconhece mais o circuito: recarregar é o caminho mais curto de volta.
    location.reload();
  }
}

async function tentarDeNovo() {
  document.removeEventListener('visibilitychange', tentarQuandoVisivel);
  try {
    // true: reconectou; false: o servidor respondeu, mas recusou o circuito; exceção: servidor fora do ar.
    const reconectou = await Blazor.reconnect();
    if (!reconectou) {
      const retomou = await Blazor.resumeCircuit();
      if (retomou) {
        modal.close();
      } else {
        location.reload();
      }
    }
  } catch {
    document.addEventListener('visibilitychange', tentarQuandoVisivel);
  }
}

async function retomar() {
  try {
    const retomou = await Blazor.resumeCircuit();
    if (!retomou) {
      location.reload();
    }
  } catch {
    location.reload();
  }
}

async function tentarQuandoVisivel() {
  if (document.visibilityState === 'visible') {
    await tentarDeNovo();
  }
}
