// NAVIGATION MODULE
// Handles page navigation and smooth scrolling

const NavigationModule = (() => {
    function setupNavigation() {
        // Home link - scroll to top
        const homeLink = document.getElementById('homeLink');
        if (homeLink) {
            homeLink.addEventListener('click', (e) => {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: 'smooth' });
            });
        }
        
        // History link - scroll to history section
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

