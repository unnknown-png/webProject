// file: src/WebApp/wwwroot/js/app.js
// Organized front-end logic for Gauss Solver demo
(() => {
    // --- Cached DOM selectors ---
    const sizeInput = document.getElementById('sizeInput');
    const matrixWrap = document.getElementById('matrixWrap');
    const randBtn = document.getElementById('rand');
    const clearBtn = document.getElementById('clear');
    const solveBtn = document.getElementById('solve');
    const progressBar = document.getElementById('progressBar');
    const progressText = document.getElementById('progressText');
    const resultEl = document.getElementById('result');
    const historyEl = document.getElementById('history');
    const clearHistoryBtn = document.getElementById('clearHistory');
    const exportBtn = document.getElementById('export');

    // Application state
    let n = Math.max(parseInt(sizeInput && sizeInput.value, 10) || 3, 1);
    const themeToggleBtn = document.getElementById('themeToggle');

    // --- Helpers: render matrix inputs ---
    function buildMatrix(size) {
        // Render matrix as two visually separated columns: coefficients (left) and RHS (right)
        matrixWrap.innerHTML = '';
        const inner = document.createElement('div');
        inner.className = 'matrix-inner';

        // Coefficients table (left)
        const coeffTable = document.createElement('table');
        coeffTable.className = 'matrix coeff-table';
        for (let i = 0; i < size; i++) {
            const tr = document.createElement('tr');
            for (let j = 0; j < size; j++) {
                const td = document.createElement('td');
                const input = document.createElement('input');
                input.type = 'number';
                input.step = 'any';
                input.className = 'coeff';
                input.dataset.row = i;
                input.dataset.col = j;
                // friendly default: identity-like
                input.value = (i === j) ? '1' : '0';
                td.appendChild(input);
                tr.appendChild(td);
            }
            coeffTable.appendChild(tr);
        }

        // RHS column (right)
        const rhsCol = document.createElement('div');
        rhsCol.className = 'rhs-column';
        for (let i = 0; i < size; i++) {
            const wrapper = document.createElement('div');
            wrapper.className = 'rhs-row';
            const rhsIn = document.createElement('input');
            rhsIn.type = 'number';
            rhsIn.step = 'any';
            rhsIn.className = 'rhs';
            rhsIn.dataset.row = i;
            rhsIn.value = '0';
            wrapper.appendChild(rhsIn);
            rhsCol.appendChild(wrapper);
        }

        // wrap coefficients in a column container so it can have the same framing as RHS
        const coeffCol = document.createElement('div');
        coeffCol.className = 'coeff-column';
        coeffCol.appendChild(coeffTable);
        inner.appendChild(coeffCol);
        inner.appendChild(rhsCol);
        matrixWrap.appendChild(inner);
    }

    function renderSummary(size) {
        matrixWrap.innerHTML = '';
        const div = document.createElement('div');
        div.className = 'matrix-summary';
        div.innerHTML = `Матриця та вектор правих частин розміру <strong>${size}×${size}</strong> згенеровані. Поля не відображаються для великих розмірів.`;
        matrixWrap.appendChild(div);
    }

    // Fill with random numbers (range symmetric around zero)
    function randomFill(range = 10) {
        if (n < 10) {
            matrixWrap.querySelectorAll('input.coeff').forEach(i => i.value = (Math.random() * 2 * range - range).toFixed(2));
            matrixWrap.querySelectorAll('input.rhs').forEach(i => i.value = (Math.random() * 2 * range - range).toFixed(2));
        } else {
            // For large sizes we don't render inputs — show a short notice instead
            renderSummary(n);
            progressText.textContent = `Random values generated for ${n}×${n} (not displayed)`;
        }
    }

    function clearMatrix() {
        if (n < 10) {
            matrixWrap.querySelectorAll('input').forEach(i => i.value = '');
        } else {
            renderSummary(n);
            progressText.textContent = `Matrix ${n}×${n} cleared (no visible inputs)`;
        }
    }

    // Read matrix values from DOM into JS arrays
    function readMatrix() {
        const rows = [];
        for (let i = 0; i < n; i++) {
            const row = [];
            for (let j = 0; j < n; j++) {
                const el = matrixWrap.querySelector(`input.coeff[data-row="${i}"][data-col="${j}"]`);
                row.push(Number(el && el.value ? el.value : 0));
            }
            rows.push(row);
        }
        const rhs = [];
        for (let i = 0; i < n; i++) {
            const el = matrixWrap.querySelector(`input.rhs[data-row="${i}"]`);
            rhs.push(Number(el && el.value ? el.value : 0));
        }
        return { rows, rhs };
    }

    // Update progress UI
    function setProgress(p) {
        if (!progressBar) return;
        progressBar.style.width = p + '%';
        progressText.textContent = (p === 0) ? 'idle' : p + '%';
    }

    // -------------------------------
    // Numerical solver: Gaussian elimination with partial pivoting
    // Returns a Promise to allow UI progress updates
    // -------------------------------
    function solveGaussAsync(Ainit, bin, onProgress) {
        return new Promise((resolve, reject) => {
            setTimeout(() => {
                try {
                    const A = Ainit.map(r => r.slice());
                    const b = bin.slice();
                    const N = A.length;

                    for (let k = 0; k < N; k++) {
                        // partial pivot
                        let maxRow = k;
                        for (let i = k + 1; i < N; i++) {
                            if (Math.abs(A[i][k]) > Math.abs(A[maxRow][k])) maxRow = i;
                        }
                        if (Math.abs(A[maxRow][k]) < 1e-12) throw new Error('Матриця вироджена або близька до виродження');
                        if (maxRow !== k) {
                            [A[k], A[maxRow]] = [A[maxRow], A[k]];
                            [b[k], b[maxRow]] = [b[maxRow], b[k]];
                        }

                        for (let i = k + 1; i < N; i++) {
                            const factor = A[i][k] / A[k][k];
                            for (let j = k; j < N; j++) A[i][j] -= factor * A[k][j];
                            b[i] -= factor * b[k];
                        }

                        if (typeof onProgress === 'function') onProgress(Math.round((k + 1) / N * 100));
                    }

                    // back substitution
                    const x = new Array(N).fill(0);
                    for (let i = N - 1; i >= 0; i--) {
                        let s = b[i];
                        for (let j = i + 1; j < N; j++) s -= A[i][j] * x[j];
                        x[i] = s / A[i][i];
                    }
                    resolve(x);
                } catch (err) {
                    reject(err);
                }
            }, 150); // small defer so UI updates between steps
        });
    }

    // -------------------------------
    // History (localStorage)
    // -------------------------------
    const HISTORY_KEY = 'gauss_history_v1';

    function addHistory(entry) {
        const raw = localStorage.getItem(HISTORY_KEY);
        const arr = raw ? JSON.parse(raw) : [];
        arr.unshift(entry);
        arr.splice(20); // keep up to 20
        localStorage.setItem(HISTORY_KEY, JSON.stringify(arr));
        renderHistory();
    }

    function renderHistory() {
        historyEl.innerHTML = '';
        const raw = localStorage.getItem(HISTORY_KEY);
        const list = raw ? JSON.parse(raw) : [];
        if (!list.length) {
            const empty = document.createElement('div');
            empty.className = 'history-item';
            empty.style.opacity = '.6';
            empty.textContent = 'Empty';
            historyEl.appendChild(empty);
            return;
        }

        list.forEach(h => {
            const el = document.createElement('div');
            el.className = 'history-item';
            const left = document.createElement('div');
            left.innerHTML = `<div style="font-weight:600">${h.time}</div><small class="hint">${h.size}×${h.size}</small>`;
            const right = document.createElement('div');
            right.textContent = 'x=' + JSON.stringify(h.result.map(v => Number(v.toFixed(4))));
            el.appendChild(left);
            el.appendChild(right);
            historyEl.appendChild(el);
        });
    }

    // Export history as JSON file
    function exportHistory() {
        const raw = localStorage.getItem(HISTORY_KEY) || '[]';
        const blob = new Blob([raw], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = 'gauss_history.json'; document.body.appendChild(a); a.click(); a.remove(); URL.revokeObjectURL(url);
    }

    // -------------------------------
    // Event wiring
    // -------------------------------
    // size input: allow arbitrary positive integer; if <10 render matrix inputs, else show summary
    if (sizeInput) {
        sizeInput.addEventListener('change', () => {
            let v = Math.max(parseInt(sizeInput.value, 10) || 1, 1);
            // store as string cleanup
            sizeInput.value = v;
            n = v;
            if (n < 10) {
                buildMatrix(n);
            } else {
                renderSummary(n);
            }
            setProgress(0);
            resultEl.hidden = true;
        });
    }

    randBtn.addEventListener('click', () => { randomFill(10); resultEl.hidden = true; setProgress(0); });
    clearBtn.addEventListener('click', () => { clearMatrix(); resultEl.hidden = true; setProgress(0); });

    solveBtn.addEventListener('click', async () => {
        resultEl.hidden = true;
        setProgress(0);
        if (n >= 10) {
            // Too large to render/solve in-browser for now — inform user
            alert(`Matrix ${n}×${n} is too large to render/solve in the browser. Use a smaller size (< 10) or server-side solver.`);
            return;
        }

        const { rows, rhs } = readMatrix();
        // validation
        if (rows.some(r => r.some(c => !isFinite(c))) || rhs.some(v => !isFinite(v))) {
            alert('Please enter valid numbers');
            return;
        }

        progressText.textContent = 'starting...';
        try {
            const x = await solveGaussAsync(rows, rhs, p => setProgress(p));
            setProgress(100);
            resultEl.hidden = false;
            resultEl.textContent = 'Solution: [' + x.map(v => Number(v.toFixed(6))).join(', ') + ']';
            addHistory({ time: new Date().toLocaleString(), size: n, result: x });
        } catch (err) {
            setProgress(0);
            resultEl.hidden = false;
            resultEl.textContent = 'Error: ' + (err && err.message ? err.message : String(err));
        }
    });

    clearHistoryBtn.addEventListener('click', () => { localStorage.removeItem(HISTORY_KEY); renderHistory(); });
    exportBtn.addEventListener('click', exportHistory);

    // Theme toggle: persist choice in localStorage
    const THEME_KEY = 'gauss_theme';
    function applyTheme(theme) {
        try {
            if (theme === 'light') {
                document.body.setAttribute('data-theme', 'light');
                if (themeToggleBtn) themeToggleBtn.textContent = 'Light';
            } else {
                document.body.removeAttribute('data-theme');
                if (themeToggleBtn) themeToggleBtn.textContent = 'Dark';
            }
        } catch (e) { /* ignore */ }
    }

    // init theme from storage
    const savedTheme = localStorage.getItem(THEME_KEY) || 'dark';
    applyTheme(savedTheme === 'light' ? 'light' : 'dark');

    if (themeToggleBtn) {
        themeToggleBtn.addEventListener('click', () => {
            const cur = document.body.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
            const next = cur === 'light' ? 'dark' : 'light';
            applyTheme(next);
            localStorage.setItem(THEME_KEY, next);
        });
    }

    // --- Initialize ---
    if (n < 10) buildMatrix(n);
    else renderSummary(n);
    renderHistory();
    setProgress(0);

    // Topbar "Home" link: smooth scroll to top (keeps markup clean, no inline JS)
    const homeLink = document.getElementById('homeLink');
    if (homeLink) {
        homeLink.addEventListener('click', (e) => {
            e.preventDefault();
            window.scrollTo({ top: 0, behavior: 'smooth' });
        });
    }

    // Topbar "History" link: smooth scroll down to the history section
    const historyNavLink = document.querySelector('a.nav-link[href="#history-section"]');
    if (historyNavLink) {
        historyNavLink.addEventListener('click', (e) => {
            e.preventDefault();
            const target = document.getElementById('history-section');
            if (target) {
                // ensure the wrap's header is visible — scroll the section into view
                target.scrollIntoView({ behavior: 'smooth', block: 'start' });
                // optionally focus for accessibility
                try { target.setAttribute('tabindex', '-1'); target.focus({ preventScroll: true }); } catch (err) { /* ignore */ }
            } else {
                // fallback: scroll to bottom
                window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
            }
        });
    }

})();
