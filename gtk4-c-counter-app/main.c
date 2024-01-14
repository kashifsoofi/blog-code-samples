#include <gtk/gtk.h>

static int counter = 0;
GtkWidget *labelCounter;

static void increase_click_callback(GtkButton *btn)
{
  counter++;
  char counterStr[10];
  sprintf(counterStr, "%d", counter);
  gtk_label_set_label(GTK_LABEL(labelCounter), counterStr);
}

static void decrease_click_callback(GtkButton *btn)
{
  counter--;
  char counterStr[10];
  sprintf(counterStr, "%d", counter);
  gtk_label_set_label(GTK_LABEL(labelCounter), counterStr);
}

static void activate (GtkApplication* app, gpointer user_data)
{
  char counterStr[10];
  sprintf(counterStr, "%d", counter);
  labelCounter = gtk_label_new(counterStr);
  gtk_widget_set_margin_top(labelCounter, 12);
  gtk_widget_set_margin_bottom(labelCounter, 12);
  gtk_widget_set_margin_start(labelCounter, 12);
  gtk_widget_set_margin_bottom(labelCounter, 12);

  GtkWidget *buttonIncrease;
  buttonIncrease = gtk_button_new();
  gtk_button_set_label(GTK_BUTTON(buttonIncrease), "Increase");
  gtk_widget_set_margin_top(buttonIncrease, 12);
  gtk_widget_set_margin_bottom(buttonIncrease, 12);
  gtk_widget_set_margin_start(buttonIncrease, 12);
  gtk_widget_set_margin_end(buttonIncrease, 12);
  g_signal_connect (buttonIncrease, "clicked", G_CALLBACK (increase_click_callback), NULL);
 
  GtkWidget *buttonDecrease;
  buttonDecrease = gtk_button_new();
  gtk_button_set_label(GTK_BUTTON(buttonDecrease), "Decrease");
  gtk_widget_set_margin_top(buttonDecrease, 12);
  gtk_widget_set_margin_bottom(buttonDecrease, 12);
  gtk_widget_set_margin_start(buttonDecrease, 12);
  gtk_widget_set_margin_end(buttonDecrease, 12);
  g_signal_connect (buttonDecrease, "clicked", G_CALLBACK (decrease_click_callback), NULL);

  GtkWidget *gtkBox;
  gtkBox = gtk_box_new(GTK_ORIENTATION_VERTICAL, 0);
  gtk_box_append(GTK_BOX(gtkBox), labelCounter);
  gtk_box_append(GTK_BOX(gtkBox), buttonIncrease);
  gtk_box_append(GTK_BOX(gtkBox), buttonDecrease);

  GtkWidget *window;
  window = gtk_application_window_new (app);
  gtk_window_set_title (GTK_WINDOW (window), "GTK Counter App");
  gtk_window_set_default_size (GTK_WINDOW (window), 300, 300);
  gtk_window_set_child(GTK_WINDOW (window), gtkBox);
  gtk_window_present (GTK_WINDOW (window));
}

int
main (int    argc, char **argv)
{
  GtkApplication *app;
  int status;

  app = gtk_application_new ("org.gtk.counterapp", G_APPLICATION_DEFAULT_FLAGS);
  g_signal_connect (app, "activate", G_CALLBACK (activate), NULL);
  status = g_application_run (G_APPLICATION (app), argc, argv);
  g_object_unref (app);

  return status;
}
