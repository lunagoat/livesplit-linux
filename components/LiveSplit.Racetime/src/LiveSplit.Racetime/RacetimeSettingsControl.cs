using LiveSplit.UI;
namespace LiveSplit.Racetime;
public class RacetimeSettingsControl : SettingsBase
{
    private RacetimeSettings settings;
    public RacetimeSettings Settings { get => settings; set => SetField(ref settings, value); }
    public string Message => "There are no options available for this racing service.";
}
