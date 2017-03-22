using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.IO;
using System.Diagnostics;

namespace Workload_Estimator
{
    class Program
    {
        const string IMPORT_FILE = "import\\import.txt";
        const string OUTPUT_FILE = "output.txt";
        const int HOURS_PER_DAY = 6;
        const int DAILY_PROJECT_HOURS = 3;
        const int DAILY_TASK_HOURS = 2;
        const int DAILY_SCOPE_HOURS = 1;

        static List<WorkItem> workItems = new List<WorkItem>();

        /// <summary>
        /// Project scheduling notes:
        /// --Given equal priority category, project work takes 3 hours, tasks 2 hours, scopes 1 hr.
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var startDate = GetStartDate();

            Console.WriteLine("Reading databases...");
            ReadWorkItemsFromDB("CMD", "CRWorkload");
            ReadWorkItemsFromDB("CMD", "PWorkload");
            ReadWorkItemsFromDB("TMD", "TaskWorkload");

            Console.WriteLine("Planning workload...");
            Workload wl = new Workload(workItems);
            var results = wl.PlanWorkload(startDate);

            //write output
            Console.WriteLine("Writing output...");
            File.WriteAllLines(OUTPUT_FILE, results.Select(r => r.ToString()).ToArray());
            Process.Start(OUTPUT_FILE);
            
            Console.WriteLine("Done. Press any key to exit.");

            Console.ReadKey();
            
        }

        private static DateTime GetStartDate()
        {
            Console.WriteLine("What is the desired starting date? Press enter for tomorrow.");
            DateTime startDate = DateTime.MinValue;
            while (startDate == DateTime.MinValue)
            {
                string response = Console.ReadLine();
                if (response == string.Empty)
                {
                    startDate = DateTime.Now.AddDays(1).Date;
                    if (startDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        startDate = startDate.AddDays(2);
                    }
                    if (startDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        startDate = startDate.AddDays(1);
                    }
                }
                else
                {
                    if (DateTime.TryParse(response, out startDate))
                    {
                        if (startDate.DayOfWeek == DayOfWeek.Saturday)
                        {
                            startDate = startDate.AddDays(2);
                        }
                        if (startDate.DayOfWeek == DayOfWeek.Sunday)
                        {
                            startDate = startDate.AddDays(1);
                        }
                    }
                    else
                    {
                        Console.WriteLine("That didn't look right. Give it another try.");
                    }
                }
            }
            return startDate;
        }

        private static void ReadWorkItemsFromFile(string filename)
        {
            //read the work items from the file
            string[] file = File.ReadAllLines(filename);
            string[] headers = file[0].Replace(" ", string.Empty).Split('\t'); //remove all spaces from headers            
            for (int x = 1; x < file.Count(); x++)
            {
                workItems.Add(new WorkItem(headers, file[x].Split('\t')));
            }
        }

        private static void ReadWorkItemsFromDB(string dsn, string queryName)
        {
            //set up connection
            OdbcConnection conn = new OdbcConnection($"DSN={dsn}");
            conn.Open();
            OdbcCommand command = conn.CreateCommand();
            command.CommandText = $"SELECT * FROM {queryName}";
            OdbcDataReader reader = command.ExecuteReader();

            //get field names
            int fieldCount = reader.FieldCount;
            var headerList = new List<string>();
            for (int i = 0; i < fieldCount; i++)
            {
                headerList.Add(reader.GetName(i).Replace(" ", string.Empty));
            }
            var headers = headerList.ToArray();

            //read tasks
            while (reader.Read())
            {
                string[] vals = new string[fieldCount];
                for (int i = 0; i < fieldCount; i++)
                {
                    vals[i] = reader.IsDBNull(i) ? null : reader.GetValue(i).ToString();
                }
                workItems.Add(new WorkItem(headers, vals));
            }

            //clean up
            reader.Close();
            command.Dispose();
            conn.Close();
        }
    }
}
