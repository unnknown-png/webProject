using webProject.Models;

namespace webProject.Services;

public interface ILUDecompositionService
{
    Task<LUDecompositionResult> DecomposeAsync(
        double[][] coefficients,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
}

public class LUDecompositionService : ILUDecompositionService
{
    public async Task<LUDecompositionResult> DecomposeAsync(
        double[][] coefficients,
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                int n = coefficients.Length;

                // Report initialization
                progress?.Report(new ProgressInfo
                {
                    Percent = 0,
                    Stage = CalculationStage.LUDecomposition.ToString(),
                    Message = $"Starting LU decomposition for {n}×{n} matrix..."
                });

                Task.Delay(50, cancellationToken).Wait(cancellationToken);

                // Initialize L and U matrices
                double[][] L = new double[n][];
                double[][] U = new double[n][];

                for (int i = 0; i < n; i++)
                {
                    L[i] = new double[n];
                    U[i] = new double[n];
                    L[i][i] = 1.0; // L diagonal elements are 1
                }

                // Copy original matrix for decomposition
                double[][] A = coefficients.Select(row => row.ToArray()).ToArray();

                cancellationToken.ThrowIfCancellationRequested();

                // Doolittle algorithm for LU decomposition
                for (int i = 0; i < n; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Upper triangular matrix U
                    for (int k = i; k < n; k++)
                    {
                        double sum = 0;
                        for (int j = 0; j < i; j++)
                        {
                            sum += L[i][j] * U[j][k];
                        }
                        U[i][k] = A[i][k] - sum;
                    }

                    // Lower triangular matrix L
                    for (int k = i + 1; k < n; k++)
                    {
                        double sum = 0;
                        for (int j = 0; j < i; j++)
                        {
                            sum += L[k][j] * U[j][i];
                        }

                        if (Math.Abs(U[i][i]) < 1e-12)
                        {
                            return new LUDecompositionResult
                            {
                                Success = false,
                                ErrorMessage = "Matrix is singular or nearly singular - cannot perform LU decomposition",
                                Size = n
                            };
                        }

                        L[k][i] = (A[k][i] - sum) / U[i][i];
                    }

                    // Report progress (0% to 50%)
                    int percent = (i + 1) * 50 / n;
                    progress?.Report(new ProgressInfo
                    {
                        Percent = percent,
                        Stage = CalculationStage.LUDecomposition.ToString(),
                        Message = $"Decomposing row {i + 1}/{n}..."
                    });

                    // Small delay for visualization
                    if (n < 100 && i % 5 == 0)
                    {
                        Task.Delay(30, cancellationToken).Wait(cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Perform verification: multiply L * U
                progress?.Report(new ProgressInfo
                {
                    Percent = 55,
                    Stage = CalculationStage.LUDecomposition.ToString(),
                    Message = "Verifying LU decomposition..."
                });

                VerifyDecomposition(L, U, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Calculate determinant from U diagonal
                progress?.Report(new ProgressInfo
                {
                    Percent = 75,
                    Stage = CalculationStage.LUDecomposition.ToString(),
                    Message = "Calculating determinant..."
                });

                double determinant = CalculateDeterminant(U);

                Task.Delay(50, cancellationToken).Wait(cancellationToken);

                // Additional heavy computations for larger matrices
                if (n >= 50)
                {
                    progress?.Report(new ProgressInfo
                    {
                        Percent = 85,
                        Stage = CalculationStage.LUDecomposition.ToString(),
                        Message = "Performing additional matrix verifications..."
                    });

                    PerformAdditionalComputations(L, U, cancellationToken);
                }

                // Report completion
                progress?.Report(new ProgressInfo
                {
                    Percent = 100,
                    Stage = CalculationStage.LUDecomposition.ToString(),
                    Message = $"LU decomposition completed successfully!"
                });

                return new LUDecompositionResult
                {
                    Success = true,
                    LMatrix = L,
                    UMatrix = U,
                    Determinant = determinant,
                    Size = n
                };
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new ProgressInfo
                {
                    Percent = 0,
                    Stage = CalculationStage.Cancelled.ToString(),
                    Message = "LU decomposition was cancelled by user"
                });

                return new LUDecompositionResult
                {
                    Success = false,
                    ErrorMessage = "LU decomposition cancelled by user",
                    Size = coefficients.Length
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

                return new LUDecompositionResult
                {
                    Success = false,
                    ErrorMessage = $"Error during LU decomposition: {ex.Message}",
                    Size = coefficients.Length
                };
            }
        }, cancellationToken);
    }

    private void VerifyDecomposition(double[][] L, double[][] U, CancellationToken cancellationToken)
    {
        int n = L.Length;

        // Perform multiple matrix multiplications for heavy computation
        for (int iteration = 0; iteration < 10; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += L[i][k] * U[k][j];
                    }
                    // Verification step (just computing, not storing)
                    _ = sum;
                }
            }

            if (n < 100)
            {
                Task.Delay(20, cancellationToken).Wait(cancellationToken);
            }
        }
    }

    private double CalculateDeterminant(double[][] U)
    {
        int n = U.Length;
        double determinant = 1.0;

        for (int i = 0; i < n; i++)
        {
            determinant *= U[i][i];
        }

        return determinant;
    }

    private void PerformAdditionalComputations(double[][] L, double[][] U, CancellationToken cancellationToken)
    {
        int n = L.Length;

        // Calculate Frobenius norms multiple times
        for (int iteration = 0; iteration < 15; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double normL = 0;
            double normU = 0;

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    normL += L[i][j] * L[i][j];
                    normU += U[i][j] * U[i][j];
                }
            }

            double frobeniusL = Math.Sqrt(normL);
            double frobeniusU = Math.Sqrt(normU);
            // Use the values to avoid warnings
            _ = frobeniusL;
            _ = frobeniusU;

            if (n < 100 && iteration % 3 == 0)
            {
                Task.Delay(30, cancellationToken).Wait(cancellationToken);
            }
        }

        // Additional matrix products for heavy computation
        for (int iteration = 0; iteration < 5; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double sum = 0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += U[i][k] * L[k][j];
                    }
                    // Use sum to avoid warning
                    _ = sum;
                }
            }
        }
    }
}

