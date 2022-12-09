namespace FoxRunner
{
    public class FoxProgram
    {
        private static void Main()
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string[] arguments = Environment.GetCommandLineArgs();
            Console.WriteLine("Hello, World!");
        }
    }
}