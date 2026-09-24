(function () {
    'use strict';

    const conteudo = document.getElementById('conteudoDashboard');
    const barra = document.getElementById('barraAtualizacao');
    if (!conteudo || !barra || typeof Chart === 'undefined') {
        return;
    }

    const cores = {
        perigo: '#e5484d',
        info: '#3b82f6',
        paleta: ['#3b82f6', '#e5484d', '#f5a524', '#22c55e', '#8b5cf6']
    };
    const graficos = [];
    const intervalo = Number(barra.dataset.intervalo || 30) * 1000;
    const texto = document.getElementById('textoAtualizacao');
    const indicador = document.getElementById('indicadorDashboard');
    const botaoPausar = document.getElementById('botaoPausarDashboard');
    let pausado = false;
    let atualizando = false;
    let ultimaAtualizacao = Date.now();

    function corTexto() {
        return getComputedStyle(document.body).getPropertyValue('--bs-secondary-color').trim() || '#64748b';
    }

    function corGrade() {
        return getComputedStyle(document.documentElement).getPropertyValue('--crs-borda').trim() || '#e3e8ef';
    }

    function lerDados() {
        const elemento = document.getElementById('dadosDashboard');
        return elemento ? JSON.parse(elemento.textContent) : null;
    }

    function criarGraficos(animar) {
        graficos.forEach(grafico => grafico.destroy());
        graficos.length = 0;

        const dados = lerDados();
        if (!dados) {
            return;
        }

        Chart.defaults.color = corTexto();
        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        const animacao = animar ? {} : { animation: false };

        const linha = document.getElementById('graficoLinha');
        if (linha) {
            graficos.push(new Chart(linha, {
                type: 'line',
                data: {
                    labels: dados.horas,
                    datasets: [
                        {
                            label: dados.rotuloEventos,
                            data: dados.eventos,
                            borderColor: cores.info,
                            backgroundColor: 'rgba(59,130,246,.12)',
                            fill: true,
                            tension: .35,
                            pointRadius: 0,
                            borderWidth: 2
                        },
                        {
                            label: dados.rotuloBloqueios,
                            data: dados.bloqueios,
                            borderColor: cores.perigo,
                            backgroundColor: cores.perigo,
                            tension: .35,
                            pointRadius: 2,
                            borderWidth: 2,
                            yAxisID: 'bloqueios'
                        }
                    ]
                },
                options: {
                    ...animacao,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: { legend: { position: 'bottom' } },
                    scales: {
                        x: { grid: { display: false }, ticks: { color: corTexto(), maxRotation: 0, autoSkip: true } },
                        y: {
                            beginAtZero: true,
                            grid: { color: corGrade() },
                            ticks: { color: cores.info, precision: 0 },
                            title: { display: true, text: dados.rotuloEventos, color: cores.info }
                        },
                        // Eixo proprio: com dezenas de eventos por hora, os poucos bloqueios ficariam achatados no zero.
                        bloqueios: {
                            position: 'right',
                            beginAtZero: true,
                            suggestedMax: 5,
                            grid: { drawOnChartArea: false },
                            ticks: { color: cores.perigo, precision: 0 },
                            title: { display: true, text: dados.rotuloBloqueios, color: cores.perigo }
                        }
                    }
                }
            }));
        }

        const fontes = document.getElementById('graficoFontes');
        if (fontes) {
            graficos.push(new Chart(fontes, {
                type: 'doughnut',
                data: {
                    labels: dados.fontes,
                    datasets: [{ data: dados.fontesQuantidade, backgroundColor: cores.paleta, borderWidth: 0 }]
                },
                options: { ...animacao, maintainAspectRatio: false, cutout: '65%', plugins: { legend: { position: 'bottom' } } }
            }));
        }

        const paises = document.getElementById('graficoPaises');
        if (paises) {
            graficos.push(new Chart(paises, {
                type: 'bar',
                data: {
                    labels: dados.paises,
                    datasets: [{ label: dados.rotuloBloqueios, data: dados.paisesQuantidade, backgroundColor: cores.perigo, borderRadius: 4, maxBarThickness: 22 }]
                },
                options: {
                    ...animacao,
                    indexAxis: 'y',
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false } },
                    scales: {
                        x: { beginAtZero: true, grid: { color: corGrade() }, ticks: { color: corTexto(), precision: 0 } },
                        y: { grid: { display: false }, ticks: { color: corTexto() } }
                    }
                }
            }));
        }
    }

    // Busca a propria pagina e troca somente o conteudo do dashboard: o HTML continua sendo gerado pelo servidor.
    async function atualizar() {
        if (atualizando || document.hidden) {
            return;
        }
        atualizando = true;
        try {
            const resposta = await fetch(window.location.href, { headers: { 'Accept': 'text/html' }, cache: 'no-store' });
            if (resposta.redirected || !resposta.ok) {
                if (resposta.redirected) {
                    window.location.reload();
                }
                throw new Error('HTTP ' + resposta.status);
            }

            const documento = new DOMParser().parseFromString(await resposta.text(), 'text/html');
            const novo = documento.getElementById('conteudoDashboard');
            if (!novo) {
                throw new Error('Conteudo do dashboard nao encontrado');
            }

            conteudo.replaceChildren(...Array.from(novo.childNodes).map(no => document.importNode(no, true)));
            criarGraficos(false);
            ultimaAtualizacao = Date.now();
            atualizarTexto();
        } catch (erro) {
            texto.textContent = barra.dataset.textoFalha;
            console.warn('Falha ao atualizar o dashboard', erro);
        } finally {
            atualizando = false;
        }
    }

    function atualizarTexto() {
        const segundos = Math.round((Date.now() - ultimaAtualizacao) / 1000);
        texto.textContent = segundos < 5 ? barra.dataset.textoAgora : barra.dataset.textoSegundos.replace('{0}', segundos);
    }

    botaoPausar.addEventListener('click', () => {
        pausado = !pausado;
        indicador.classList.toggle('pausado', pausado);
        botaoPausar.querySelector('i').className = pausado ? 'bi bi-play' : 'bi bi-pause';
        botaoPausar.querySelector('span').textContent = pausado ? barra.dataset.textoContinuar : barra.dataset.textoPausar;
    });

    document.getElementById('botaoAtualizarDashboard').addEventListener('click', atualizar);

    // Ao voltar para a aba depois de um tempo, atualiza imediatamente.
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden && !pausado && Date.now() - ultimaAtualizacao >= intervalo) {
            atualizar();
        }
    });

    setInterval(() => {
        if (!pausado && Date.now() - ultimaAtualizacao >= intervalo) {
            atualizar();
        }
        if (!atualizando) {
            atualizarTexto();
        }
    }, 5000);

    criarGraficos(true);
    document.addEventListener('crspips:tema', () => criarGraficos(false));
})();
