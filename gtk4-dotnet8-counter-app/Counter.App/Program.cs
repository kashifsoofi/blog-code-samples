var application = Gtk.Application.New("org.GirCore.GTK4Counter", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var counter = 0;

    var labelCounter = Gtk.Label.New(counter.ToString());
    labelCounter.SetMarginTop(12);
    labelCounter.SetMarginBottom(12);
    labelCounter.SetMarginStart(12);
    labelCounter.SetMarginEnd(12);

    var buttonIncrease = Gtk.Button.New();
    buttonIncrease.Label = "Increase";
    buttonIncrease.SetMarginTop(12);
    buttonIncrease.SetMarginBottom(12);
    buttonIncrease.SetMarginStart(12);
    buttonIncrease.SetMarginEnd(12);
    buttonIncrease.OnClicked += (_, _) =>
    {
        counter++;
        labelCounter.SetLabel(counter.ToString());
    };

    var buttonDecrease = Gtk.Button.New();
    buttonDecrease.Label = "Decrease";
    buttonDecrease.SetMarginTop(12);
    buttonDecrease.SetMarginBottom(12);
    buttonDecrease.SetMarginStart(12);
    buttonDecrease.SetMarginEnd(12);
    buttonDecrease.OnClicked += (_, _) =>
    {
        counter--;
        labelCounter.SetLabel(counter.ToString());
    };

    var gtkBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
    gtkBox.Append(labelCounter);
    gtkBox.Append(buttonIncrease);
    gtkBox.Append(buttonDecrease);

    var window = Gtk.ApplicationWindow.New((Gtk.Application)sender);
    window.Title = "GTK Counter App";
    window.SetDefaultSize(300, 300);
    window.Child = gtkBox;
    window.Show();
};
return application.RunWithSynchronizationContext();