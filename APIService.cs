using NITGEN.SDK.NBioBSP;

namespace BiometricService
{
    public sealed class APIService : BackgroundService
    {
        private readonly ILogger<APIService> _logger;
        public NBioAPI _NBioAPI;
        public NBioAPI.IndexSearch _IndexSearch;
        
        public APIService(ILogger<APIService> logger) 
        {
            _logger = logger;
            _NBioAPI = new NBioAPI();
            _IndexSearch = new NBioAPI.IndexSearch(_NBioAPI);
            _IndexSearch.InitEngine();

            string filepath = "C:\\Windows\\System32\\NBSP2Por.dll";
            if (File.Exists(filepath))
            {
                _NBioAPI.SetSkinResource(filepath);
            }
            else
            {
                _logger.LogWarning("NBSP2Por.dll not found at {Path}; biometric capture UI will use default skin.", filepath);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Biometric API Service is starting.");
            try
            {
                _logger.LogInformation("Biometric API Service is running.");
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(5000, stoppingToken);
                }
            }
            catch (OperationCanceledException)      // Has been canceled manually
            {
                _NBioAPI.Dispose();
                _IndexSearch.TerminateEngine();
                _logger.LogInformation("Biometric API Service has been stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Biometric API Service: {Message}", ex.Message);
            }
        }
    }
}
