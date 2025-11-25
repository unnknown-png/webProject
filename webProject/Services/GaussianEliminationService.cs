using webProject.Models;

namespace webProject.Services;

public interface IGaussianEliminationService
{
    Task<MatrixSolution> SolveAsync(double[][] coefficients, double[] rightHandSide, IProgress<int>? progress = null);
    GeneratedMatrix GenerateRandomMatrix(int size, double minValue = -10, double maxValue = 10);
}

public class GaussianEliminationService : IGaussianEliminationService
{
    public async Task<MatrixSolution> SolveAsync(double[][] coefficients, double[] rightHandSide, IProgress<int>? progress = null)
    {
        return await Task.Run(() =>
        {
            try
            {
                var n = coefficients.Length;
                
                // Validate input
                if (n == 0 || rightHandSide.Length != n)
                {
                    return new MatrixSolution
                    {
                        Success = false,
                        ErrorMessage = "Invalid matrix dimensions",
                        Size = n
                    };
                }

                // Create copies to avoid modifying original arrays
                var a = coefficients.Select(row => row.ToArray()).ToArray();
                var b = rightHandSide.ToArray();

                // Forward elimination with partial pivoting
                for (int k = 0; k < n; k++)
                {
                    // Find pivot
                    int maxRow = k;
                    for (int i = k + 1; i < n; i++)
                    {
                        if (Math.Abs(a[i][k]) > Math.Abs(a[maxRow][k]))
                        {
                            maxRow = i;
                        }
                    }

                    // Check for singular matrix
                    if (Math.Abs(a[maxRow][k]) < 1e-12)
                    {
                        return new MatrixSolution
                        {
                            Success = false,
                            ErrorMessage = "Matrix is singular or nearly singular",
                            Size = n
                        };
                    }

                    // Swap rows
                    if (maxRow != k)
                    {
                        (a[k], a[maxRow]) = (a[maxRow], a[k]);
                        (b[k], b[maxRow]) = (b[maxRow], b[k]);
                    }

                    // Eliminate column
                    for (int i = k + 1; i < n; i++)
                    {
                        double factor = a[i][k] / a[k][k];
                        for (int j = k; j < n; j++)
                        {
                            a[i][j] -= factor * a[k][j];
                        }
                        b[i] -= factor * b[k];
                    }

                    // Report progress
                    progress?.Report((k + 1) * 100 / n);
                }

                // Back substitution
                var solution = new double[n];
                for (int i = n - 1; i >= 0; i--)
                {
                    double sum = b[i];
                    for (int j = i + 1; j < n; j++)
                    {
                        sum -= a[i][j] * solution[j];
                    }
                    solution[i] = sum / a[i][i];
                    
                    // Check for NaN or Infinity
                    if (double.IsNaN(solution[i]) || double.IsInfinity(solution[i]))
                    {
                        return new MatrixSolution
                        {
                            Success = false,
                            ErrorMessage = "Solution contains invalid values (NaN or Infinity)",
                            Size = n
                        };
                    }
                }

                return new MatrixSolution
                {
                    Success = true,
                    Solution = solution,
                    Size = n,
                    SolvedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new MatrixSolution
                {
                    Success = false,
                    ErrorMessage = $"Error during calculation: {ex.Message}",
                    Size = coefficients.Length
                };
            }
        });
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
                // Generate random value in range [minValue, maxValue]
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

