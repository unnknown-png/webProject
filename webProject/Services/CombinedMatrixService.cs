using System.Diagnostics;
using webProject.Models;

namespace webProject.Services;

public interface ICombinedMatrixService
{
    Task<CombinedSolutionResult> SolveAndDecomposeAsync(
        double[][] coefficients,
        double[] rightHandSide,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}

public class CombinedMatrixService : ICombinedMatrixService
{
    private readonly IGaussianEliminationService _gaussService;
    private readonly ILUDecompositionService _luService;

    public CombinedMatrixService(
        IGaussianEliminationService gaussService,
        ILUDecompositionService luService)
    {
        _gaussService = gaussService;
        _luService = luService;
    }

    public async Task<CombinedSolutionResult> SolveAndDecomposeAsync(
        double[][] coefficients,
        double[] rightHandSide,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            int n = coefficients.Length;

            progress?.Report(new ProgressInfo
            {
                Percent = 0,
                Stage = CalculationStage.Initializing.ToString(),
                Message = $"Starting combined computation for {n}×{n} system..."
            });

            await Task.Delay(100, cancellationToken);

            // Create copies for parallel operations
            var matrixForGauss = coefficients.Select(row => row.ToArray()).ToArray();
            var vectorForGauss = rightHandSide.ToArray();
            var matrixForLU = coefficients.Select(row => row.ToArray()).ToArray();

            cancellationToken.ThrowIfCancellationRequested();

            // Progress reporters for both operations
            var gaussProgress = new Progress<ProgressInfo>(info =>
            {
                // Report Gaussian progress (0-50%)
                var adjustedInfo = new ProgressInfo
                {
                    Percent = info.Percent / 2,
                    Stage = info.Stage,
                    Message = $"[Gaussian] {info.Message}"
                };
                progress?.Report(adjustedInfo);
            });

            var luProgress = new Progress<ProgressInfo>(info =>
            {
                // Report LU progress (50-100%)
                var adjustedInfo = new ProgressInfo
                {
                    Percent = 50 + info.Percent / 2,
                    Stage = info.Stage,
                    Message = $"[LU Decomposition] {info.Message}"
                };
                progress?.Report(adjustedInfo);
            });

            // Run both operations in parallel
            var gaussTask = _gaussService.SolveAsync(
                matrixForGauss, 
                vectorForGauss, 
                gaussProgress, 
                cancellationToken);

            var luTask = _luService.DecomposeAsync(
                matrixForLU, 
                luProgress, 
                cancellationToken);

            // Wait for both to complete
            await Task.WhenAll(gaussTask, luTask);

            // Tasks are already completed, just get results
            var gaussianSolution = gaussTask.Result;
            var luDecomposition = luTask.Result;

            stopwatch.Stop();

            // Check if both succeeded
            bool overallSuccess = gaussianSolution.Success && luDecomposition.Success;
            string? errorMessage = null;

            if (!gaussianSolution.Success)
            {
                errorMessage = $"Gaussian elimination failed: {gaussianSolution.ErrorMessage}";
            }
            else if (!luDecomposition.Success)
            {
                errorMessage = $"LU decomposition failed: {luDecomposition.ErrorMessage}";
            }

            progress?.Report(new ProgressInfo
            {
                Percent = 100,
                Stage = CalculationStage.Completed.ToString(),
                Message = overallSuccess 
                    ? $"Successfully completed both operations in {stopwatch.Elapsed.TotalSeconds:F2}s!" 
                    : "Computation completed with errors"
            });

            return new CombinedSolutionResult
            {
                GaussianSolution = gaussianSolution,
                LUDecomposition = luDecomposition,
                Success = overallSuccess,
                ErrorMessage = errorMessage,
                Size = n,
                SolvedAt = DateTime.UtcNow,
                ComputationTimeSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new ProgressInfo
            {
                Percent = 0,
                Stage = CalculationStage.Cancelled.ToString(),
                Message = "Combined computation was cancelled by user"
            });

            return new CombinedSolutionResult
            {
                Success = false,
                ErrorMessage = "Computation cancelled by user",
                Size = coefficients.Length,
                ComputationTimeSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
        catch (Exception ex)
        {
            progress?.Report(new ProgressInfo
            {
                Percent = 0,
                Stage = CalculationStage.Failed.ToString(),
                Message = $"Error: {ex.Message}"
            });

            return new CombinedSolutionResult
            {
                Success = false,
                ErrorMessage = $"Error during computation: {ex.Message}",
                Size = coefficients.Length,
                ComputationTimeSeconds = stopwatch.Elapsed.TotalSeconds
            };
        }
    }
}

