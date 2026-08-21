namespace DivisiBillWsClient.Views;

public partial class ChangePasswordPopup : CommunityToolkit.Maui.Views.Popup<bool>
{
    public ChangePasswordPopup()
    {
        InitializeComponent();
        ChangePasswordViewModel vm = new(async (bool b) => await CloseAsync(b), null);
        BindingContext = vm;
    }
}