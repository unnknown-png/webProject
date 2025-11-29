// VALIDATION MODULE
// Contains validation constants and functions for matrix input

const ValidationModule = (() => {
    // VALIDATION CONSTANTS
    const MIN_MATRIX_SIZE = 2;
    const MAX_MATRIX_SIZE = 4000;
    const MAX_MATRIX_VALUE = 1e10;

    // DOM references
    let sizeError = null;
    let resultEl = null;

    function init(sizeErrorElement, resultElement) {
        sizeError = sizeErrorElement;
        resultEl = resultElement;
    }

    function showSizeError(message) {
        if (sizeError) {
            sizeError.textContent = message;
            sizeError.style.display = message ? 'inline-block' : 'none';
        }
    }

    function hideSizeError() {
        if (sizeError) {
            sizeError.style.display = 'none';
        }
    }

    function showResult(message, isError = false) {
        if (resultEl) {
            resultEl.hidden = false;
            resultEl.textContent = message;
            resultEl.style.color = isError ? '#ff6b6b' : '';
        }
    }

    function validateMatrixSize(size) {
        if (size < MIN_MATRIX_SIZE || size > MAX_MATRIX_SIZE) {
            showResult(`Matrix size must be between ${MIN_MATRIX_SIZE} and ${MAX_MATRIX_SIZE}`, true);
            return false;
        }
        return true;
    }

    function validateMatrixValues(coefficients, rightHandSide) {
        // Check coefficients
        for (let i = 0; i < coefficients.length; i++) {
            for (let j = 0; j < coefficients[i].length; j++) {
                const value = coefficients[i][j];
                
                if (isNaN(value) || !isFinite(value)) {
                    showResult(`Invalid value at position [${i}][${j}]: must be a valid number`, true);
                    return false;
                }
                
                if (Math.abs(value) > MAX_MATRIX_VALUE) {
                    showResult(`Value at position [${i}][${j}] exceeds maximum allowed (${MAX_MATRIX_VALUE})`, true);
                    return false;
                }
            }
        }

        // Check right hand side
        for (let i = 0; i < rightHandSide.length; i++) {
            const value = rightHandSide[i];
            
            if (isNaN(value) || !isFinite(value)) {
                showResult(`Invalid value at right hand side [${i}]: must be a valid number`, true);
                return false;
            }
            
            if (Math.abs(value) > MAX_MATRIX_VALUE) {
                showResult(`Right hand side value [${i}] exceeds maximum allowed`, true);
                return false;
            }
        }

        return true;
    }

    function validateSizeInput(inputValue, sizeInput, onValidSize) {
        // Check if input is valid number
        if (isNaN(inputValue) || inputValue < MIN_MATRIX_SIZE) {
            const newSize = MIN_MATRIX_SIZE;
            sizeInput.value = MIN_MATRIX_SIZE;
            showSizeError(`Matrix size cannot be less than ${MIN_MATRIX_SIZE}`);
            setTimeout(hideSizeError, 3000);
            return newSize;
        } else if (inputValue > MAX_MATRIX_SIZE) {
            const newSize = MAX_MATRIX_SIZE;
            sizeInput.value = MAX_MATRIX_SIZE;
            showSizeError(`Matrix size cannot exceed ${MAX_MATRIX_SIZE}`);
            setTimeout(hideSizeError, 3000);
            return newSize;
        } else {
            hideSizeError();
            return inputValue;
        }
    }

    function validateSizeInputRealtime(inputValue) {
        if (isNaN(inputValue)) {
            showSizeError('Please enter a valid number');
        } else if (inputValue < MIN_MATRIX_SIZE) {
            showSizeError(`Minimum size is ${MIN_MATRIX_SIZE}`);
        } else if (inputValue > MAX_MATRIX_SIZE) {
            showSizeError(`Maximum size is ${MAX_MATRIX_SIZE}`);
        } else {
            hideSizeError();
        }
    }

    return {
        MIN_MATRIX_SIZE,
        MAX_MATRIX_SIZE,
        MAX_MATRIX_VALUE,
        init,
        showSizeError,
        hideSizeError,
        showResult,
        validateMatrixSize,
        validateMatrixValues,
        validateSizeInput,
        validateSizeInputRealtime
    };
})();

