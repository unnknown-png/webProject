// SIGNALR AND PROGRESS MODULE
// Handles SignalR connection and progress tracking

const SignalRProgressModule = (() => {
    let connection = null;
    let currentTaskId = null;
    let isCancelled = false;
    
    // DOM references
    let progressBar = null;
    let progressText = null;
    let progressPercent = null;
    let progressStage = null;
    let cancelBtn = null;

    function init(elements) {
        progressBar = elements.progressBar;
        progressText = elements.progressText;
        progressPercent = elements.progressPercent;
        progressStage = elements.progressStage;
        cancelBtn = elements.cancelBtn;
    }

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
            if (taskId === currentTaskId && !isCancelled) {
                setProgress(percent, message, stage);
            }
        });

        connection.start()
            .catch(err => console.error("SignalR connection error:", err));
    }

    function setProgress(percent, text, stage = null) {
        if (progressBar) progressBar.style.width = percent + '%';
        if (progressPercent) progressPercent.textContent = percent + '%';
        if (progressText) progressText.textContent = text || (percent === 0 ? 'Idle' : percent + '%');
        
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

    function setCurrentTaskId(taskId) {
        currentTaskId = taskId;
        isCancelled = false; // Reset cancelled flag for new task
    }


    function clearProgress() {
        setProgress(0, 'Idle');
        if (progressStage) progressStage.textContent = '';
        isCancelled = false;
    }

    async function cancelTask() {
        if (!currentTaskId) return false;
        
        // Set cancelled flag immediately to prevent "Completed" from overwriting
        isCancelled = true;
        
        // Get current progress percentage before cancelling
        const currentPercent = progressBar ? 
            parseInt(progressBar.style.width) || 0 : 0;
        
        try {
            const res = await fetch(`/api/matrix/cancel/${currentTaskId}`, {
                method: 'POST'
            });
            const data = await res.json();
            if (data.success) {
                // Keep current percentage, only change status to Cancelled
                setProgress(currentPercent, 'Cancelled', 'Cancelled');
                if (cancelBtn) cancelBtn.disabled = true;
                
                // Clear progress and reset UI after a moment (don't change 3000 - user requirement)
                setTimeout(() => {
                    clearProgress();
                    if (cancelBtn) {
                        cancelBtn.disabled = false;
                        cancelBtn.style.display = 'none';
                    }
                    currentTaskId = null;
                }, 3000);
                
                return true; // Success, caller should show message
            }
            return false;
        } catch (err) {
            console.error('Cancel error:', err);
            // Still clear on error
            setTimeout(() => {
                clearProgress();
                if (cancelBtn) cancelBtn.disabled = false;
                currentTaskId = null;
            }, 1000);
            return false;
        }
    }

    function isCancelledTask() {
        return isCancelled;
    }

    function handleError(data, resultCallback) {
        setProgress(0, 'Error', 'Failed');
        if (resultCallback) {
            resultCallback(`Error: ${data.error || 'Request failed'}`, true);
        }
        currentTaskId = null;
        
        if (cancelBtn) {
            cancelBtn.disabled = false;
            cancelBtn.style.display = 'none';
        }
        
        // Reload history to show error/cancellation
        setTimeout(() => {
            clearProgress();
            if (typeof HistoryModule !== 'undefined' && HistoryModule.loadHistory) {
                HistoryModule.loadHistory();
            }
        }, 500);
    }

    async function finalizeSuccess() {
        // Check if task was cancelled
        if (isCancelled) {
            return false; // Cancelled, don't continue
        }
        
        // Wait for SignalR to receive 100% progress
        await new Promise(resolve => setTimeout(resolve, 800));
        
        // Force 100% progress before showing result
        setProgress(100, 'Completed', 'Completed');
        await new Promise(resolve => setTimeout(resolve, 500));
        
        return true; // Continue with showing result
    }

    return {
        init,
        initSignalR,
        setProgress,
        setCurrentTaskId,
        clearProgress,
        cancelTask,
        isCancelledTask,
        handleError,
        finalizeSuccess
    };
})();