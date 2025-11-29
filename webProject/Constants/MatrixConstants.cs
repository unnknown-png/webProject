namespace webProject.Constants;

/// <summary>
/// Constants for matrix computation and validation
/// </summary>
public static class MatrixConstants
{
    // Matrix size validation
    public const int MinMatrixSize = 2;
    public const int MaxMatrixSize = 4000;
    public const double MaxMatrixValue = 1e10;
    public const int SmallMatrixThreshold = 10; // Matrices < 10 show full results
    
    // Computation delays (milliseconds) - for UI responsiveness
    public const int InitializationDelay = 100;
    public const int SmallMatrixBaseDelay = 200;
    public const int ProgressUpdateDelay = 50;
    public const int StepDelay = 30;
    public const int FinalizationDelay = 100;
    public const int VerificationDelay = 20;
    
    // Progress percentages
    public const int ProgressInitStart = 0;
    public const int ProgressInitEnd = 5;
    public const int ProgressForwardStart = 5;
    public const int ProgressForwardEnd = 65;
    public const int ProgressBackStart = 65;
    public const int ProgressBackEnd = 95;
    public const int ProgressFinalizing = 95;
    public const int ProgressCompleted = 100;
    
    // LU Decomposition progress
    public const int LUProgressStart = 0;
    public const int LUProgressDecomposeEnd = 50;
    public const int LUProgressVerifyStart = 55;
    public const int LUProgressDeterminantStart = 75;
    public const int LUProgressAdditionalStart = 85;
    
    // Computation thresholds
    public const int SmallMatrixThresholdForDelay = 100; // Matrices < 100 have delays for visualization
    public const int LargeMatrixThresholdForHeavyComputation = 50; // Matrices >= 50 trigger additional computations
    
    // Iteration counts for heavy computations
    public const int LUVerificationIterations = 10;
    public const int FrobeniusNormIterations = 15;
    public const int AdditionalMatrixMultiplications = 5;
    
    // Numerical precision
    public const double SingularityThreshold = 1e-12; // Threshold for considering a matrix singular
    
    // Cache settings
    public const int MatrixCacheExpirationMinutes = 30;
}

/// <summary>
/// Constants for SignalR and progress tracking
/// </summary>
public static class ProgressConstants
{
    public const string ProgressHubUrl = "/progressHub";
    public const string ReceiveProgressMethod = "ReceiveProgress";
    
    public const int ProgressClearDelay = 2000; // 2 seconds
    public const int ReconnectionDelay = 5000; // 5 seconds
}

/// <summary>
/// Constants for history and export
/// </summary>
public static class HistoryConstants
{
    public const int MaxHistoryItemsToShow = 50;
    public const string ExportFileName = "gauss-history.json";
}

