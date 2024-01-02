use std::cell::Cell;
use std::rc::Rc;

use glib::clone;
use gtk::prelude::*;
use gtk::{glib, Application, ApplicationWindow, Label, Box, Button, Orientation};

const APP_ID: &str = "org.gtk_rs.GTK4Counter";

fn main() -> glib::ExitCode {
// Create a new application
    let app = Application::builder().application_id(APP_ID).build();

    // Connect to "activate" signal of `app`
    app.connect_activate(build_ui);

    // Run the application
    app.run()
}

fn build_ui(app: &Application) {
    let counter = Rc::new(Cell::new(0));
    let label_counter = Label::builder()
        .label(&counter.get().to_string())
        .margin_top(12)
        .margin_bottom(12)
        .margin_start(12)
        .margin_end(12)
        .build();

    let button_increase = Button::builder()
        .label("Increase")
        .margin_top(12)
        .margin_bottom(12)
        .margin_start(12)
        .margin_end(12)
        .build();
        
    let button_decrease = Button::builder()
        .label("Decrease")
        .margin_top(12)
        .margin_bottom(12)
        .margin_start(12)
        .margin_end(12)
        .build();
    
    button_increase.connect_clicked(clone!(@weak counter, @weak label_counter =>
        move |_| {
            counter.set(counter.get() + 1);
            label_counter.set_label(&counter.get().to_string());
    }));

    button_decrease.connect_clicked(clone!(@weak label_counter =>
        move |_| {
            counter.set(counter.get() - 1);
            label_counter.set_label(&counter.get().to_string());
    }));

    let gtk_box = Box::builder()
        .orientation(Orientation::Vertical)
        .build();
    gtk_box.append(&label_counter);
    gtk_box.append(&button_increase);
    gtk_box.append(&button_decrease);

    // Create a window and set the title
    let window = ApplicationWindow::builder()
        .application(app)
        .title("GTK Counter App")
        .child(&gtk_box)
        .build();

    // Present window
    window.present();
}
