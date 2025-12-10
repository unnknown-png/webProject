
const NavigationModule = (() => {
    function setupNavigation() {
        const homeLink = document.getElementById('homeLink');
        if (homeLink) {
            homeLink.addEventListener('click', (e) => {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });
        }
        
        const historyLink = document.querySelector('a.nav-link[href="#history-section"]');
        if (historyLink) {
            historyLink.addEventListener('click', (e) => {
                e.preventDefault();
                const historySection = document.getElementById('history-section');
                if (historySection) {
                    historySection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        }
    }

    return {
        setupNavigation
    };
})();

