# Choose Font with GTK4 and Rust

## GTK and Rust
GTK is a free and open-source cross-platform widget toolkit for creating graphical user interfaces (GUIs).

Rust is a fast, reliable and productive language for building software on embedded devices, web services and more. It is a system programming language focused on safety, speed and concurrency.

Focus of this tutorial is to write an app to choose font with GTK 4 and Rust.

## Project Setup
Let's begin by installing all necessary tools. First, follow the instructions on the [GTK website](https://www.gtk.org/docs/installations/) in order to install GTK 4. Then install Rust with [rustup](https://rustup.rs/). We are targeting GTK4, Rust 1.75 and gtk-rs version 0.7.3 with features `v4_12`.

Now lets create a new project by executing:
```
cargo new gtk4-rust-font-dialog
```

Add [gtk4 crate]() to your dependencies in `Cargo.toml`. 
```
cargo add gtk4 --rename gtk --features v4_12
```

## Application
Lets start by creating GTK Application and connecting to `activate` event of the Application. We will create a method `build_ui` to create a window and display it. GTK4 provides a widget `FontDialogButton`, that can be added to a preferences or settings window of the application. It displays current (default) font. Clicking on it will launch the `FontDialog` also provided by GTK4 that gives uer the ability to select a font.

The whole listing of the `main.rs` is below following the screenshots.

```rust
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
```
<figure>
  <a href="images/01-font-dialog-button.png"><img src="images/01-font-dialog-button.png"></a>
  <figcaption>Font Dialog Button</figcaption>
</figure>

<figure>
  <a href="images/02-font-dialog.png"><img src="images/02-font-dialog.png"></a>
  <figcaption>Font Dialog Button</figcaption>
</figure>

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/gtk4-rust-font-dialog) repository.

## References
In no particular order
* [GTK](https://www.gtk.org/)
* [GTK Installation](https://www.gtk.org/docs/installations/)
* [Rust](https://www.rust-lang.org/)
* [rustup](https://rustup.rs/)
* [gtk-rs](https://gtk-rs.org/)
* [grk4 crate](https://crates.io/crates/gtk4)
* [GUI development with Rust and GTK 4](https://gtk-rs.org/gtk4-rs/stable/latest/book/)
* 
* And many more