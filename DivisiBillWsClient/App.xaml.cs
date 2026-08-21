namespace DivisiBillWsClient;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell()) { Width = 800, Height = 800 };
    }

    public static new App Current => Application.Current as App ?? throw new ArgumentException("Application.Current is not of type App");

    public static void RequireCheckForProEdition() { }

    public static SettingsClass Settings { get; } = new SettingsClass();

}
public class SettingsClass
{
    public string UserKey { get; set; } = "";
}
