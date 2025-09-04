using System;
using System.Runtime.InteropServices;
using System.Text;

namespace TestUtils
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var repo = CreateRepository();
            Console.WriteLine($"Repo1 is connected: {repo.IsConnected()}");
            Console.WriteLine($"Repo1 IsInitialized: {repo.DatabaseIsInitialized()}");
            var repo2 = CreateRepository();
            Console.WriteLine($"Repo2 is connected: {repo2.IsConnected()}");
            Console.WriteLine($"Repo2 IsInitialized: {repo2.DatabaseIsInitialized()}");
            Console.WriteLine($"Type 'exit' to quit.");
            while (true)
            {
                Console.Write("Repo1>");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;
                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
                try
                {
                    var output = repo.ExecuteCommand(input);
                    Console.WriteLine($"{output}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
        }
        public static RepositoriesCore.EmployeesRepository CreateRepository()
        {
            var debugConfigFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "test.ini");
            var iniHandler = new IniFileHandler();
            var repo = new RepositoriesCore.EmployeesRepository(RepositoriesCore.IRepository.GenerateConnectionString(
                server: iniHandler.ReadValue("default", "Server", debugConfigFile),
                database: iniHandler.ReadValue("default", "Database", debugConfigFile),
                userId: iniHandler.ReadValue("default", "User_Id", debugConfigFile),
                password: iniHandler.ReadValue("default", "Password", debugConfigFile)
                ));
            return repo;
        }
    }
    class IniFileHandler
    {
        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public void WriteValue(string section, string key, string value, string filePath)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        public string ReadValue(string section, string key, string filePath)
        {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, "", temp, 255, filePath);
            return temp.ToString();
        }
    }

}