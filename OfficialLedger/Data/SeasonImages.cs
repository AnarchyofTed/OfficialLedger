namespace OfficialLedger.Models;

public static class SeasonImages
{
    public static readonly (string Key, string Label)[] Options =
    {
        ("baseball", "Baseball"), ("football", "Football"), ("soccer", "Soccer"),
        ("basketball", "Basketball"), ("softball", "Softball"), ("volleyball", "Volleyball"),
        ("tennis", "Tennis"), ("golf", "Golf"), ("hockey", "Hockey"), ("other", "Other"),
        ("none", "No image")
    };

    public static string Path(string key) =>
        $"/images/sports/{(Options.Any(x => x.Key == key) ? key : "other")}.svg?v=2";
}
