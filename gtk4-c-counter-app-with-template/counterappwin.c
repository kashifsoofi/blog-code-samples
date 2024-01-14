#include <gtk/gtk.h>

#include "counterapp.h"
#include "counterappwin.h"

struct _CounterAppWindow
{
  GtkApplicationWindow parent;
  GtkWidget *label_counter;

  int counter;
};

G_DEFINE_TYPE(CounterAppWindow, counter_app_window, GTK_TYPE_APPLICATION_WINDOW);

static void
button_increase_on_clicked(GtkButton *btn, CounterAppWindow *win)
{
  win->counter++;
  char counter_str[10];
  sprintf(counter_str, "%d", win->counter);
  gtk_label_set_label(GTK_LABEL(win->label_counter), counter_str);
}

static void
button_decrease_on_clicked(GtkButton *btn, CounterAppWindow *win)
{
  win->counter--;
  char counter_str[10];
  sprintf(counter_str, "%d", win->counter);
  gtk_label_set_label(GTK_LABEL(win->label_counter), counter_str);
}

static void
counter_app_window_init (CounterAppWindow *win)
{
  gtk_widget_init_template (GTK_WIDGET (win));
}

static void
counter_app_window_class_init (CounterAppWindowClass *class)
{
  gtk_widget_class_set_template_from_resource (GTK_WIDGET_CLASS (class),
                                               "/org/gtk/counterapp/window.ui");
  
  gtk_widget_class_bind_template_child (GTK_WIDGET_CLASS (class), CounterAppWindow, label_counter);

  gtk_widget_class_bind_template_callback (GTK_WIDGET_CLASS (class), button_increase_on_clicked);
  gtk_widget_class_bind_template_callback (GTK_WIDGET_CLASS (class), button_decrease_on_clicked);
}

CounterAppWindow *
counter_app_window_new (CounterApp *app)
{
  return g_object_new (COUNTER_APP_WINDOW_TYPE, "application", app, NULL);
}

void
counter_app_window_open (CounterAppWindow *win,
                         GFile            *file)
{
}
