// Alternar o tema claro/escuro (chamado pelo Blazor). O tema inicial e aplicado por tema.js no <head>.
window.crspips = window.crspips || {};

window.crspips.alternarTema = function () {
    const atual = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-bs-theme', atual);
    try {
        localStorage.setItem('crspips-tema', atual);
    } catch (e) {
        // Sem armazenamento local: o tema vale apenas para esta pagina.
    }
    window.crspips.graficos?.aplicarTema();
};
