namespace DivisiBillWsClient;

public partial class CallStatusPage : ContentPage
{
    public CallStatusPage()
    {
        InitializeComponent();
        Loaded += async (object? sender, EventArgs e) =>
        {
            if (BindingContext is MainPageViewModel vm)
                await vm.OnLoadedAsync();
        };
    }

    private void OnBaseUrlEntryCompleted(object? sender, EventArgs e)
    {
        if (BindingContext is MainPageViewModel vm)
        {
            vm.CommitBaseUrlTextCommand.Execute(null);
        }
    }
}
