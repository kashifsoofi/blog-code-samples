# Counter App with GTK4 CompositeTemplate and .NET 8
## GTK and .NET
GTK is a free and open-source cross-platform widget toolkit for creating graphical user interfaces (GUIs).

.NET is the free, open-source, cross-platform framework for building modern apps and powerful cloud services.

Focus of this tutorial is to write a counter app with GTK 4 and C# targeting .NET 8.

## Project Setup
Let's begin by installing all necessary tools if not already setup. First, follow the instructions on the [GTK website](https://www.gtk.org/docs/installations/) in order to install GTK 4. Then install .NET by following instructions for your platform from [download](https://dotnet.microsoft.com/en-us/download) page. We are targeting GTK4, .NET 8 and Gir.Core.Gtk-4.0 0.5.0-preview.3.

Now lets create a new empty folder named `gtk4-dotnet8-counter-app-with-template` and execute following to create an empty solution:
```shell
dotnet new sln
```

Next we will add a console application and add that to our solution
```shell
dotnet new console -o Counter.App
dotnet sln add Counter.App/Counter.App.csproj
```
New lets add C# bindings for Gtk4 to our `Counter.App` project
```shell
cd Counter.App
dotnet add package GirCore.Gtk-4.0 --version 0.5.0-preview.3
```
Now we can run our application by executing:
```shell
dotnet run
```
At this moment it would print `Hello, world!`.

## Window Template
Lets add a new template `CounterAppWindow.ui` to our project and set it as `EmbeddedResource`.
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

## Add Class
Lets add a class named `CounterAppWindow` to match the template file name.
```csharp
```

## Source
Source code for the demo application is hosted on GitHub in [blog-code-samples](https://github.com/kashifsoofi/blog-code-samples/tree/main/gtk4-dotnet8-counter-app-with-template) repository.

## References
In no particular order
* [GTK](https://www.gtk.org/)
* [GTK Installation](https://www.gtk.org/docs/installations/)
* [.NET](https://dotnet.microsoft.com/en-us/)
* [.NET Download](https://dotnet.microsoft.com/en-us/download)
* [Gir.Core](https://github.com/gircore/gir.core)
* [GirCore.Gtk-4.0](https://www.nuget.org/packages/GirCore.Gtk-4.0/)
* [Gir.Core Gtk-4.0 Samples](https://github.com/gircore/gir.core/tree/main/src/Samples/Gtk-4.0)
* And many more