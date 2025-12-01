// GAUSS SOLVER - Main Application
// Coordinates all modules and handles user interactions

(() => {
    // DOM ELEMENTS
    const $ = id => document.getElementById(id);
    const sizeInput = $('sizeInput');
    const sizeError = $('sizeError');
    const matrixWrap = $('matrixWrap');
    const tasksContainer = $('tasksContainer');
    const resultEl = $('result');
    const historyEl = $('history');

    // Initialize all modules
    ValidationModule.init(sizeError, resultEl);
    
    const initialSize = Math.max(parseInt(sizeInput?.value, 10) || 5, 2);
    MatrixModule.init(matrixWrap, initialSize);
    HistoryModule.init(historyEl);
    ThemeModule.init($('themeToggle'));

    // Initialize SignalR and TaskManager
    let signalRConnection = null;
    
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeSignalR);
    } else {
        initializeSignalR();
    }
    
    function initializeSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not loaded yet, retrying...');
            setTimeout(initializeSignalR, 100);
            return;
        }

        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/progressHub")
            .withAutomaticReconnect()
            .build();

        signalRConnection.start()
            .then(() => {
                console.log('SignalR connected');
                TaskManagerModule.init(tasksContainer, signalRConnection);
            })
            .catch(err => console.error("SignalR connection error:", err));
    }

    // SIZE INPUT HANDLERS
    if (sizeInput) {
        sizeInput.addEventListener('change', () => {
            const inputValue = parseInt(sizeInput.value, 10);
            const newSize = ValidationModule.validateSizeInput(inputValue, sizeInput);
            MatrixModule.setSize(newSize);
            MatrixModule.updateMatrixDisplay();
            resultEl.hidden = true;
        });

        sizeInput.addEventListener('input', () => {
            const inputValue = parseInt(sizeInput.value, 10);
            ValidationModule.validateSizeInputRealtime(inputValue);
        });
    }

    // RANDOM GENERATION
    $('rand').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        if (!ValidationModule.validateMatrixSize(MatrixModule.getSize())) {
            return;
        }
        
        // Simple progress for generation (not tracked as a task)
        try {
            const res = await fetch('/api/matrix/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ 
                    size: MatrixModule.getSize(), 
                    minValue: -200, 
                    maxValue: 200 
                })
            });
            const data = await res.json();
            
            if (!data.success) {
                ValidationModule.showResult(data.error || 'Failed to generate matrix', true);
                return;
            }

            if (MatrixModule.getSize() < 10) {
                MatrixModule.fillMatrixWithData(data.coefficients, data.rightHandSide);
            } else {
                MatrixModule.setMatrixId(data.matrixId);
                MatrixModule.showSummary(MatrixModule.getSize(), `✓ ${data.message}<br><small>Click "Solve" to compute.</small>`);
            }
        } catch (err) {
            ValidationModule.showResult('Server error', true);
        }
    });

    // CLEAR BUTTON
    $('clear').addEventListener('click', () => {
        MatrixModule.clearMatrix();
        resultEl.hidden = true;
        
        // Clear all active tasks and results
        TaskManagerModule.clearAll();
    });

    // SOLVE BUTTON
    $('solve').addEventListener('click', async () => {
        resultEl.hidden = true;
        
        if (!ValidationModule.validateMatrixSize(MatrixModule.getSize())) {
            return;
        }
        
        // Check if we can create a new task
        if (!TaskManagerModule.canCreateTask()) {
            ValidationModule.showResult('Maximum 3 concurrent tasks allowed. Please wait for a task to complete or cancel one.', true);
            return;
        }
        
        // Create task in TaskManager
        const taskResult = TaskManagerModule.createTask(MatrixModule.getSize());
        if (!taskResult.success) {
            ValidationModule.showResult(taskResult.error, true);
            return;
        }
        
        const taskId = taskResult.taskId;
        
        try {
            let requestBody, endpoint;
            
            if (MatrixModule.getSize() < 10) {
                // Small matrix - solve directly
                const { rows, rhs } = MatrixModule.readMatrix();
                
                if (!ValidationModule.validateMatrixValues(rows, rhs)) {
                    TaskManagerModule.setTaskError(taskId, 'Invalid matrix values');
                    return;
                }
                
                endpoint = '/api/matrix/solve';
                requestBody = { 
                    coefficients: rows, 
                    rightHandSide: rhs,
                    taskId: taskId
                };
            } else {
                // Large matrix - solve stored
                if (!MatrixModule.getMatrixId()) {
                    TaskManagerModule.removeTask(taskId);
                    ValidationModule.showResult('Generate matrix first!', true);
                    return;
                }
                
                endpoint = '/api/matrix/solve-stored';
                requestBody = { 
                    matrixId: MatrixModule.getMatrixId(),
                    taskId: taskId
                };
            }
            
            const res = await fetch(endpoint, {
                method: 'POST',
                headers: { 
                    'Content-Type': 'application/json',
                    'X-Task-Id': taskId
                },
                body: JSON.stringify(requestBody)
            });
            
            const data = await res.json();
            
            if (!res.ok) {
                console.error('Server error response:', data);
                const errorMsg = data.details 
                    ? `${data.error}: ${data.details}` 
                    : (data.error || 'Request failed');
                TaskManagerModule.setTaskError(taskId, errorMsg);
                ValidationModule.showResult(`Error: ${errorMsg}`, true);
                return;
            }
            
            if (data.success) {
                // Task completed successfully - progress updates handled by SignalR
                // Show results
                if (MatrixModule.getSize() < 10) {
                    const resultHTML = ResultRenderer.renderSmallMatrixResult(data);
                    ValidationModule.showResult(resultHTML);
                } else {
                    const resultHTML = ResultRenderer.renderLargeMatrixResult(data);
                    ValidationModule.showResult(resultHTML);
                    MatrixModule.clearMatrixId();
                    MatrixModule.showSummary(MatrixModule.getSize(), `Solved! Check history.`);
                }
                
                // Reload history
                HistoryModule.loadHistory();
            } else {
                const errorMsg = data.error || 'Calculation failed';
                TaskManagerModule.setTaskError(taskId, errorMsg);
                ValidationModule.showResult(`Error: ${errorMsg}`, true);
            }
        } catch (err) {
            console.error('Solve error:', err);
            const errorMsg = err.message || 'Server error';
            TaskManagerModule.setTaskError(taskId, errorMsg);
            ValidationModule.showResult(`Error: ${errorMsg}`, true);
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
})();

