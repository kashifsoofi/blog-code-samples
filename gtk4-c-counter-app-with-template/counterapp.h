#pragma once

#include <gtk/gtk.h>


#define COUNTER_APP_TYPE (counter_app_get_type ())
G_DECLARE_FINAL_TYPE (CounterApp, counter_app, COUNTER, APP, GtkApplication)


CounterApp     *counter_app_new         (void);
