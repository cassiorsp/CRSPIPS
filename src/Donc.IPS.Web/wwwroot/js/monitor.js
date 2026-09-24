(function () {
    'use strict';

    const corpo = document.getElementById('corpoMonitor');
    if (!corpo) {
        return;
    }

    const filtro = document.getElementById('filtroFonte');
    const botaoPausar = document.getElementById('botaoPausar');
    const indicador = document.getElementById('indicadorVivo');
    const limiteLinhas = 300;
    let ultimoId = null;
    let pausado = false;
    let emAndamento = false;

    function celula(texto, classe) {
        const td = document.createElement('td');
        if (classe) {
            td.className = classe;
        }
        td.textContent = texto ?? '';
        return td;
    }

    function linha(evento, destacar) {
        const tr = document.createElement('tr');
        if (destacar) {
            tr.className = 'linha-nova';
        }

        tr.appendChild(celula(evento.quando, 'small text-nowrap'));

        const tdIp = document.createElement('td');
        if (evento.pais) {
            const bandeira = document.createElement('span');
            bandeira.className = 'fi fi-' + evento.pais + ' me-1';
            tdIp.appendChild(bandeira);
        }
        const link = document.createElement('a');
        link.href = corpo.dataset.urlDetalhe + '?ip=' + encodeURIComponent(evento.ip);
        link.className = 'fonte-mono';
        link.textContent = evento.ip;
        tdIp.appendChild(link);
        if (evento.local) {
            const local = document.createElement('div');
            local.className = 'small text-body-secondary text-nowrap';
            local.textContent = evento.local;
            tdIp.appendChild(local);
        }
        tr.appendChild(tdIp);

        tr.appendChild(celula(evento.fonte, 'small'));
        tr.appendChild(celula(evento.regra, 'small'));
        tr.appendChild(celula(evento.site ?? '—', 'small text-nowrap'));
        tr.appendChild(celula(evento.requisicao, 'small fonte-mono text-break'));
        tr.appendChild(celula(evento.status));
        tr.appendChild(celula(evento.detalhe, 'small text-break'));
        return tr;
    }

    function mostrarVazio() {
        if (corpo.children.length === 0) {
            const tr = document.createElement('tr');
            tr.dataset.vazio = '1';
            tr.appendChild(celula(corpo.dataset.textoVazio, 'text-body-secondary py-4 text-center'));
            tr.firstChild.colSpan = 8;
            corpo.appendChild(tr);
        }
    }

    async function atualizar(reiniciar) {
        if (emAndamento || (pausado && !reiniciar)) {
            return;
        }
        emAndamento = true;
        try {
            if (reiniciar) {
                ultimoId = null;
                corpo.replaceChildren();
            }
            const parametros = new URLSearchParams();
            if (filtro.value) {
                parametros.set('fonte', filtro.value);
            }
            if (ultimoId !== null) {
                parametros.set('aposId', ultimoId);
            }
            const separador = corpo.dataset.url.includes('?') ? '&' : '?';
            const resposta = await fetch(corpo.dataset.url + separador + parametros.toString(), { headers: { 'Accept': 'application/json' } });
            if (!resposta.ok) {
                return;
            }
            const eventos = await resposta.json();
            if (eventos.length > 0) {
                corpo.querySelector('[data-vazio]')?.remove();
                const destacar = ultimoId !== null;
                const fragmento = document.createDocumentFragment();
                eventos.forEach(evento => fragmento.appendChild(linha(evento, destacar)));
                corpo.prepend(fragmento);
                ultimoId = Math.max(ultimoId ?? 0, ...eventos.map(e => e.id));
                while (corpo.children.length > limiteLinhas) {
                    corpo.lastElementChild.remove();
                }
            }
            mostrarVazio();
        } catch (erro) {
            console.warn('Falha ao atualizar o monitor', erro);
        } finally {
            emAndamento = false;
        }
    }

    botaoPausar.addEventListener('click', () => {
        pausado = !pausado;
        indicador.classList.toggle('pausado', pausado);
        botaoPausar.querySelector('i').className = pausado ? 'bi bi-play' : 'bi bi-pause';
        botaoPausar.querySelector('span').textContent = pausado ? botaoPausar.dataset.textoContinuar : botaoPausar.dataset.textoPausar;
        if (!pausado) {
            atualizar(false);
        }
    });

    filtro.addEventListener('change', () => atualizar(true));

    atualizar(true);
    setInterval(() => atualizar(false), 5000);
})();
