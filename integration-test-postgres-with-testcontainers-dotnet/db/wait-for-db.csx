#r "nuget: Npgsql, 5.0.0"

using System.Threading;
using System.Diagnostics;
using Npgsql;


string connectionString = Args[Args.Count - 1]; 

int retries = 120;
int interval = 500;
NpgsqlConnection connection = new NpgsqlConnection(connectionString);
for (int i = 0; i < retries; i++)
{
    try
    {
        connection.Open();
        Console.WriteLine("Database connected.");
        connection.Close();

        var arguments = "";
        for (int j = 1; j < Args.Count - 1; j++)
        {
            arguments += Args[j] + " ";
        }

        // Add connection string enclosed in ""
        arguments += "\"" + connectionString + "\"";

        Process process = new Process();
        // Configure the process using the StartInfo properties.
        process.StartInfo.FileName = Args[0];
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.Start();
        process.WaitForExit();// Waits here for the process to exit.

        Environment.Exit(0);
    }
    catch (NpgsqlException)
    {
        Console.WriteLine("Cannot establish connection to the DB.");
    }
    Thread.Sleep(interval);
}
Console.WriteLine("Failed to connect to the DB, QUIT!");
Environment.Exit(1);