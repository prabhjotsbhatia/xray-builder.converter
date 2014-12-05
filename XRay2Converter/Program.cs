using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;

namespace XRay2Converter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                if (!File.Exists(args[0]))
                {
                    Console.WriteLine("Chosen file does not exist!");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Missing/improper arguments: <file> <Shelfari URL>");
                return;
            }

            if (!Directory.Exists("out"))
            {
                if (!Directory.CreateDirectory("out").Exists)
                {
                    Console.WriteLine("Error creating 'out' directory.");
                    return;
                }
            }
            string newPath = "out/" + Path.GetFileName(args[0]);
            Console.WriteLine("Writing new X-Ray file to {0}", Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\" + newPath);
            NewXRay test;
            try
            {
                test = new NewXRay(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unhandled exception occurred while parsing X-Ray file:\nError: {0}\nStack trace: {1}\nMethod: {2}", ex.Message, ex.StackTrace, ex.TargetSite);
                return;
            }
            try
            {
                SQLiteConnection.CreateFile(newPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while creating the new X-Ray database. Is it opened in another program?\n{0}", ex.Message);
                return;
            }
            SQLiteConnection m_dbConnection;
            m_dbConnection = new SQLiteConnection(("Data Source=" + newPath + ";Version=3;"));
            m_dbConnection.Open();
            string sql;
            using (StreamReader streamReader = new StreamReader("BaseDB.sql", Encoding.UTF8))
            {
                sql = streamReader.ReadToEnd();
            }
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            Console.WriteLine("\nBuilding new X-ray database. May take a few minutes...");
            command.ExecuteNonQuery();
            Console.WriteLine("Done building initial database. Populating with info from source X-Ray...");
            test.PopulateDB(m_dbConnection, args[1]);
            Console.WriteLine("Updating indices...");
            sql = "CREATE INDEX idx_occurrence_start ON occurrence(start ASC);\n"
                + "CREATE INDEX idx_entity_type ON entity(type ASC);\n"
                + "CREATE INDEX idx_entity_excerpt ON entity_excerpt(entity ASC);";
            command = new SQLiteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery();
            m_dbConnection.Close();
            Console.WriteLine("Done writing X-Ray file! Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
