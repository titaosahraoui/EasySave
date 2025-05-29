using System;
using System.Globalization;

namespace BackupApp
{
    public class LanguageBinding
    {
        private readonly LanguageService _languageService;

        public LanguageBinding()
        {
            _languageService = new LanguageService();
        }

        public string GetString(string key)
        {
            return _languageService.GetString(key);
        }
    }
}