
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



