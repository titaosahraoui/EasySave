using BackupApp;
using ReactiveUI;
using System.Globalization;
using System.Reflection;
using System.Resources;

public class LanguageService : ReactiveObject
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

    public string GetCurrentLanguage()
    {
        return _currentCulture.Name;
    }

    public void SetLanguage(string languageCode)
    {
        try
        {
            var newCulture = new CultureInfo(languageCode);
            _currentCulture = newCulture;
            _config.DefaultLanguage = languageCode;
            _config.Save();
            this.RaisePropertyChanged(nameof(GetString)); // Notify UI to update
        }
        catch
        {
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