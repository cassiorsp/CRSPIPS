(function () {
    'use strict';

    document.querySelector('[data-alternar-tema]')?.addEventListener('click', () => {
        const atual = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-bs-theme', atual);
        try {
            localStorage.setItem('crspips-tema', atual);
        } catch (e) {
            // Sem armazenamento local: o tema vale apenas para esta pagina.
        }
        document.dispatchEvent(new CustomEvent('crspips:tema'));
    });

    document.querySelector('[data-alternar-menu]')?.addEventListener('click', () => {
        document.getElementById('barraLateral')?.classList.toggle('aberta');
    });

    // Confirmacao antes de enviar formularios sensiveis.
    document.querySelectorAll('form[data-confirmar]').forEach(form => {
        form.addEventListener('submit', evento => {
            if (!window.confirm(form.dataset.confirmar)) {
                evento.preventDefault();
            }
        });
    });

    // Avisa quando ha alteracoes nao salvas em um formulario e o usuario envia outro ou sai da pagina.
    document.querySelectorAll('form[data-avisar-alteracoes]').forEach(formulario => {
        let alterado = false;
        let enviando = false;
        formulario.addEventListener('input', () => alterado = true);
        formulario.addEventListener('change', () => alterado = true);
        formulario.addEventListener('submit', () => enviando = true);

        document.querySelectorAll('form').forEach(outro => {
            if (outro === formulario) {
                return;
            }
            outro.addEventListener('submit', evento => {
                if (alterado && !window.confirm(formulario.dataset.avisarAlteracoes)) {
                    evento.preventDefault();
                    evento.stopImmediatePropagation();
                } else {
                    enviando = true;
                }
            });
        });

        window.addEventListener('beforeunload', evento => {
            if (alterado && !enviando) {
                evento.preventDefault();
            }
        });
    });

    // Switches que enviam o formulario ao mudar (ex.: ativar/desativar regra).
    document.querySelectorAll('[data-enviar-ao-mudar]').forEach(campo => {
        campo.addEventListener('change', () => campo.form?.submit());
    });

    // Modais genericos: copia data-id / data-ip do botao para os campos marcados com data-campo.
    document.querySelectorAll('.modal').forEach(modal => {
        modal.addEventListener('show.bs.modal', evento => {
            const origem = evento.relatedTarget;
            if (!origem) {
                return;
            }
            modal.querySelectorAll('[data-campo]').forEach(campo => {
                const valor = origem.dataset[campo.dataset.campo];
                if (valor === undefined) {
                    return;
                }
                if (campo.tagName === 'INPUT') {
                    campo.value = valor;
                } else {
                    campo.textContent = valor;
                }
            });
        });
    });

    // Selecao em lote na grid de bloqueios.
    const selecoes = () => Array.from(document.querySelectorAll('[data-selecao]:checked'));
    const atualizarSelecao = () => {
        const quantidade = selecoes().length;
        document.querySelectorAll('[data-requer-selecao]').forEach(botao => botao.disabled = quantidade === 0);
        document.querySelectorAll('[data-contador-selecao]').forEach(contador => contador.textContent = quantidade);
    };
    document.querySelectorAll('[data-selecao]').forEach(caixa => caixa.addEventListener('change', atualizarSelecao));
    document.querySelector('[data-selecionar-todos]')?.addEventListener('change', evento => {
        document.querySelectorAll('[data-selecao]').forEach(caixa => caixa.checked = evento.target.checked);
        atualizarSelecao();
    });
    document.querySelector('[data-form-selecao]')?.addEventListener('submit', evento => {
        const form = evento.target;
        form.querySelectorAll('input[name="ids"]').forEach(campo => campo.remove());
        selecoes().forEach(caixa => {
            const campo = document.createElement('input');
            campo.type = 'hidden';
            campo.name = 'ids';
            campo.value = caixa.value;
            form.appendChild(campo);
        });
    });
})();
