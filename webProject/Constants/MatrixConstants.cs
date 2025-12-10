namespace webProject.Constants;

public static class MatrixConstants
{
    public const int MinMatrixSize = 2;
    public const int MaxMatrixSize = 4000;
    public const double MaxMatrixValue = 1e10;
    public const int SmallMatrixThreshold = 10; 
    
    public const int InitializationDelay = 100;
    public const int SmallMatrixBaseDelay = 200;
    public const int ProgressUpdateDelay = 50;
    public const int StepDelay = 30;
    public const int FinalizationDelay = 100;
    public const int VerificationDelay = 20;
    
    public const int ProgressInitStart = 0;
    public const int ProgressInitEnd = 5;
    public const int ProgressForwardStart = 5;
    public const int ProgressForwardEnd = 65;
    public const int ProgressBackStart = 65;
    public const int ProgressBackEnd = 95;
    public const int ProgressFinalizing = 95;
    public const int ProgressCompleted = 100;
    
    public const int LUProgressStart = 0;
    public const int LUProgressDecomposeEnd = 50;
    public const int LUProgressVerifyStart = 55;
    public const int LUProgressDeterminantStart = 75;
    public const int LUProgressAdditionalStart = 85;
    
    public const int SmallMatrixThresholdForDelay = 100; 
    public const int LargeMatrixThresholdForHeavyComputation = 50; 
    
    public const int LUVerificationIterations = 10;
    public const int FrobeniusNormIterations = 15;
    public const int AdditionalMatrixMultiplications = 5;
    
    public const double SingularityThreshold = 1e-12; 
    
    public const int MatrixCacheExpirationMinutes = 30;
}

public static class ProgressConstants
{
    public const string ProgressHubUrl = "/progressHub";
    public const string ReceiveProgressMethod = "ReceiveProgress";
    
    public const int ProgressClearDelay = 2000; 
    public const int ReconnectionDelay = 5000; 
}

public static class HistoryConstants
{
    public const int MaxHistoryItemsToShow = 50;
    public const string ExportFileName = "gauss-history.json";
}

