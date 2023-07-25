#r "nuget: MySql.Data, 8.0.17"

using System.Threading;
using System.Diagnostics;
using MySql.Data.MySqlClient;


string connectionString = Args[Args.Count - 1];

int retries = 120;
int interval = 500;
MySqlConnection connection = null;
for (int i = 0; i < retries; i++)
{
    try
    {
        connection = new MySqlConnection(connectionString);
        connection.Open();
        Console.WriteLine("Database connected.");
        connection.Close();

        var arguments = "";
        for (int j = 1; j < Args.Count; j++)
        {
            arguments += Args[j] + " ";
        }

        Process process = new Process();
        // Configure the process using the StartInfo properties.
        process.StartInfo.FileName = Args[0];
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.Start();
        process.WaitForExit();// Waits here for the process to exit.

        Environment.Exit(0);
    }
    catch (MySqlException)
    {
        Console.WriteLine("Cannot establish connection to the DB.");
    }
    Thread.Sleep(interval);
}
Console.WriteLine("Failed to connect to the DB, QUIT!");
Environment.Exit(1);