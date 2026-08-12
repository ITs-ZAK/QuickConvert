namespace QuickConvert.App;

public static class FormatEmptyState
{
    public static string GetMessage(int selectedFileCount, bool hasCompatibleFormats)
    {
        if (selectedFileCount == 0)
            return "Najpierw wybierz pliki";
        return hasCompatibleFormats
            ? string.Empty
            : "Brak wspólnego formatu dla tego zestawu plików";
    }
}
