
const SignalRProgressModule = (() => {
    let connection = null;
    let currentTaskId = null;
    let isCancelled = false;
    
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
        
        isCancelled = true;
        
        const currentPercent = progressBar ? 
            parseInt(progressBar.style.width) || 0 : 0;
        
        try {
            const res = await fetch(`/api/matrix/cancel/${currentTaskId}`, {
                method: 'POST'
            });
            const data = await res.json();
            if (data.success) {
                setProgress(currentPercent, 'Cancelled', 'Cancelled');
                if (cancelBtn) cancelBtn.disabled = true;
                
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
        
        setTimeout(() => {
            clearProgress();
            if (typeof HistoryModule !== 'undefined' && HistoryModule.loadHistory) {
                HistoryModule.loadHistory();
            }
        }, 500);
    }

    async function finalizeSuccess() {
        if (isCancelled) {
            return false; // Cancelled, don't continue
        }
        
        await new Promise(resolve => setTimeout(resolve, 800));
        
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