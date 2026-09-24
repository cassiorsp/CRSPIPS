(function () {
    'use strict';

    const filtro = document.querySelector('[data-filtro-paises]');
    const lista = document.querySelector('[data-lista-paises]');
    const contador = document.querySelector('[data-contador-paises]');
    if (!filtro || !lista) {
        return;
    }

    const normalizar = texto => texto.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();

    filtro.addEventListener('input', () => {
        const termo = normalizar(filtro.value.trim());
        lista.querySelectorAll('.item-pais').forEach(item => {
            item.hidden = termo.length > 0 && !normalizar(item.dataset.nome).includes(termo);
        });
    });

    lista.addEventListener('change', () => {
        contador.textContent = lista.querySelectorAll('input:checked').length;
    });
})();
