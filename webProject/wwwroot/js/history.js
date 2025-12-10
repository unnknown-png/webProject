
const HistoryModule = (() => {
    let historyEl = null;

    function init(historyElement) {
        historyEl = historyElement;
    }

    async function loadHistory() {
        try {
            const res = await fetch('/api/history?limit=20');
            const data = await res.json();
            if (data.success && data.history) {
                renderHistory(data.history);
            }
        } catch (err) {
            if (historyEl) {
                historyEl.innerHTML = '<div class="history-item" style="opacity:.6">Failed to load</div>';
            }
        }
    }

    function renderHistory(list) {
        if (!historyEl) return;
        
        historyEl.innerHTML = '';
        if (!list.length) {
            historyEl.innerHTML = '<div class="history-item" style="opacity:.6">Empty</div>';
            return;
        }
        
        list.forEach(h => {
            const el = document.createElement('div');
            el.className = 'history-item';
            const solution = h.success ? JSON.parse(h.solution) : null;
            
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

    async function clearHistory() {
        try {
            await fetch('/api/history', { method: 'DELETE' });
            await loadHistory();
        } catch (err) {
            alert('Failed to clear history');
        }
    }

    function exportHistory() {
        window.location.href = '/api/history/export';
    }

    return {
        init,
        loadHistory,
        renderHistory,
        clearHistory,
        exportHistory
    };
})();

