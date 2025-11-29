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
        cancelBtn.addEventListener('click', async () => {
            const cancelled = await SignalRProgressModule.cancelTask();
            if (cancelled) {
                ValidationModule.showResult('Calculation was cancelled by user', false);
                // Reload history to show the cancelled task
                setTimeout(() => {
                    HistoryModule.loadHistory();
                }, 500);
            }
        });
    }

    // RANDOM GENERATION
    $('rand').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        if (!ValidationModule.validateMatrixSize(MatrixModule.getSize())) {
            return;
        }
        
        await MatrixModule.generateRandom(
            (percent, text) => SignalRProgressModule.setProgress(percent, text),
            (msg, isError) => ValidationModule.showResult(msg, isError)
        );
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
        
        if (!ValidationModule.validateMatrixSize(MatrixModule.getSize())) {
            return;
        }
        
        const taskId = crypto.randomUUID ? crypto.randomUUID() : Date.now().toString();
        SignalRProgressModule.setCurrentTaskId(taskId);
        SignalRProgressModule.setProgress(0, 'Starting...', 'Initializing');
        if (cancelBtn) cancelBtn.disabled = false;
        
        try {
            const result = await MatrixModule.solveMatrix(taskId, SignalRProgressModule, ValidationModule);
            
            if (!result.success) {
                if (result.needsGeneration) {
                    alert('Generate matrix first!');
                } else if (result.error) {
                    SignalRProgressModule.handleError(result.data, ValidationModule.showResult);
                } else if (result.exception) {
                    ValidationModule.showResult('Server error', true);
                    SignalRProgressModule.setProgress(0);
                    SignalRProgressModule.setCurrentTaskId(null);
                    if (cancelBtn) {
                        cancelBtn.disabled = false;
                        cancelBtn.style.display = 'none';
                    }
                }
                return;
            }
            
            // Finalize with 100% progress
            const shouldContinue = await SignalRProgressModule.finalizeSuccess();
            if (!shouldContinue) return;
            
            // Show results using ResultRenderer module
            if (result.isSmall) {
                const resultHTML = ResultRenderer.renderSmallMatrixResult(result.data);
                ValidationModule.showResult(resultHTML);
            } else {
                if (result.data.success) {
                    const resultHTML = ResultRenderer.renderLargeMatrixResult(result.data);
                    ValidationModule.showResult(resultHTML);
                    MatrixModule.clearMatrixId();
                    MatrixModule.showSummary(MatrixModule.getSize(), `Solved! Check history.`);
                } else {
                    ValidationModule.showResult(`Error: ${result.data.error}`, true);
                }
            }
            
            SignalRProgressModule.setCurrentTaskId(null);
            setTimeout(() => SignalRProgressModule.clearProgress(), 2000);
            
            HistoryModule.loadHistory();
        } catch (err) {
            console.error('Solve error:', err);
            ValidationModule.showResult('Server error', true);
            SignalRProgressModule.setProgress(0);
            SignalRProgressModule.setCurrentTaskId(null);
            
            if (cancelBtn) {
                cancelBtn.disabled = false;
                cancelBtn.style.display = 'none';
            }
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

