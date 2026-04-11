using BasketManager.ViewModels;

namespace BasketManager;

public partial class CanchaPage : ContentPage
{
	public CanchaPage(MainViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
    }
}