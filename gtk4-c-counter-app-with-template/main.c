// https://gitlab.gnome.org/GNOME/gtk/-/tree/main/examples/application3
#include <gtk/gtk.h>

#include "counterapp.h"

int
main (int argc, char *argv[])
{
  return g_application_run (G_APPLICATION (counter_app_new ()), argc, argv);
}
