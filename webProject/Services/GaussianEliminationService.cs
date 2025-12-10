using webProject.Models;
using webProject.Helpers;
using webProject.Constants;

namespace webProject.Services;

public interface IGaussianEliminationService
{
    Task<MatrixSolution> SolveAsync(
        double[][] coefficients, 
        double[] rightHandSide, 
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default);
    GeneratedMatrix GenerateRandomMatrix(int size, double minValue = -10, double maxValue = 10);
}

public class GaussianEliminationService : IGaussianEliminationService
{
    public async Task<MatrixSolution> SolveAsync(
        double[][] coefficients, 
        double[] rightHandSide, 
        IProgress<ProgressInfo>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            try
            {
                var n = coefficients.Length;
                
                progress?.Report(new ProgressInfo
                {
                    Percent = MatrixConstants.ProgressInitStart,
                    Stage = CalculationStage.Initializing.ToString(),
                    Message = $"Starting computation for {n}×{n} system..."
                });
                
                await Task.Delay(MatrixConstants.InitializationDelay, cancellationToken);
                
                cancellationToken.ThrowIfCancellationRequested();
                
                if (n == 0 || rightHandSide.Length != n)
                {
                    return new MatrixSolution
                    {
                        Success = false,
                        ErrorMessage = "Invalid matrix dimensions",
                        Size = n
                    };
                }

                var a = coefficients.Select(row => row.ToArray()).ToArray();
                var b = rightHandSide.ToArray();

                if (n < MatrixConstants.SmallMatrixThresholdForDelay)
                {
                    await Task.Delay(MatrixConstants.SmallMatrixBaseDelay, cancellationToken);
                }

                progress?.Report(new ProgressInfo
                {
                    Percent = MatrixConstants.ProgressInitEnd,
                    Stage = CalculationStage.ForwardElimination.ToString(),
                    Message = "Performing forward elimination with partial pivoting..."
                });
                
                await Task.Delay(50, cancellationToken);

                for (int k = 0; k < n; k++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    int maxRow = k;
                    for (int i = k + 1; i < n; i++)
                    {
                        if (Math.Abs(a[i][k]) > Math.Abs(a[maxRow][k]))
                        {
                            maxRow = i;
                        }
                    }

                    if (Math.Abs(a[maxRow][k]) < 1e-12)
                    {
                        return new MatrixSolution
                        {
                            Success = false,
                            ErrorMessage = "Matrix is singular or nearly singular",
                            Size = n
                        };
                    }

                    if (maxRow != k)
                    {
                        (a[k], a[maxRow]) = (a[maxRow], a[k]);
                        (b[k], b[maxRow]) = (b[maxRow], b[k]);
                    }

                    for (int i = k + 1; i < n; i++)
                    {
                        double factor = a[i][k] / a[k][k];
                        for (int j = k; j < n; j++)
                        {
                            a[i][j] -= factor * a[k][j];
                        }
                        b[i] -= factor * b[k];
                    }

                    int percent = 5 + (k + 1) * 60 / n;
                    progress?.Report(new ProgressInfo
                    {
                        Percent = percent,
                        Stage = CalculationStage.ForwardElimination.ToString(),
                        Message = $"Eliminating column {k + 1}/{n}..."
                    });

                    if (n < 100 && k % 5 == 0)
                    {
                        await Task.Delay(50, cancellationToken);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ProgressInfo
                {
                    Percent = 65,
                    Stage = CalculationStage.BackSubstitution.ToString(),
                    Message = "Performing back substitution..."
                });

                var solution = new double[n];
                for (int i = n - 1; i >= 0; i--)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    double sum = b[i];
                    for (int j = i + 1; j < n; j++)
                    {
                        sum -= a[i][j] * solution[j];
                    }
                    solution[i] = sum / a[i][i];
                    
                    if (double.IsNaN(solution[i]) || double.IsInfinity(solution[i]))
                    {
                        return new MatrixSolution
                        {
                            Success = false,
                            ErrorMessage = "Solution contains invalid values (NaN or Infinity)",
                            Size = n
                        };
                    }

                    int percent = 65 + (n - i) * 30 / n;
                    progress?.Report(new ProgressInfo
                    {
                        Percent = percent,
                        Stage = CalculationStage.BackSubstitution.ToString(),
                        Message = $"Computing variable x{n - i}/{n}..."
                    });

                    if (n < 100 && i % 5 == 0)
                    {
                        await Task.Delay(30, cancellationToken);
                    }
                }

                progress?.Report(new ProgressInfo
                {
                    Percent = 95,
                    Stage = CalculationStage.Finalizing.ToString(),
                    Message = "Finalizing results..."
                });

                if (n < 100)
                {
                    await Task.Delay(100, cancellationToken);
                }

                progress?.Report(new ProgressInfo
                {
                    Percent = 100,
                    Stage = CalculationStage.Completed.ToString(),
                    Message = $"Successfully solved {n}×{n} system!"
                });

                return new MatrixSolution
                {
                    Success = true,
                    Solution = solution,
                    Size = n,
                    SolvedAt = TimeZoneHelper.UtcNow
                };
            }
            catch (OperationCanceledException)
            {
                progress?.Report(new ProgressInfo
                {
                    Percent = 0,
                    Stage = CalculationStage.Cancelled.ToString(),
                    Message = "Calculation was cancelled by user"
                });

                return new MatrixSolution
                {
                    Success = false,
                    ErrorMessage = "Calculation cancelled by user",
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

                return new MatrixSolution
                {
                    Success = false,
                    ErrorMessage = $"Error during calculation: {ex.Message}",
                    Size = coefficients.Length
                };
            }
        }, cancellationToken);
    }

    public GeneratedMatrix GenerateRandomMatrix(int size, double minValue = -10, double maxValue = 10)
    {
        var random = new Random();
        var coefficients = new double[size][];
        var rhs = new double[size];

        for (int i = 0; i < size; i++)
        {
            coefficients[i] = new double[size];
            for (int j = 0; j < size; j++)
            {
                coefficients[i][j] = random.NextDouble() * (maxValue - minValue) + minValue;
            }
            rhs[i] = random.NextDouble() * (maxValue - minValue) + minValue;
        }

        return new GeneratedMatrix
        {
            Coefficients = coefficients,
            RightHandSide = rhs,
            Size = size
        };
    }
}

