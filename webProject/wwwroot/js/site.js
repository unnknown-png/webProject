// GAUSS SOLVER - Frontend Logic with SignalR Progress Tracking
(() => {
    
    // DOM ELEMENTS
    const $ = id => document.getElementById(id);
    const sizeInput = $('sizeInput');
    const matrixWrap = $('matrixWrap');
    const progressBar = $('progressBar');
    const progressText = $('progressText');
    const progressPercent = $('progressPercent');
    const progressStage = $('progressStage');
    const cancelBtn = $('cancelBtn');
    const resultEl = $('result');
    const historyEl = $('history');

    let size = Math.max(parseInt(sizeInput?.value, 10) || 3, 1);
    let matrixId = null;
    let currentTaskId = null;
    let connection = null;

    // SIGNALR CONNECTION
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not loaded yet, retrying...');
            setTimeout(initSignalR, 100);
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/progressHub")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveProgress", (taskId, percent, stage, message) => {
            if (taskId === currentTaskId) {
                setProgress(percent, message, stage);
            }
        });

        connection.start()
            .catch(err => console.error("SignalR connection error:", err));
    }

    // Initialize SignalR when script loads
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initSignalR);
    } else {
        initSignalR();
    }

    // UTILITY FUNCTIONS
    function setProgress(percent, text, stage = null) {
        progressBar.style.width = percent + '%';
        if (progressPercent) progressPercent.textContent = percent + '%';
        progressText.textContent = text || (percent === 0 ? 'Idle' : percent + '%');
        
        if (stage && progressStage) {
            const stageLabels = {
                'Initializing': 'Initializing...',
                'ForwardElimination': 'Forward Elimination',
                'BackSubstitution': 'Back Substitution',
                'Finalizing': 'Finalizing...',
                'Completed': 'Completed!',
                'Cancelled': 'Cancelled',
                'Failed': 'Failed'
            };
            progressStage.textContent = stageLabels[stage] || stage;
        }

        // Show/hide cancel button
        if (cancelBtn) {
            if (percent > 0 && percent < 100) {
                cancelBtn.style.display = 'inline-block';
            } else {
                cancelBtn.style.display = 'none';
            }
        }
    }

    function showResult(message, isError = false) {
        resultEl.hidden = false;
        resultEl.textContent = message;
        resultEl.style.color = isError ? '#ff6b6b' : '';
    }

    // MATRIX UI
    function buildMatrix(n) {
        matrixWrap.innerHTML = '';
        const inner = document.createElement('div');
        inner.className = 'matrix-inner';

        // Coefficients table
        const coeffTable = document.createElement('table');
        coeffTable.className = 'matrix coeff-table';
        for (let i = 0; i < n; i++) {
            const tr = document.createElement('tr');
            for (let j = 0; j < n; j++) {
                const td = document.createElement('td');
                const inp = document.createElement('input');
                inp.type = 'number';
                inp.step = 'any';
                inp.className = 'coeff';
                inp.dataset.row = i;
                inp.dataset.col = j;
                inp.value = i === j ? '1' : '0';
                td.appendChild(inp);
                tr.appendChild(td);
            }
            coeffTable.appendChild(tr);
        }

        // RHS column
        const rhsCol = document.createElement('div');
        rhsCol.className = 'rhs-column';
        for (let i = 0; i < n; i++) {
            const wrapper = document.createElement('div');
            wrapper.className = 'rhs-row';
            const inp = document.createElement('input');
            inp.type = 'number';
            inp.step = 'any';
            inp.className = 'rhs';
            inp.dataset.row = i;
            inp.value = '0';
            wrapper.appendChild(inp);
            rhsCol.appendChild(wrapper);
        }

        const coeffCol = document.createElement('div');
        coeffCol.className = 'coeff-column';
        coeffCol.appendChild(coeffTable);
        inner.appendChild(coeffCol);
        inner.appendChild(rhsCol);
        matrixWrap.appendChild(inner);
    }

    function showSummary(n, msg) {
        matrixWrap.innerHTML = `<div class="matrix-summary">${msg || `Matrix ${n}×${n} - too large to display.`}</div>`;
    }

    function readMatrix() {
        const rows = [], rhs = [];
        for (let i = 0; i < size; i++) {
            const row = [];
            for (let j = 0; j < size; j++) {
                const el = matrixWrap.querySelector(`input.coeff[data-row="${i}"][data-col="${j}"]`);
                row.push(Number(el?.value || 0));
            }
            rows.push(row);
            const rhsEl = matrixWrap.querySelector(`input.rhs[data-row="${i}"]`);
            rhs.push(Number(rhsEl?.value || 0));
        }
        return { rows, rhs };
    }

    // HISTORY
    async function loadHistory() {
        try {
            const res = await fetch('/api/matrix/history?limit=20');
            const data = await res.json();
            if (data.success && data.history) renderHistory(data.history);
        } catch (err) {
            historyEl.innerHTML = '<div class="history-item" style="opacity:.6">Failed to load</div>';
        }
    }

    function renderHistory(list) {
        historyEl.innerHTML = '';
        if (!list.length) {
            historyEl.innerHTML = '<div class="history-item" style="opacity:.6">Empty</div>';
            return;
        }
        list.forEach(h => {
            const el = document.createElement('div');
            el.className = 'history-item';
            const solution = h.success ? JSON.parse(h.solution) : null;
            
            // Format time (already in Kyiv timezone from backend)
            const date = new Date(h.createdAt);
            const kyivTime = date.toLocaleString('uk-UA', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                hour: '2-digit',
                minute: '2-digit',
                second: '2-digit'
            });
            
            el.innerHTML = `
                <div>
                    <div style="font-weight:600">${kyivTime}</div>
                    <small class="hint">${h.size}×${h.size}</small>
                </div>
                <div style="color:${h.success ? '' : '#ff6b6b'}">
                    ${h.success 
                        ? (solution.length <= 10 
                            ? `x=${JSON.stringify(solution.map(v => v.toFixed(4)))}` 
                            : `Solved (${solution.length} values)`)
                        : `Error: ${h.errorMessage || 'Unknown'}`}
                </div>
            `;
            historyEl.appendChild(el);
        });
    }

    // EVENT HANDLERS
    if (sizeInput) {
        sizeInput.addEventListener('change', () => {
            size = Math.max(parseInt(sizeInput.value, 10) || 1, 1);
            sizeInput.value = size;
            matrixId = null;
            size < 10 ? buildMatrix(size) : showSummary(size);
            setProgress(0);
            resultEl.hidden = true;
        });
    }

    // Cancel button
    if (cancelBtn) {
        cancelBtn.addEventListener('click', async () => {
            if (!currentTaskId) return;
            
            try {
                const res = await fetch(`/api/matrix/cancel/${currentTaskId}`, {
                    method: 'POST'
                });
                const data = await res.json();
                if (data.success) {
                    setProgress(0, 'Cancelling...', 'Cancelled');
                    cancelBtn.disabled = true;
                }
            } catch (err) {
                console.error('Cancel error:', err);
            }
        });
    }

    // Random generation
    $('rand').addEventListener('click', async () => {
        resultEl.hidden = true;
        setProgress(0, 'Generating...');
        try {
            const res = await fetch('/api/matrix/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ size, minValue: -200, maxValue: 200 })
            });
            const data = await res.json();
            
            if (!data.success) {
                showResult(data.error || 'Failed', true);
                setProgress(0);
                return;
            }

            if (size < 10) {
                for (let i = 0; i < size; i++) {
                    for (let j = 0; j < size; j++) {
                        const el = matrixWrap.querySelector(`input.coeff[data-row="${i}"][data-col="${j}"]`);
                        if (el) el.value = data.coefficients[i][j].toFixed(2);
                    }
                    const rhsEl = matrixWrap.querySelector(`input.rhs[data-row="${i}"]`);
                    if (rhsEl) rhsEl.value = data.rightHandSide[i].toFixed(2);
                }
                setProgress(100, 'Ready');
            } else {
                matrixId = data.matrixId;
                showSummary(size, `✓ ${data.message}<br><small>Click "Solve" to compute.</small>`);
                setProgress(100, 'Ready');
            }
        } catch (err) {
            showResult('Server error', true);
            setProgress(0);
        }
    });

    // Clear
    $('clear').addEventListener('click', () => {
        if (size < 10) matrixWrap.querySelectorAll('input').forEach(i => i.value = '');
        else { matrixId = null; showSummary(size); }
        
        // Clear results and progress
        resultEl.hidden = true;
        setProgress(0, 'Idle');
        
        // Clear progress stage
        if (progressStage) progressStage.textContent = '';
    });

    // Solve
    $('solve').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        currentTaskId = crypto.randomUUID ? crypto.randomUUID() : Date.now().toString();
        setProgress(0, 'Starting...', 'Initializing');
        if (cancelBtn) cancelBtn.disabled = false;
        
        try {
            if (size < 10) {
                const { rows, rhs } = readMatrix();
                
                const res = await fetch('/api/matrix/solve', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'X-Task-Id': currentTaskId
                    },
                    body: JSON.stringify({ 
                        coefficients: rows, 
                        rightHandSide: rhs,
                        taskId: currentTaskId
                    })
                });
                
                const data = await res.json();
                await new Promise(resolve => setTimeout(resolve, 500));
                
                showResult(data.success 
                    ? `Solution: [${data.solution.map(v => v.toFixed(6)).join(', ')}]`
                    : `Error: ${data.error}`, !data.success);
                
                currentTaskId = null;
            } else {
                if (!matrixId) { 
                    alert('Generate matrix first!'); 
                    return; 
                }
                
                const res = await fetch('/api/matrix/solve-stored', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'X-Task-Id': currentTaskId
                    },
                    body: JSON.stringify({ 
                        matrixId,
                        taskId: currentTaskId
                    })
                });
                const data = await res.json();
                await new Promise(resolve => setTimeout(resolve, 500));
                
                if (data.success) {
                    showResult(`✓ ${data.solutionSummary}`);
                    matrixId = null;
                    showSummary(size, `Solved! Check history.`);
                } else {
                    showResult(`Error: ${data.error}`, true);
                }
                
                currentTaskId = null;
            }
            loadHistory();
        } catch (err) {
            showResult('Server error', true);
            setProgress(0);
            currentTaskId = null;
        }
    });

    // History controls
    $('clearHistory').addEventListener('click', async () => {
        try {
            await fetch('/api/matrix/history', { method: 'DELETE' });
            loadHistory();
        } catch (err) { alert('Failed to clear'); }
    });

    $('export').addEventListener('click', () => {
        window.location.href = '/api/matrix/history/export';
    });

    // THEME
    const THEME_KEY = 'gauss_theme';
    function applyTheme(theme) {
        if (theme === 'light') {
            document.body.setAttribute('data-theme', 'light');
            $('themeToggle').textContent = 'Light';
        } else {
            document.body.removeAttribute('data-theme');
            $('themeToggle').textContent = 'Dark';
        }
    }
    const savedTheme = localStorage.getItem(THEME_KEY) || 'dark';
    applyTheme(savedTheme);
    $('themeToggle').addEventListener('click', () => {
        const cur = document.body.getAttribute('data-theme') === 'light' ? 'light' : 'dark';
        const next = cur === 'light' ? 'dark' : 'light';
        applyTheme(next);
        localStorage.setItem(THEME_KEY, next);
    });

    // NAVIGATION
    const homeLink = $('homeLink');
    if (homeLink) homeLink.addEventListener('click', (e) => {
        e.preventDefault();
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });
    
    const historyLink = document.querySelector('a.nav-link[href="#history-section"]');
    if (historyLink) historyLink.addEventListener('click', (e) => {
        e.preventDefault();
        const target = $('history-section');
        if (target) target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });

    // INITIALIZATION
    size < 10 ? buildMatrix(size) : showSummary(size);
    loadHistory();
    setProgress(0);
})();

// ====================
// PASSWORD TOGGLE (for Login/Register pages)
// ====================
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
