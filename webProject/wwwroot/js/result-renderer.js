// RESULT RENDERER MODULE
// Handles rendering of calculation results with LU decomposition

const ResultRenderer = (() => {
    
    // Render result for small matrices (< 10)
    function renderSmallMatrixResult(data) {
        if (!data.success) {
            return `<div class="result-error"><strong>✗ Error:</strong> ${data.error}</div>`;
        }

        let html = `<div class="result-success">`;
        
        // Solution
        html += `<div class="solution-display">`;
        html += `<strong>✓ Solution:</strong>`;
        html += `<div class="solution-values">[${data.solution.map(v => v.toFixed(6)).join(', ')}]</div>`;
        html += `</div>`;
        
        // Computation info
        html += renderComputationInfo(data);
        
        // LU decomposition
        if (data.luDecomposition && data.luDecomposition.lMatrix && data.luDecomposition.uMatrix) {
            html += renderLUDecomposition(data.luDecomposition);
        }
        
        html += `</div>`;
        return html;
    }

    // Render result for large matrices (>= 10)
    function renderLargeMatrixResult(data) {
        if (!data.success) {
            return `<div class="result-error"><strong>✗ Error:</strong> ${data.error}</div>`;
        }

        let html = `<div class="result-success">`;
        html += `<div class="solution-display">`;
        html += `<strong>✓ ${data.solutionSummary || 'Solution computed successfully'}</strong>`;
        html += `</div>`;
        
        // Computation info with LU note
        html += renderComputationInfo(data, true);
        
        html += `</div>`;
        return html;
    }

    // Render computation information
    function renderComputationInfo(data, includeLuNote = false) {
        let html = `<div class="computation-info">`;
        
        if (data.computationTime) {
            html += `<small>Computation time: ${data.computationTime.toFixed(2)}s</small>`;
        }
        
        if (data.determinant !== null && data.determinant !== undefined) {
            html += `<small>Determinant: ${data.determinant.toExponential(4)}</small>`;
        } else if (includeLuNote) {
            html += `<small>Determinant: not available (too large or matrix is singular)</small>`;
        }
        
        if (includeLuNote && data.luNote) {
            html += `<small style="color: var(--muted); font-style: italic;">${data.luNote}</small>`;
        }
        
        html += `</div>`;
        return html;
    }

    // Render LU decomposition matrices
    function renderLUDecomposition(luData) {
        let html = `<div class="lu-decomposition-section">`;
        html += `<strong class="lu-title">LU Decomposition:</strong>`;
        html += `<div class="lu-matrices-container">`;
        
        // L Matrix
        html += renderMatrix(luData.lMatrix, 'L Matrix', 'l-label');
        
        // U Matrix
        html += renderMatrix(luData.uMatrix, 'U Matrix', 'u-label');
        
        html += `</div></div>`;
        return html;
    }

    // Render single matrix (L or U)
    function renderMatrix(matrixData, label, labelClass) {
        let html = `<div class="lu-matrix-wrapper">`;
        html += `<strong class="matrix-label ${labelClass}">${label}</strong>`;
        html += `<div class="matrix-scroll"><table class="lu-matrix">`;
        
        for (let i = 0; i < matrixData.length; i++) {
            html += `<tr>`;
            for (let j = 0; j < matrixData[i].length; j++) {
                html += `<td>${matrixData[i][j].toFixed(4)}</td>`;
            }
            html += `</tr>`;
        }
        
        html += `</table></div></div>`;
        return html;
    }

    // Public API
    return {
        renderSmallMatrixResult,
        renderLargeMatrixResult
    };
})();

