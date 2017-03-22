using System;
using System.Linq;

namespace Workload_Estimator
{
    class WorkItem
    {
        public const string TYPEIND_PROJECT = "P";
        public const string TYPEIND_CR = "CR";
        public const string TYPEIND_TASK = "T";

        public int ID { get; set; }
        public string Type { get; set; }
        public bool Exec { get; set; }
        public int Priority { get; set; }
        public int SubPriority { get; set; }
        private int hours = 0;
        public int Hours
        {
            get { return hours; }
            set
            {
                hours = value;
                RemainingHours = value;
            }
        }
        public string Description { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime StartDate { get; set; }

        public int RemainingHours { get; set; } //the number of undone hours

        public WorkItem(string[] headers, string[] data)
        {
            for (int x = 0; x < headers.Count(); x++)
            {
                string header = headers[x];
                //find the applicable property
                var p = GetType().GetProperty(header);
                switch (p.PropertyType.ToString())
                {
                    case "System.Int32":
                        p.SetValue(this, Convert.ToInt32(data[x]));
                        continue;
                    case "System.String":
                        p.SetValue(this, data[x]);
                        continue;
                    case "System.DateTime":
                        if (data[x] == string.Empty)
                            p.SetValue(this, null);
                        else
                            p.SetValue(this, Convert.ToDateTime(data[x]));
                        continue;
                    case "System.Boolean":
                        p.SetValue(this, Convert.ToBoolean(data[x]));
                        continue;
                }
            }
        }
    }
}
