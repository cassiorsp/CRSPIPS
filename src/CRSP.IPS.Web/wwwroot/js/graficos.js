// Graficos do dashboard com Chart.js, chamados pelos componentes Blazor (GraficoLinha, GraficoRosca, GraficoBarras).
// Cada <canvas> guarda o proprio grafico: novas chamadas so atualizam os dados, sem recriar nem animar de novo.
window.crspips = window.crspips || {};

(function () {
    'use strict';

    const paleta = ['#3b82f6', '#e5484d', '#f5a524', '#22c55e', '#8b5cf6', '#14b8a6', '#ec4899', '#64748b'];

    function cores() {
        const estilo = getComputedStyle(document.documentElement);
        return {
            texto: estilo.getPropertyValue('--bs-secondary-color').trim() || '#64748b',
            grade: estilo.getPropertyValue('--crs-borda').trim() || '#e3e8ef',
            cartao: estilo.getPropertyValue('--crs-cartao').trim() || '#ffffff',
            info: '#3b82f6',
            perigo: '#e5484d'
        };
    }

    function montar(canvas, criarConfiguracao) {
        if (!canvas || typeof Chart === 'undefined') {
            return;
        }

        const c = cores();
        Chart.defaults.color = c.texto;
        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        const configuracao = criarConfiguracao(c);
        canvas._criarConfiguracao = criarConfiguracao;

        if (canvas._grafico) {
            canvas._grafico.data = configuracao.data;
            canvas._grafico.options = configuracao.options;
            canvas._grafico.update('none');
        } else {
            canvas._grafico = new Chart(canvas, configuracao);
        }
    }

    const dica = c => ({
        backgroundColor: c.cartao,
        titleColor: c.texto,
        bodyColor: c.texto,
        borderColor: c.grade,
        borderWidth: 1,
        padding: 10,
        boxPadding: 4,
        usePointStyle: true
    });

    function degrade(canvas, cor) {
        const contexto = canvas.getContext('2d');
        const gradiente = contexto.createLinearGradient(0, 0, 0, canvas.clientHeight || 280);
        gradiente.addColorStop(0, cor + '55');
        gradiente.addColorStop(1, cor + '00');
        return gradiente;
    }

    window.crspips.graficos = {
        linha(canvas, dados) {
            montar(canvas, c => ({
                type: 'line',
                data: {
                    labels: dados.rotulos,
                    datasets: [
                        {
                            label: dados.rotuloEventos,
                            data: dados.eventos,
                            borderColor: c.info,
                            backgroundColor: degrade(canvas, c.info),
                            fill: true,
                            tension: .35,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            borderWidth: 2
                        },
                        {
                            label: dados.rotuloBloqueios,
                            data: dados.bloqueios,
                            borderColor: c.perigo,
                            backgroundColor: c.perigo,
                            tension: .35,
                            pointRadius: 2,
                            pointHoverRadius: 5,
                            borderWidth: 2,
                            yAxisID: 'bloqueios'
                        }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
                        tooltip: dica(c)
                    },
                    scales: {
                        x: { grid: { display: false }, ticks: { color: c.texto, maxRotation: 0, autoSkip: true, maxTicksLimit: 12 } },
                        y: {
                            beginAtZero: true,
                            grid: { color: c.grade },
                            ticks: { color: c.info, precision: 0 },
                            title: { display: true, text: dados.rotuloEventos, color: c.info }
                        },
                        // Eixo proprio: com dezenas de eventos, os poucos bloqueios ficariam achatados no zero.
                        bloqueios: {
                            position: 'right',
                            beginAtZero: true,
                            suggestedMax: 4,
                            grid: { drawOnChartArea: false },
                            ticks: { color: c.perigo, precision: 0 },
                            title: { display: true, text: dados.rotuloBloqueios, color: c.perigo }
                        }
                    }
                }
            }));
        },

        rosca(canvas, dados) {
            montar(canvas, c => ({
                type: 'doughnut',
                data: {
                    labels: dados.rotulos,
                    datasets: [{ data: dados.valores, backgroundColor: paleta, borderColor: c.cartao, borderWidth: 2, hoverOffset: 6 }]
                },
                options: {
                    maintainAspectRatio: false,
                    cutout: '68%',
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
                        tooltip: dica(c)
                    }
                }
            }));
        },

        barras(canvas, dados) {
            montar(canvas, c => ({
                type: 'bar',
                data: {
                    labels: dados.rotulos,
                    datasets: [{ label: dados.rotuloSerie, data: dados.valores, backgroundColor: c.perigo, borderRadius: 4, maxBarThickness: 22 }]
                },
                options: {
                    indexAxis: 'y',
                    maintainAspectRatio: false,
                    plugins: { legend: { display: false }, tooltip: dica(c) },
                    scales: {
                        x: { beginAtZero: true, grid: { color: c.grade }, ticks: { color: c.texto, precision: 0 } },
                        y: { grid: { display: false }, ticks: { color: c.texto } }
                    }
                }
            }));
        },

        // Requisicoes por intervalo: barras empilhadas (sucesso, erro do cliente 4xx, erro do servidor 5xx).
        requisicoes(canvas, dados) {
            montar(canvas, c => ({
                type: 'bar',
                data: {
                    labels: dados.rotulos,
                    datasets: [
                        { label: dados.rotuloSucesso, data: dados.sucesso, backgroundColor: '#22c55e', borderRadius: 2 },
                        { label: dados.rotuloErroCliente, data: dados.erroCliente, backgroundColor: '#f5a524', borderRadius: 2 },
                        { label: dados.rotuloErroServidor, data: dados.erroServidor, backgroundColor: c.perigo, borderRadius: 2 }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
                        tooltip: dica(c)
                    },
                    scales: {
                        x: { stacked: true, grid: { display: false }, ticks: { color: c.texto, maxRotation: 0, autoSkip: true, maxTicksLimit: 12 } },
                        y: { stacked: true, beginAtZero: true, grid: { color: c.grade }, ticks: { color: c.texto, precision: 0 } }
                    }
                }
            }));
        },

        // Memoria de um application pool: media (area) entre a minima e a maxima (linhas tracejadas), em MB.
        memoria(canvas, dados) {
            montar(canvas, c => ({
                type: 'line',
                data: {
                    labels: dados.rotulos,
                    datasets: [
                        {
                            label: dados.rotuloMedia,
                            data: dados.media,
                            borderColor: c.info,
                            backgroundColor: degrade(canvas, c.info),
                            fill: true,
                            tension: .3,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            borderWidth: 2
                        },
                        {
                            label: dados.rotuloMaxima,
                            data: dados.maxima,
                            borderColor: c.perigo,
                            borderDash: [5, 4],
                            tension: .3,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            borderWidth: 1.5
                        },
                        {
                            label: dados.rotuloMinima,
                            data: dados.minima,
                            borderColor: '#22c55e',
                            borderDash: [5, 4],
                            tension: .3,
                            pointRadius: 0,
                            pointHoverRadius: 4,
                            borderWidth: 1.5
                        }
                    ]
                },
                options: {
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8 } },
                        tooltip: {
                            ...dica(c),
                            callbacks: { label: item => `${item.dataset.label}: ${Number(item.parsed.y).toLocaleString(undefined, { maximumFractionDigits: 0 })} MB` }
                        }
                    },
                    scales: {
                        x: { grid: { display: false }, ticks: { color: c.texto, maxRotation: 0, autoSkip: true, maxTicksLimit: 12 } },
                        y: { beginAtZero: true, grid: { color: c.grade }, ticks: { color: c.texto, callback: v => v + ' MB' } }
                    }
                }
            }));
        },

        destruir(canvas) {
            if (canvas && canvas._grafico) {
                canvas._grafico.destroy();
                canvas._grafico = null;
            }
        },

        // Ao trocar o tema, redesenha com as cores novas (grade, textos e fundo das dicas).
        aplicarTema() {
            document.querySelectorAll('canvas').forEach(canvas => {
                if (canvas._grafico && canvas._criarConfiguracao) {
                    montar(canvas, canvas._criarConfiguracao);
                }
            });
        }
    };
})();
