
const MatrixModule = (() => {
    let matrixWrap = null;
    let size = 5;
    let matrixId = null;

    function init(matrixWrapElement, initialSize) {
        matrixWrap = matrixWrapElement;
        size = initialSize;
    }

    function setSize(newSize) {
        size = newSize;
    }

    function getSize() {
        return size;
    }

    function setMatrixId(id) {
        matrixId = id;
    }

    function getMatrixId() {
        return matrixId;
    }

    function clearMatrixId() {
        matrixId = null;
    }

    function buildMatrix(n) {
        matrixWrap.innerHTML = '';
        const inner = document.createElement('div');
        inner.className = 'matrix-inner';

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

    function fillMatrixWithData(coefficients, rightHandSide) {
        for (let i = 0; i < size; i++) {
            for (let j = 0; j < size; j++) {
                const el = matrixWrap.querySelector(`input.coeff[data-row="${i}"][data-col="${j}"]`);
                if (el) el.value = coefficients[i][j].toFixed(2);
            }
            const rhsEl = matrixWrap.querySelector(`input.rhs[data-row="${i}"]`);
            if (rhsEl) rhsEl.value = rightHandSide[i].toFixed(2);
        }
    }

    function clearMatrix() {
        if (size < 10) {
            matrixWrap.querySelectorAll('input').forEach(i => i.value = '');
        } else {
            matrixId = null;
            showSummary(size);
        }
    }

    function updateMatrixDisplay() {
        matrixId = null;
        size < 10 ? buildMatrix(size) : showSummary(size);
    }

    async function generateRandom(progressCallback, resultCallback) {
        try {
            if (progressCallback) progressCallback(0, 'Generating...');
            
            const res = await fetch('/api/matrix/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ size, minValue: -200, maxValue: 200 })
            });
            const data = await res.json();
            
            if (!data.success) {
                if (resultCallback) resultCallback(data.error || 'Failed', true);
                if (progressCallback) progressCallback(0);
                return false;
            }

            if (size < 10) {
                fillMatrixWithData(data.coefficients, data.rightHandSide);
                if (progressCallback) progressCallback(100, 'Ready');
            } else {
                matrixId = data.matrixId;
                showSummary(size, `✓ ${data.message}<br><small>Click "Solve" to compute.</small>`);
                if (progressCallback) progressCallback(100, 'Ready');
            }
            return true;
        } catch (err) {
            if (resultCallback) resultCallback('Server error', true);
            if (progressCallback) progressCallback(0);
            return false;
        }
    }

    async function solveMatrix(taskId, progressModule, validationModule) {
        try {
            if (size < 10) {
                const { rows, rhs } = readMatrix();
                
                if (!validationModule.validateMatrixValues(rows, rhs)) {
                    progressModule.setProgress(0, 'Idle');
                    progressModule.setCurrentTaskId(null);
                    return { success: false };
                }
                
                const res = await fetch('/api/matrix/solve', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'X-Task-Id': taskId
                    },
                    body: JSON.stringify({ 
                        coefficients: rows, 
                        rightHandSide: rhs,
                        taskId: taskId
                    })
                });
                
                const data = await res.json();
                
                if (!res.ok) {
                    return { success: false, error: true, data };
                }
                
                return { success: true, data, isSmall: true };
            } else {
                if (!matrixId) {
                    return { success: false, needsGeneration: true };
                }
                
                console.log('🚀 Queueing matrix task for async processing...');
                
                const res = await fetch('/api/matrix/queue-solve', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ 
                        matrixId
                    })
                });
                
                const data = await res.json();
                
                if (!res.ok) {
                    return { success: false, error: true, data };
                }
                
                console.log('✅ Task queued successfully:', data);
                return { success: true, data, isSmall: false, queued: true };
            }
        } catch (err) {
            console.error('Solve error:', err);
            return { success: false, exception: err };
        }
    }

    return {
        init,
        setSize,
        getSize,
        setMatrixId,
        getMatrixId,
        clearMatrixId,
        buildMatrix,
        showSummary,
        readMatrix,
        fillMatrixWithData,
        clearMatrix,
        updateMatrixDisplay,
        generateRandom,
        solveMatrix
    };
})();

