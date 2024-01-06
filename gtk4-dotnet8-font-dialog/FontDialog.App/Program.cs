﻿var application = Gtk.Application.New("org.GirCore.GTK4FontDialog", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var fontDialog = Gtk.FontDialog.New();
    var fontDialogButton = Gtk.FontDialogButton.New(fontDialog);
    fontDialogButton.SetMarginTop(12);
    fontDialogButton.SetMarginBottom(12);
    fontDialogButton.SetMarginStart(12);
    fontDialogButton.SetMarginEnd(12);

    var currentFont = fontDialogButton.GetFontDesc();
    var labelFont = Gtk.Label.New(currentFont?.ToString());
    labelFont.SetMarginTop(12);
    labelFont.SetMarginBottom(12);
    labelFont.SetMarginStart(12);
    labelFont.SetMarginEnd(12);

    var buttonSelectFont = Gtk.Button.New();
    buttonSelectFont.Label = "Select Font";
    buttonSelectFont.SetMarginTop(12);
    buttonSelectFont.SetMarginBottom(12);
    buttonSelectFont.SetMarginStart(12);
    buttonSelectFont.SetMarginEnd(12);
    buttonSelectFont.OnClicked += (_, _) =>
    {
        var fontDialog = Gtk.FontDialog.New();
        fontDialog.RunDispose();
    };

    var gtkBox = Gtk.Box.New(Gtk.Orientation.Vertical, 0);
    gtkBox.Append(fontDialogButton);
    gtkBox.Append(labelFont);
    gtkBox.Append(buttonSelectFont);

    var window = Gtk.ApplicationWindow.New((Gtk.Application)sender);
    window.Title = "GTK Choose Font";
    window.SetDefaultSize(300, 300);
    window.Child = gtkBox;
    window.Show();
};
return application.RunWithSynchronizationContext();