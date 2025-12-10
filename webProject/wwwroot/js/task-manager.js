
const TaskManagerModule = (() => {
    const MAX_CONCURRENT_TASKS = 3;
    const tasks = new Map(); // taskId -> task object
    let tasksContainer = null;
    let signalRConnection = null;

    function init(container, connection) {
        tasksContainer = container;
        
        if (!tasksContainer) {
            console.error('TaskManager: tasksContainer is null!');
            return;
        }
        
        if (connection) {
            setSignalRConnection(connection);
        }
    }
    
    function setSignalRConnection(connection) {
        signalRConnection = connection;
        
        if (signalRConnection) {
            signalRConnection.on("ReceiveProgress", (taskId, percent, stage, message) => {
                updateTaskProgress(taskId, percent, message, stage);
            });
            
            signalRConnection.on("TaskQueued", (data) => {
                console.log('Task queued:', data);
                updateTaskProgress(data.taskId, 0, data.message || 'Queued for processing', 'Queued');
            });
            
            signalRConnection.on("TaskStatusChanged", (data) => {
                console.log('Task status changed:', data);
                updateTaskProgress(data.taskId, 5, data.message || 'Processing started', 'Processing');
            });
            
            signalRConnection.on("TaskCompleted", (data) => {
                console.log('Task completed:', data);
                
                updateTaskProgress(data.taskId, 100, data.message || 'Completed', 'Completed');
                
                if (typeof HistoryModule !== 'undefined') {
                    setTimeout(() => HistoryModule.loadHistory(), 500);
                }
                
                if (typeof ValidationModule !== 'undefined') {
                    const msg = `✓ Matrix ${data.size}×${data.size} solved successfully in ${data.executionTime?.toFixed(2)}s`;
                    ValidationModule.showResult(msg);
                }
            });
            
            signalRConnection.on("TaskFailed", (data) => {
                console.log('Task failed:', data);
                updateTaskProgress(data.taskId, 0, data.message || data.error || 'Failed', 'Failed');
                
                if (typeof ValidationModule !== 'undefined') {
                    ValidationModule.showResult(`Error: ${data.error || 'Task failed'}`, true);
                }
            });
            
            console.log('TaskManager: SignalR handlers registered');
        }
    }

    function canCreateTask() {
        const activeTasks = Array.from(tasks.values()).filter(t => 
            t.status === 'running' || t.status === 'pending'
        );
        return activeTasks.length < MAX_CONCURRENT_TASKS;
    }

    function getActiveTaskCount() {
        return Array.from(tasks.values()).filter(t => 
            t.status === 'running' || t.status === 'pending'
        ).length;
    }

    function createTask(matrixSize) {
        if (!canCreateTask()) {
            return { 
                success: false, 
                error: 'Maximum 3 concurrent tasks allowed. Please wait for a task to complete or cancel one.' 
            };
        }

        const taskId = generateTaskId();
        const task = {
            id: taskId,
            size: matrixSize,
            status: 'pending',
            progress: 0,
            message: 'Initializing...',
            stage: null,
            createdAt: Date.now(),
            element: null
        };

        tasks.set(taskId, task);
        renderTask(task);
        
        return { success: true, taskId };
    }

    function generateTaskId() {
        return 'task_' + Date.now() + '_' + Math.random().toString(36).substr(2, 9);
    }

    function renderTask(task) {
        const taskElement = document.createElement('div');
        taskElement.className = 'task-status';
        taskElement.id = `task-${task.id}`;
        taskElement.dataset.taskId = task.id;

        taskElement.innerHTML = `
            <div class="task-header">
                <div class="task-info">
                    <span class="task-label">Matrix ${task.size}×${task.size}</span>
                    <span class="task-size" data-task-time>${getElapsedTime(task.createdAt)}</span>
                </div>
                <div class="task-actions">
                    <button class="btn-cancel" data-task-cancel="${task.id}">Cancel</button>
                </div>
            </div>
            <div class="progress-info">
                <div class="progress-header">
                    <span data-task-text="${task.id}">${task.message}</span>
                </div>
                <div data-task-stage="${task.id}" class="progress-stage"></div>
            </div>
            <div class="progress">
                <i data-task-bar="${task.id}" style="width: ${task.progress}%"></i>
                <span data-task-percent="${task.id}" class="progress-percent">${task.progress}%</span>
            </div>
        `;

        tasksContainer.appendChild(taskElement);
        task.element = taskElement;

        const cancelBtn = taskElement.querySelector(`[data-task-cancel="${task.id}"]`);
        if (cancelBtn) {
            cancelBtn.addEventListener('click', () => cancelTask(task.id));
        }

        task.timeInterval = setInterval(() => {
            const timeElement = taskElement.querySelector('[data-task-time]');
            if (timeElement) {
                timeElement.textContent = getElapsedTime(task.createdAt);
            }
        }, 1000);
    }

    function getElapsedTime(startTime) {
        const elapsed = Math.floor((Date.now() - startTime) / 1000);
        if (elapsed < 60) return `${elapsed}s`;
        const minutes = Math.floor(elapsed / 60);
        const seconds = elapsed % 60;
        return `${minutes}m ${seconds}s`;
    }

    function updateTaskProgress(taskId, percent, message, stage = null) {
        const task = tasks.get(taskId);
        if (!task) return;

        task.progress = percent;
        task.message = message;
        task.stage = stage;
        
        if (percent >= 100) {
            task.status = 'completed';
        } else if (percent > 0) {
            task.status = 'running';
        }

        if (task.element) {
            const bar = task.element.querySelector(`[data-task-bar="${taskId}"]`);
            const percentText = task.element.querySelector(`[data-task-percent="${taskId}"]`);
            const messageText = task.element.querySelector(`[data-task-text="${taskId}"]`);
            const stageText = task.element.querySelector(`[data-task-stage="${taskId}"]`);
            const cancelBtn = task.element.querySelector(`[data-task-cancel="${taskId}"]`);

            if (bar) bar.style.width = percent + '%';
            if (percentText) percentText.textContent = percent + '%';
            if (messageText) messageText.textContent = message || (percent === 0 ? 'Idle' : percent + '%');
            
            if (stageText && stage) {
                const stageLabels = {
                    'Initializing': 'Initializing...',
                    'ForwardElimination': 'Forward Elimination',
                    'BackSubstitution': 'Back Substitution',
                    'Finalizing': 'Finalizing...',
                    'Completed': 'Completed!',
                    'Cancelled': 'Cancelled',
                    'Failed': 'Failed'
                };
                stageText.textContent = stageLabels[stage] || stage;
            }

            if (cancelBtn) {
                if (percent >= 100 || percent === 0 || stage === 'Cancelled' || stage === 'Failed') {
                    cancelBtn.classList.add('hidden');
                } else {
                    cancelBtn.classList.remove('hidden');
                }
            }

            if (task.status === 'completed') {
                task.element.classList.add('task-completed');
                
                if (task.timeInterval) {
                    clearInterval(task.timeInterval);
                    task.timeInterval = null;
                }
                
                setTimeout(() => {
                    removeTask(taskId);
                    hideResult();
                }, 2000);
            }
            
            if (stage === 'Cancelled' || stage === 'Failed') {
                task.status = stage === 'Cancelled' ? 'cancelled' : 'failed';
                
                if (task.timeInterval) {
                    clearInterval(task.timeInterval);
                    task.timeInterval = null;
                }
                
                setTimeout(() => {
                    removeTask(taskId);
                    hideResult();
                }, 2000);
            }
        }
    }
    
    function hideResult() {
        const resultEl = document.getElementById('result');
        if (resultEl) {
            resultEl.hidden = true;
        }
    }

    async function cancelTask(taskId) {
        const task = tasks.get(taskId);
        if (!task) return false;

        try {
            const res = await fetch(`/api/matrix/cancel/${taskId}`, {
                method: 'POST'
            });
            const data = await res.json();
            
            if (data.success) {
                updateTaskProgress(taskId, task.progress, 'Cancelled', 'Cancelled');
                task.status = 'cancelled';
                
                const cancelBtn = task.element?.querySelector(`[data-task-cancel="${taskId}"]`);
                if (cancelBtn) {
                    cancelBtn.disabled = true;
                    cancelBtn.classList.add('hidden');
                }
                
                if (task.timeInterval) {
                    clearInterval(task.timeInterval);
                }
                
                if (typeof HistoryModule !== 'undefined' && HistoryModule.loadHistory) {
                    HistoryModule.loadHistory();
                }
                
                setTimeout(() => {
                    removeTask(taskId);
                    hideResult();
                }, 2000);
                
                return true;
            }
            return false;
        } catch (err) {
            console.error('Cancel error:', err);
            return false;
        }
    }

    function removeTask(taskId) {
        const task = tasks.get(taskId);
        if (!task) return;

        if (task.timeInterval) {
            clearInterval(task.timeInterval);
        }

        if (task.element) {
            task.element.style.transition = 'opacity 0.3s ease, transform 0.3s ease';
            task.element.style.opacity = '0';
            task.element.style.transform = 'scale(0.95)';
            
            setTimeout(() => {
                task.element.remove();
            }, 300);
        }

        tasks.delete(taskId);
    }

    function setTaskError(taskId, errorMessage) {
        const task = tasks.get(taskId);
        if (!task) return;

        task.status = 'failed';
        updateTaskProgress(taskId, 0, errorMessage, 'Failed');
        
        const cancelBtn = task.element?.querySelector(`[data-task-cancel="${taskId}"]`);
        if (cancelBtn) {
            cancelBtn.classList.add('hidden');
        }
        
        if (task.timeInterval) {
            clearInterval(task.timeInterval);
        }
        
        if (typeof HistoryModule !== 'undefined' && HistoryModule.loadHistory) {
            HistoryModule.loadHistory();
        }
        
        setTimeout(() => {
            removeTask(taskId);
            hideResult();
        }, 2000);
    }

    function getTask(taskId) {
        return tasks.get(taskId);
    }

    function getAllTasks() {
        return Array.from(tasks.values());
    }
    
    function clearAll() {
        tasks.forEach((task, taskId) => {
            if (task.timeInterval) {
                clearInterval(task.timeInterval);
            }
            if (task.element) {
                task.element.remove();
            }
        });
        tasks.clear();
        
        hideResult();
    }

    return {
        init,
        setSignalRConnection,
        canCreateTask,
        getActiveTaskCount,
        createTask,
        updateTaskProgress,
        cancelTask,
        removeTask,
        setTaskError,
        getTask,
        getAllTasks,
        clearAll
    };
})();

