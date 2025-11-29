// THEME AND NAVIGATION MODULE
// Handles theme switching, navigation, and utility functions

const ThemeModule = (() => {
    const THEME_KEY = 'gauss_theme';
    let themeToggleBtn = null;

    function init(themeToggleElement) {
        themeToggleBtn = themeToggleElement;
        const savedTheme = localStorage.getItem(THEME_KEY) || 'dark';
        applyTheme(savedTheme);
    }

    function applyTheme(theme) {
        if (theme === 'light') {
            document.body.setAttribute('data-theme', 'light');
            if (themeToggleBtn) themeToggleBtn.textContent = 'Light';
        } else {
            document.body.removeAttribute('data-theme');
            if (themeToggleBtn) themeToggleBtn.textContent = 'Dark';
        }
    }

    function toggleTheme() {
        const cur = document.body.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
        const next = cur === 'light' ? 'dark' : 'light';
        applyTheme(next);
        localStorage.setItem(THEME_KEY, next);
    }

    return {
        init,
        applyTheme,
        toggleTheme
    };
})();

// NAVIGATION MODULE
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
                const target = document.getElementById('history-section');
                if (target) {
                    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                }
            });
        }
    }

    return {
        setupNavigation
    };
})();

// PASSWORD TOGGLE (for Login/Register pages)
function togglePassword(inputId, button) {
    const input = document.getElementById(inputId);
    
    if (input.type === 'password') {
        input.type = 'text';
        button.textContent = 'Hide';
        button.setAttribute('aria-label', 'Hide password');
    } else {
        input.type = 'password';
        button.textContent = 'Show';
        button.setAttribute('aria-label', 'Show password');
    }
}

