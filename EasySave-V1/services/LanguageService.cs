using BackupApp;
using System.Globalization;
using System.Reflection;
using System.Resources;

public class LanguageService
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;
    private readonly AppConfig _config;

    public LanguageService()
    {
        _config = AppConfig.Load();
        _resourceManager = new ResourceManager("EasySave_V1.Resources.Strings", Assembly.GetExecutingAssembly());

        try
        {
            _currentCulture = new CultureInfo(_config.DefaultLanguage);
        }
        catch
        {
            _currentCulture = CultureInfo.InvariantCulture;
        }
    }

    public void SetLanguage(string languageCode)
    {
        try
        {
            var newCulture = new CultureInfo(languageCode);
            _currentCulture = newCulture;
            _config.DefaultLanguage = languageCode;
            _config.Save();
        }
        catch
        {
            // Fallback to default if invalid language code
            _currentCulture = CultureInfo.InvariantCulture;
        }
    }

    public string GetString(string key)
    {
        try
        {
            string result = _resourceManager.GetString(key, _currentCulture);
            return result ?? $"[{key}]"; // Return placeholder if not found
        }
        catch
        {
            return $"[{key}]"; // Return placeholder on error
        }
    }
}