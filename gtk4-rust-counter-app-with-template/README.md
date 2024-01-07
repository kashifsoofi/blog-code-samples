# Counter App with GTK4 CompositeTemplate and Rust
## GTK and Rust
GTK is a free and open-source cross-platform widget toolkit for creating graphical user interfaces (GUIs).

Rust is a fast, reliable and productive language for building software on embedded devices, web services and more. It is a system programming language focused on safety, speed and concurrency.

Focus of this tutorial is to write a counter app with GTK 4 Composite Template and Rust. There is a much more comprehensive write up and walkthrough in [GUI development with Rust and GTK 4](https://gtk-rs.org/gtk4-rs/stable/latest/book/composite_templates.html) book. This is just a simplified version of that recreating our [counter app](https://github.com/kashifsoofi/blog-code-samples/tree/main/gtk4-rust-counter-app) with composite template.

## Project Setup
Let's begin by installing all necessary tools if setup is not already complete. First, follow the instructions on the [GTK website](https://www.gtk.org/docs/installations/) in order to install GTK 4. Then install Rust with [rustup](https://rustup.rs/). We are targeting GTK4, Rust 1.75 and gtk-rs version 0.7.3 with features `v4_12`.

Now lets create a new project by executing:
```shell
cargo new gtk4-rust-counter-app-template
```

Add [gtk4 crate]() to your dependencies in `Cargo.toml`. 
```shell
cargo add gtk4 --rename gtk --features v4_12
```

Now we can run our application by executing:
```shell
cargo run
```
At this moment it would print `Hello, world!`.

## Window Template
Lets start by adding a template for our counter app named `window.ui`, under `resources` folder.
```xml
<?xml version="1.0" encoding="UTF-8"?>
<interface>
  <template class="CounterAppWindow" parent="GtkApplicationWindow">
    <property name="title">GTK Template Counter App</property>
    <child>
      <object class="GtkBox">
        <property name="orientation">vertical</property>
        <property name="margin-start">12</property>
        <property name="margin-end">12</property>
        <property name="spacing">12</property>
        <child>
          <object class="GtkLabel" id="label_counter">
            <property name="label">0</property>
            <property name="margin-top">12</property>
            <property name="margin-bottom">12</property>
            <property name="margin-start">12</property>
            <property name="margin-end">12</property>  
          </object>
        </child>
        <child>
          <object class="GtkButton" id="button_increase">
            <property name="label">Increase</property>
            <property name="margin-top">12</property>
            <property name="margin-bottom">12</property>
            <property name="margin-start">12</property>
            <property name="margin-end">12</property>  
          </object>
        </child>
        <child>
          <object class="GtkButton" id="button_decrease">
            <property name="label">Decrease</property>
            <property name="margin-top">12</property>
            <property name="margin-bottom">12</property>
            <property name="margin-start">12</property>
            <property name="margin-end">12</property>  
          </object>
        </child>
      </object>
    </child>
  </template>
</interface>
```

## Resources
We would take advantage of `gio::Resource` to embed the template file into our application. The files to embed are described by an xml file. For our template file we also add the compressed and preprocess attribute in order to reduce the final size of the resources. Lets add another file `resources.gresource.xml` under `resources` folder.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<gresources>
  <gresource prefix="/org/gtk_rs/GTK4CounterTemplate/">
    <file compressed="true" preprocess="xml-stripblanks">window.ui</file>
  </gresource>
</gresources>
```

We would execute `glib_build_tools::compile_resources` within a cargo [build script](https://doc.rust-lang.org/cargo/reference/build-scripts.html) to compile the resources and link it to our application.

We will add `glib-build-tools` as build dependency in `Cargo.toml` by executing
```shell
cargo add glib-build-tools --build
```

Lets add `build.rs` file at the root of the project. This will compile the resources whenever we trigger a build with cargo and then statically link our executable to them.
```rust
fn main() {
    glib_build_tools::compile_resources(
        &["resources"],
        "resources/resources.gresource.xml",
        "counterapp.gresource",
    );
}
```

## Use Resources
Next we would register and include the resources by calling the macro [gio::resources_register_include](https://gtk-rs.org/gtk-rs-core/stable/latest/docs/gio/macro.resources_register_include.html). Remember to always register the resources before creating the `gtk::Application`.

`main.rs` would look like following, it won't compile just yet as we still have to add code for our window.
```rust
mod window;

use gtk::prelude::*;
use gtk::{gio, glib, Application};
use window::Window;

const APP_ID: &str = "org.gtk_rs.GTK4CounterTemplate";

fn main() -> glib::ExitCode {
    // Register and include resources
    gio::resources_register_include!("counterapp.gresource")
        .expect("Failed to register resources.");

    // Create a new application
    let app = Application::builder().application_id(APP_ID).build();

    // Connect to "activate" signal of `app`
    app.connect_activate(build_ui);

    // Run the application
    app.run()
}

fn build_ui(app: &Application) {
    // Create new window and present it
    let window = Window::new(app);
    window.present();
}
```

## Application Window
We will create a custom widget in our code inheriting from `gtk::ApplicationWindow` to make use of our template.

Lets add `mod.rs` with following content.
```rust
mod imp;

use glib::Object;
use gtk::{gio, glib, Application};

glib::wrapper! {
    pub struct Window(ObjectSubclass<imp::Window>)
        @extends gtk::ApplicationWindow, gtk::Window, gtk::Widget,
        @implements gio::ActionGroup, gio::ActionMap, gtk::Accessible, gtk::Buildable,
                    gtk::ConstraintTarget, gtk::Native, gtk::Root, gtk::ShortcutManager;
}

impl Window {
    pub fn new(app: &Application) -> Self {
        // Create new window
        Object::builder().property("application", app).build()
    }
}
```

Next we will add a new file under `src/window` named `imp.rs`. I am following the same naming as the GTK4 Rust book, but feel free to name it after the window, this would be more appropriate if we have multiple windows.

We will start by adding `Window` struct and adding derive macro `CompositeTemplate` to the struct and we will also specify the template name.
We will also add struct member of type `TemplateChild` for each of the widgets we added with `id` in our template, this would allow us to access those widgets later. We will also add a struct member to hold the current counter that we will update and display in `label_counter` widget in our button click handlers.
```rust
#[derive(CompositeTemplate, Default)]
#[template(resource = "/org/gtk_rs/GTK4CounterTemplate/window.ui")]
pub struct Window {
    #[template_child]
    pub label_counter: TemplateChild<Label>,
    #[template_child]
    pub button_increase: TemplateChild<Button>,
    #[template_child]
    pub button_decrease: TemplateChild<Button>,

    pub counter: Rc<Cell<i32>>,
}
```

We will implement `ObjectSubclass` for our struct. Here we make sure that `NAME` matches `class` attribute value in template and `ParentType` matches `parent` attribute value.
```rust
// The central trait for subclassing a GObject
#[glib::object_subclass]
impl ObjectSubclass for Window {
    // `NAME` needs to match `class` attribute of template
    const NAME: &'static str = "CounterAppWindow";
    type Type = super::Window;
    type ParentType = gtk::ApplicationWindow;

    fn class_init(klass: &mut Self::Class) {
        klass.bind_template();
    }

    fn instance_init(obj: &InitializingObject<Self>) {
        obj.init_template();
    }
}
```

Next we connect the callbacks on the clicked signal of the buttons within `constructed` method of `ObjectImpl` trait. The widgets are available as struct members using `self`.

```rust
impl ObjectImpl for Window {
    fn constructed(&self) {
        // Call "constructed" on parent
        self.parent_constructed();

        // Connect to "clicked" signal of `button_increase`
        self.button_increase.connect_clicked(clone!(@weak self as obj => move |_| {
            obj.counter.set(obj.counter.get() + 1);
            obj.label_counter.set_label(&obj.counter.get().to_string());
        }));

        // Connect to "clicked" signal of `button_decrease`
        self.button_decrease.connect_clicked(clone!(@weak self as obj => move |_| {
            obj.counter.set(obj.counter.get() - 1);
            obj.label_counter.set_label(&obj.counter.get().to_string());
        }));
    }
}
```

Finally we will implement other traits need to successfully build our project.
```rust
// Trait shared by all widgets
impl WidgetImpl for Window {}

// Trait shared by all windows
impl WindowImpl for Window {}

// Trait shared by all application windows
impl ApplicationWindowImpl for Window {}
```

And thats it for this tutorial/sample, running this would diaplay the counter app window and we can click on Increase/Decrease button and see the value updated in the label.

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/gtk4-rust-counter-app) repository.

## References
In no particular order
* [GTK](https://www.gtk.org/)
* [GTK Installation](https://www.gtk.org/docs/installations/)
* [Rust](https://www.rust-lang.org/)
* [rustup](https://rustup.rs/)
* [gtk-rs](https://gtk-rs.org/)
* [grk4 crate](https://crates.io/crates/gtk4)
* [GUI development with Rust and GTK 4](https://gtk-rs.org/gtk4-rs/stable/latest/book/)
* [glib_build_tools::compile_resources](https://gtk-rs.org/gtk-rs-core/stable/latest/docs/glib_build_tools/fn.compile_resources.html)
* [Build Scripts](https://doc.rust-lang.org/cargo/reference/build-scripts.html)
* [gio::resources_register_include](https://gtk-rs.org/gtk-rs-core/stable/latest/docs/gio/macro.resources_register_include.html)
* And many more