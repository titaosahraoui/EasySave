using System.Globalization;
using System.Reflection;
using System.Resources;

public class LanguageService
{
    private readonly ResourceManager _resourceManager;
    private CultureInfo _currentCulture;

    public LanguageService()
    {
        // Match this to your project's root namespace
        _resourceManager = new ResourceManager("EasySave_V1.Resources.Strings",
                                            Assembly.GetExecutingAssembly());
        _currentCulture = CultureInfo.CurrentCulture;
    }

    public void SetLanguage(string languageCode)
    {
        _currentCulture = new CultureInfo(languageCode);
    }

    public string GetString(string key)
    {
        try
        {
            return _resourceManager.GetString(key, _currentCulture) ?? $"[{key}]";
        }
        catch
        {
            return $"[{key}]";
        }
    }
}