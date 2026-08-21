namespace DivisiBillWsClient
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
        }

        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);
            TitleLabel.Text = Current.CurrentPage.Title;
        }
    }
}
