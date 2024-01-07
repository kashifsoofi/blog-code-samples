using Gtk;
namespace Counter.App;

public class CounterAppWindow : ApplicationWindow
{
    // private static void ClassInit(Type gClass, System.Type type, IntPtr classData)
    // {
        
    //         SetTemplate(
    //             gtype: gClass, 
    //             template: Assembly.GetExecutingAssembly().ReadResource("CompositeWidget.ui")
    //         );
    //         BindTemplateChild(gClass, nameof(Button));
    //         BindTemplateSignals(gClass, type);
    // }

    // protected override void Initialize()
    //     {
    //         InitTemplate();
    //         ConnectTemplateChildToField(nameof(Button), ref Button);
    //     }

    private readonly Application _application;

    [Connect("label_counter")] private readonly Label? labelCounter;
    [Connect("button_increase")] private readonly Button? buttonIncrease;
    [Connect("button_decrease")] private readonly Button? buttonDecrease;

    private int counter = 0;

    private CounterAppWindow(Builder builder, Application application)
        : base(builder.GetPointer("CounterAppWindow"), false)
    {
        _application = application;
    }

    public CounterAppWindow(Application application)
        : this(new Builder($"{nameof(CounterAppWindow)}.ui"), application)
    { }

    public void Start()
    {
        _application.AddWindow(this);
        Show();
    }

    private void ButtonIncrease_OnClicked(object sender, EventArgs args)
    {
        counter++;
        labelCounter?.SetLabel(counter.ToString());
    }

    private void ButtonDecrease_OnClicked(object sender, EventArgs args)
    {
        counter--;
        labelCounter?.SetLabel(counter.ToString());
    }
}