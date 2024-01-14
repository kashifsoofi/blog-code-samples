#include <gtk/gtk.h>

#include "counterapp.h"
#include "counterappwin.h"

struct _CounterApp
{
  GtkApplication parent;
};

G_DEFINE_TYPE(CounterApp, counter_app, GTK_TYPE_APPLICATION);

static void
counter_app_init (CounterApp *app)
{
}

static void
counter_app_activate (GApplication *app)
{
  CounterAppWindow *win;

  win = counter_app_window_new (COUNTER_APP (app));
  gtk_window_present (GTK_WINDOW (win));
}

static void
counter_app_open (GApplication  *app,
                  GFile        **files,
                  int            n_files,
                  const char    *hint)
{
  GList *windows;
  CounterAppWindow *win;
  int i;

  windows = gtk_application_get_windows (GTK_APPLICATION (app));
  if (windows)
    win = COUNTER_APP_WINDOW (windows->data);
  else
    win = counter_app_window_new (COUNTER_APP (app));

  for (i = 0; i < n_files; i++)
    counter_app_window_open (win, files[i]);

  gtk_window_present (GTK_WINDOW (win));
}

static void
counter_app_class_init (CounterAppClass *class)
{
  G_APPLICATION_CLASS (class)->activate = counter_app_activate;
  G_APPLICATION_CLASS (class)->open = counter_app_open;
}

CounterApp *
counter_app_new (void)
{
  return g_object_new (COUNTER_APP_TYPE,
                       "application-id", "org.gtk.counterapp",
                       "flags", G_APPLICATION_HANDLES_OPEN,
                       NULL);
}

