use glib::clone;
use gtk::prelude::*;
use gtk::{
    self, gio, glib, pango, Application, ApplicationWindow, Button, FontDialog, FontDialogButton,
    Label, Orientation,
};

const APP_ID: &str = "org.gtk_rs.GTK4FontDialog";

fn main() -> glib::ExitCode {
    // Create a new application
    let app = Application::builder().application_id(APP_ID).build();

    // Connect to "activate" signal of `app`
    app.connect_activate(build_ui);

    // Run the application
    app.run()
}

fn build_ui(app: &Application) {
    let font_dialog = FontDialog::builder().modal(false).build();
    let font_dialog_button = FontDialogButton::builder()
        .dialog(&font_dialog)
        .margin_top(12)
        .margin_bottom(12)
        .margin_start(12)
        .margin_end(12)
        .build();

    // Add buttons to `gtk_box`
    let gtk_box = gtk::Box::builder()
        .orientation(Orientation::Vertical)
        .build();
    gtk_box.append(&font_dialog_button);

    // Create a window and set the title
    let window = ApplicationWindow::builder()
        .application(app)
        .title("GTK Choose Font")
        .child(&gtk_box)
        .build();
        
    // Present window
    window.present();
}
