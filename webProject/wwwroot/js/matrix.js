// MATRIX MODULE
// Handles matrix UI, generation, and solving

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

    // Build matrix UI for small matrices
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

    // Show summary for large matrices
    function showSummary(n, msg) {
        matrixWrap.innerHTML = `<div class="matrix-summary">${msg || `Matrix ${n}×${n} - too large to display.`}</div>`;
    }

    // Read matrix values from UI
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

    // Fill matrix with random values
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

    // Clear all inputs
    function clearMatrix() {
        if (size < 10) {
            matrixWrap.querySelectorAll('input').forEach(i => i.value = '');
        } else {
            matrixId = null;
            showSummary(size);
        }
    }

    // Update matrix display based on size
    function updateMatrixDisplay() {
        matrixId = null;
        size < 10 ? buildMatrix(size) : showSummary(size);
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
        updateMatrixDisplay
    };
})();

