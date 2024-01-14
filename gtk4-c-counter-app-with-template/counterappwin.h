#pragma once

#include <gtk/gtk.h>
#include "counterapp.h"


#define COUNTER_APP_WINDOW_TYPE (counter_app_window_get_type ())
G_DECLARE_FINAL_TYPE (CounterAppWindow, counter_app_window, COUNTER, APP_WINDOW, GtkApplicationWindow)


CounterAppWindow       *counter_app_window_new          (CounterApp *app);
void                    counter_app_window_open         (CounterAppWindow *win,
                                                         GFile            *file);
