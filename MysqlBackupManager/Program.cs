using MySql.Data.MySqlClient;

namespace MysqlBackupManager
{
    public class Program
    {
        private static void Main()
        {
            string constring = "server=localhost;user=root;password=admin;database=bot_receiver_db;";
            if (!Directory.Exists(Environment.CurrentDirectory + "/backup/")) Directory.CreateDirectory(Environment.CurrentDirectory + "/backup/");
            ReadCommand:
            Console.WriteLine("Type command:");
            Console.WriteLine("1 - export backup");
            Console.WriteLine("2 - import backup");
            string type = Console.ReadLine();
            if (type == "1")
            {
                Console.WriteLine("Making backup");
                string file = Environment.CurrentDirectory + "/backup/" + DateTime.Now.ToString("HH_mm_ss") + ".sql";

                using MySqlConnection conn = new(constring);
                using MySqlCommand cmd = new();
                using MySqlBackup mb = new(cmd);
                cmd.Connection = conn;
                conn.Open();
                mb.ExportToFile(file);
                conn.Close();
                Console.WriteLine("Done");
                Console.ReadKey();
            }
            else if (type == "2")
            {
            ReadFileName:
                Console.WriteLine("Enter filepath:");
                string filePath = Console.ReadLine();
                if (File.Exists(filePath) && filePath.EndsWith(".sql"))
                {
                    Console.WriteLine("Restoring backup");
                    using MySqlConnection conn = new(constring);
                    using MySqlCommand cmd = new();
                    using MySqlBackup mb = new(cmd);
                    cmd.Connection = conn;
                    conn.Open();
                    mb.ImportFromFile(filePath);
                    conn.Close();
                    Console.WriteLine("Done");
                    Console.ReadKey();
                }
                else
                {
                    Console.WriteLine("File not found, ot not supported");
                    goto ReadFileName;
                }
            }
            else
            {
                Console.WriteLine("Unknown command");
                goto ReadCommand;
            }
        }
    }
}