
namespace LiveSplit.Localization;

public static class UiLocalizer
{
    public static string Translate(string source)
    {
        return Translate(source, LanguageResolver.ResolveCurrentCultureLanguage());
    }

    public static string Translate(string source, AppLanguage language)
    {
        if (string.IsNullOrEmpty(source) || language == null || !language.RequiresLocalization)
        {
            return source;
        }

        return UiTextCatalog.TryGetTranslation(source, language, out string translated)
            ? translated
            : source;
    }

    public static string TranslateKey(string key, string fallback)
    {
        return TranslateKey(key, fallback, LanguageResolver.ResolveCurrentCultureLanguage());
    }

    public static string TranslateKey(string key, string fallback, AppLanguage language)
    {
        if (UiTextCatalog.TryGetKeyTranslation(language, key, out string translated))
        {
            return translated;
        }

        return Translate(fallback, language);
    }
}
