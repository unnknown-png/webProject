// SIGNALR AND PROGRESS MODULE
// Handles SignalR connection and progress tracking

const SignalRProgressModule = (() => {
    let connection = null;
    let currentTaskId = null;
    
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
            if (taskId === currentTaskId) {
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
    }

    function getCurrentTaskId() {
        return currentTaskId;
    }

    function clearProgress() {
        setProgress(0, 'Idle');
        if (progressStage) progressStage.textContent = '';
    }

    async function cancelTask() {
        if (!currentTaskId) return;
        
        try {
            const res = await fetch(`/api/matrix/cancel/${currentTaskId}`, {
                method: 'POST'
            });
            const data = await res.json();
            if (data.success) {
                setProgress(0, 'Cancelling...', 'Cancelled');
                if (cancelBtn) cancelBtn.disabled = true;
            }
        } catch (err) {
            console.error('Cancel error:', err);
        }
    }

    return {
        init,
        initSignalR,
        setProgress,
        setCurrentTaskId,
        getCurrentTaskId,
        clearProgress,
        cancelTask
    };
})();

