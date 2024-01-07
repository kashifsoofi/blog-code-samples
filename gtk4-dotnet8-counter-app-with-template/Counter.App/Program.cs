using System.Reflection;
using Counter.App;

Console.WriteLine("1");
Gio.Functions.ResourcesRegister(Gio.Functions.ResourceLoad(Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!) + "/counterapp.gresource"));
Console.WriteLine("2");
var application = Gtk.Application.New("org.GirCore.GTK4Counter", Gio.ApplicationFlags.FlagsNone);
application.OnActivate += (sender, args) =>
{
    var builder = new Gtk.Builder($"{nameof(CounterAppWindow)}.ui");
    Console.WriteLine($"root: {builder.GetPointer("_root")}");
    Console.WriteLine($"CounterAppWindow: {builder.GetPointer("CounterAppWindow")}");
    Console.WriteLine($"label_counter: {builder.GetPointer("label_counter")}");
    var window = new CounterAppWindow(application);
    window.Start();
};
return application.RunWithSynchronizationContext(null);