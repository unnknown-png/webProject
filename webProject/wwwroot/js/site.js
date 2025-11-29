// GAUSS SOLVER - Main Application
// Coordinates all modules and handles user interactions

(() => {
    // DOM ELEMENTS
    const $ = id => document.getElementById(id);
    const sizeInput = $('sizeInput');
    const sizeError = $('sizeError');
    const matrixWrap = $('matrixWrap');
    const progressBar = $('progressBar');
    const progressText = $('progressText');
    const progressPercent = $('progressPercent');
    const progressStage = $('progressStage');
    const cancelBtn = $('cancelBtn');
    const resultEl = $('result');
    const historyEl = $('history');

    // Initialize all modules
    ValidationModule.init(sizeError, resultEl);
    
    SignalRProgressModule.init({
        progressBar,
        progressText,
        progressPercent,
        progressStage,
        cancelBtn
    });

    const initialSize = Math.max(parseInt(sizeInput?.value, 10) || 5, 2);
    MatrixModule.init(matrixWrap, initialSize);
    HistoryModule.init(historyEl);
    ThemeModule.init($('themeToggle'));

    // Initialize SignalR
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => SignalRProgressModule.initSignalR());
    } else {
        SignalRProgressModule.initSignalR();
    }

    // SIZE INPUT HANDLERS
    if (sizeInput) {
        sizeInput.addEventListener('change', () => {
            const inputValue = parseInt(sizeInput.value, 10);
            const newSize = ValidationModule.validateSizeInput(inputValue, sizeInput);
            MatrixModule.setSize(newSize);
            MatrixModule.updateMatrixDisplay();
            SignalRProgressModule.clearProgress();
            resultEl.hidden = true;
        });

        sizeInput.addEventListener('input', () => {
            const inputValue = parseInt(sizeInput.value, 10);
            ValidationModule.validateSizeInputRealtime(inputValue);
        });
    }

    // CANCEL BUTTON
    if (cancelBtn) {
        cancelBtn.addEventListener('click', () => SignalRProgressModule.cancelTask());
    }

    // RANDOM GENERATION
    $('rand').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        const size = MatrixModule.getSize();
        if (!ValidationModule.validateMatrixSize(size)) {
            return;
        }
        
        SignalRProgressModule.setProgress(0, 'Generating...');
        try {
            const res = await fetch('/api/matrix/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ size, minValue: -200, maxValue: 200 })
            });
            const data = await res.json();
            
            if (!data.success) {
                ValidationModule.showResult(data.error || 'Failed', true);
                SignalRProgressModule.setProgress(0);
                return;
            }

            if (size < 10) {
                MatrixModule.fillMatrixWithData(data.coefficients, data.rightHandSide);
                SignalRProgressModule.setProgress(100, 'Ready');
            } else {
                MatrixModule.setMatrixId(data.matrixId);
                MatrixModule.showSummary(size, `✓ ${data.message}<br><small>Click "Solve" to compute.</small>`);
                SignalRProgressModule.setProgress(100, 'Ready');
            }
        } catch (err) {
            ValidationModule.showResult('Server error', true);
            SignalRProgressModule.setProgress(0);
        }
    });

    // CLEAR BUTTON
    $('clear').addEventListener('click', () => {
        MatrixModule.clearMatrix();
        resultEl.hidden = true;
        SignalRProgressModule.clearProgress();
    });

    // SOLVE BUTTON
    $('solve').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        const size = MatrixModule.getSize();
        if (!ValidationModule.validateMatrixSize(size)) {
            return;
        }
        
        const taskId = crypto.randomUUID ? crypto.randomUUID() : Date.now().toString();
        SignalRProgressModule.setCurrentTaskId(taskId);
        SignalRProgressModule.setProgress(0, 'Starting...', 'Initializing');
        if (cancelBtn) cancelBtn.disabled = false;
        
        try {
            if (size < 10) {
                // Small matrix - solve directly
                const { rows, rhs } = MatrixModule.readMatrix();
                
                if (!ValidationModule.validateMatrixValues(rows, rhs)) {
                    SignalRProgressModule.setProgress(0, 'Idle');
                    SignalRProgressModule.setCurrentTaskId(null);
                    return;
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
                await new Promise(resolve => setTimeout(resolve, 500));
                
                ValidationModule.showResult(
                    data.success 
                        ? `Solution: [${data.solution.map(v => v.toFixed(6)).join(', ')}]`
                        : `Error: ${data.error}`, 
                    !data.success
                );
                
                SignalRProgressModule.setCurrentTaskId(null);
            } else {
                // Large matrix - solve stored
                const matrixId = MatrixModule.getMatrixId();
                if (!matrixId) { 
                    alert('Generate matrix first!'); 
                    return; 
                }
                
                const res = await fetch('/api/matrix/solve-stored', {
                    method: 'POST',
                    headers: { 
                        'Content-Type': 'application/json',
                        'X-Task-Id': taskId
                    },
                    body: JSON.stringify({ 
                        matrixId,
                        taskId: taskId
                    })
                });
                const data = await res.json();
                await new Promise(resolve => setTimeout(resolve, 500));
                
                if (data.success) {
                    ValidationModule.showResult(`✓ ${data.solutionSummary}`);
                    MatrixModule.clearMatrixId();
                    MatrixModule.showSummary(size, `Solved! Check history.`);
                } else {
                    ValidationModule.showResult(`Error: ${data.error}`, true);
                }
                
                SignalRProgressModule.setCurrentTaskId(null);
            }
            HistoryModule.loadHistory();
        } catch (err) {
            ValidationModule.showResult('Server error', true);
            SignalRProgressModule.setProgress(0);
            SignalRProgressModule.setCurrentTaskId(null);
        }
    });

    // HISTORY CONTROLS
    $('clearHistory').addEventListener('click', () => HistoryModule.clearHistory());
    $('export').addEventListener('click', () => HistoryModule.exportHistory());

    // THEME TOGGLE
    $('themeToggle').addEventListener('click', () => ThemeModule.toggleTheme());

    // NAVIGATION
    NavigationModule.setupNavigation();

    // INITIALIZATION
    const size = MatrixModule.getSize();
    size < 10 ? MatrixModule.buildMatrix(size) : MatrixModule.showSummary(size);
    HistoryModule.loadHistory();
    SignalRProgressModule.setProgress(0);
})();

