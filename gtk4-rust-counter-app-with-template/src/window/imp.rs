use std::cell::Cell;
use std::rc::Rc;

use glib::clone;
use glib::subclass::InitializingObject;
use gtk::prelude::*;
use gtk::subclass::prelude::*;
use gtk::{glib, Button, CompositeTemplate, Label};

// Object holding the state
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

// Trait shared by all GObjects
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

// Trait shared by all widgets
impl WidgetImpl for Window {}

// Trait shared by all windows
impl WindowImpl for Window {}

// Trait shared by all application windows
impl ApplicationWindowImpl for Window {}