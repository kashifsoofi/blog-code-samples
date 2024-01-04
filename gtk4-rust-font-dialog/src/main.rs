use glib::clone;
use gtk::prelude::*;
use gtk::{
    self, gio, glib, pango::FontDescription, Application, ApplicationWindow, Button, FontDialog, FontDialogButton,
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

    let current_font = font_dialog_button.font_desc().expect("").to_string();
    let label_font = Label::builder()
        .label(current_font.to_string())
        .margin_top(12)
        .margin_bottom(12)
        .margin_start(12)
        .margin_end(12)
        .build();

    let button_select_font = Button::builder()
        .label("Select Font")
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
    gtk_box.append(&label_font);
    gtk_box.append(&button_select_font);

    // Create a window and set the title
    let window = ApplicationWindow::builder()
        .application(app)
        .title("GTK Choose Font")
        .child(&gtk_box)
        .build();

    button_select_font.connect_clicked(clone!(@weak window, @weak label_font =>
        move |_| {
            let font_dialog = FontDialog::builder().modal(false).build();
            let current_font = label_font.label();

            font_dialog.choose_font(
                Some(&window),
                Some(&FontDescription::from_string(&current_font.as_str())),
                None::<&gio::Cancellable>,
                clone!(@weak label_font => move |result| {
                    if let Ok(font_desc) = result {
                        label_font.set_label(&font_desc.to_string());
                    }
                }));
        }));

    // Present window
    window.present();
}
