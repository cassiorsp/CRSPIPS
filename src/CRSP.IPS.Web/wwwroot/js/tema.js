// Aplicado no <head> para evitar o "piscar" de tema claro ao carregar a pagina.
(function () {
    let tema = null;
    try {
        tema = localStorage.getItem('crspips-tema');
    } catch (e) {
        tema = null;
    }
    if (!tema) {
        tema = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-bs-theme', tema);
})();
