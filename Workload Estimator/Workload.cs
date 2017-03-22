using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Workload_Estimator
{
    class Workload
    {
        const int HIGHEST_PRIORITY = 0;
        const int LOWEST_PRIORITY = 3;

        private List<WorkItem> workItems = new List<WorkItem>();
        public List<WorkItem> WorkItems
        {
            get { return workItems; }
            set
            {
                workItems = value.OrderBy(w => w.Priority)
                                 .ThenBy(w => w.SubPriority)
                                 .ThenBy(w => w.DueDate)
                                 .ThenBy(w => w.Exec)
                                 .ThenBy(w => w.StartDate).ToList();
            }
        }
        public List<WorkItem> WorkItemsByType(string type) => workItems.Where(w => w.Type == type).ToList();
        public List<WorkItem> WorkItemsBy(string type, int priority) => workItems.Where(w => w.Type == type && w.Priority == priority).ToList();
        public Stack<WorkItem> GetStackBy(string type, int priority) => new Stack<WorkItem>(workItems.Where(w => w.Type == type && w.Priority == priority));

        public Workload()
        {
        }

        public Workload(List<WorkItem> workItemList)
        {
            WorkItems = workItemList;
        }

        public List<Workday> PlanWorkload(DateTime startDate)
        {
            var currentDate = startDate;
            var Workdays = new List<Workday>();
            var currentWorkday = new Workday(currentDate);
            for (int pr = HIGHEST_PRIORITY; pr <= LOWEST_PRIORITY; pr++)
            {
                //get stacks by current priority
                var ProjectStack = WorkItemsBy(WorkItem.TYPEIND_PROJECT, pr);
                var TaskStack = WorkItemsBy(WorkItem.TYPEIND_TASK, pr);
                var CRStack = WorkItemsBy(WorkItem.TYPEIND_CR, pr);

                int stackCount = CRStack.Count() + TaskStack.Count() + ProjectStack.Count();

                int projectIndex = ProjectStack.Count > 0 ? 0 : -1;
                int taskIndex = TaskStack.Count > 0 ? 0 : -1;
                int CRIndex = CRStack.Count > 0 ? 0 : -1;

                var remainingProjectItems = new Func<bool>(() => projectIndex > -1 && ProjectStack[projectIndex].RemainingHours > 0);
                var remainingTaskItems = new Func<bool>(() => taskIndex > -1 && TaskStack[taskIndex].RemainingHours > 0);
                var remainingCRItems = new Func<bool>(() => CRIndex > -1 && CRStack[CRIndex].RemainingHours > 0);
                var undepletedStackCount = new Func<int>(() => {
                    int retVal = 0;
                    if (remainingProjectItems()) retVal++;
                    if (remainingCRItems()) retVal++;
                    if (remainingTaskItems()) retVal++;
                    return retVal;
                });
                var remainingHoursForUndepletedStacks = new Func<bool>(() => {
                    if (remainingTaskItems() && currentWorkday.UnplannedHours_Task > 0) return true;
                    if (remainingProjectItems() && currentWorkday.UnplannedHours_Project > 0) return true;
                    if (remainingCRItems() && currentWorkday.UnplannedHours_CR > 0) return true;
                    return false;
                });

                //while there are tasks remaining in this priority...
                while (undepletedStackCount() > 0)
                {
                    //try to add a project item
                    if (remainingProjectItems())
                    {
                        var thisItem = ProjectStack[projectIndex];
                        int maxHours = remainingHoursForUndepletedStacks() ? currentWorkday.UnplannedHours_Project : currentWorkday.UnplannedHours;
                        int hoursToUse = Math.Min(maxHours, thisItem.RemainingHours);
                        thisItem.RemainingHours -= hoursToUse;
                        currentWorkday.AddWorkdayItem(new WorkdayItem {
                            Hours = hoursToUse,
                            Type = thisItem.Type,
                            ID = thisItem.ID,
                            Description = thisItem.Description
                        });
                        if ((ProjectStack.Count() > (projectIndex + 1)) && thisItem.RemainingHours == 0) projectIndex++;
                    }

                    //try to add a scope item
                    if (remainingCRItems())
                    {
                        var thisItem = CRStack[CRIndex];
                        int maxHours = remainingHoursForUndepletedStacks() ? currentWorkday.UnplannedHours_CR : currentWorkday.UnplannedHours;
                        int hoursToUse = Math.Min(maxHours, thisItem.RemainingHours);
                        if (hoursToUse > 0)
                        {
                            thisItem.RemainingHours -= hoursToUse;
                            currentWorkday.AddWorkdayItem(new WorkdayItem
                            {
                                Hours = hoursToUse,
                                Type = thisItem.Type,
                                ID = thisItem.ID,
                                Description = thisItem.Description
                            });
                            if ((CRStack.Count() > (CRIndex + 1)) && thisItem.RemainingHours == 0) CRIndex++;
                        }
                    }

                    //try to add a task item
                    if (remainingTaskItems())
                    {
                        var thisItem = TaskStack[taskIndex];
                        int maxHours = remainingHoursForUndepletedStacks() ? currentWorkday.UnplannedHours_Task : currentWorkday.UnplannedHours;
                        int hoursToUse = Math.Min(maxHours, thisItem.RemainingHours);
                        if (hoursToUse > 0)
                        {
                            thisItem.RemainingHours -= hoursToUse;
                            currentWorkday.AddWorkdayItem(new WorkdayItem
                            {
                                Hours = hoursToUse,
                                Type = thisItem.Type,
                                ID = thisItem.ID,
                                Description = thisItem.Description
                            });
                            if ((TaskStack.Count() > (taskIndex + 1)) && thisItem.RemainingHours == 0) taskIndex++;
                        }
                    }

                    //Save the workday if it's all done
                    if (currentWorkday.UnplannedHours == 0)
                    {
                        Workdays.Add(currentWorkday);
                        currentDate = currentDate.AddDays(1);
                        if (currentDate.DayOfWeek == DayOfWeek.Saturday)
                            currentDate = currentDate.AddDays(2);
                        currentWorkday = new Workday(currentDate);
                    }
                }
            }

            //if there were tasks assigned to an unfinished day, add it to the set
            if (currentWorkday.GetWorkdayItems().Count() > 0)
            {
                Workdays.Add(currentWorkday);
            }

            return Workdays;
        }
    }

    class Workday
    {
        public const int HOURS_PER_DAY = 6;

        const int DAILY_PROJECT_HOURS = 3;
        const int DAILY_TASK_HOURS = 2;
        const int DAILY_SCOPE_HOURS = 1;

        public int UnplannedHours => HOURS_PER_DAY - workdayItems.Sum(wi => wi.Hours);
        public int PlannedHoursByType(string type) => workdayItems.Where(wi => wi.Type == type).Sum(wi => wi.Hours);
        public int UnplannedHours_Project => DAILY_PROJECT_HOURS - PlannedHoursByType(WorkItem.TYPEIND_PROJECT);
        public int UnplannedHours_Task => DAILY_TASK_HOURS - PlannedHoursByType(WorkItem.TYPEIND_TASK);
        public int UnplannedHours_CR => DAILY_SCOPE_HOURS - PlannedHoursByType(WorkItem.TYPEIND_CR);

        public DateTime Day { get; set; }
        private List<WorkdayItem> workdayItems = new List<WorkdayItem>();
        public List<WorkdayItem> GetWorkdayItems() => workdayItems;
        public void AddWorkdayItem(WorkdayItem item)
        {
            var matchingItem = workdayItems.FirstOrDefault(i => i.ID == item.ID && i.Type == item.Type);
            if (matchingItem == null)
            {
                workdayItems.Add(item);
            }
            else
            {
                matchingItem.Hours += item.Hours;
            }
        }
        public Workday(DateTime day)
        {
            Day = day;
        }

        public override string ToString() => $"{Day.ToShortDateString()}: {string.Join(";", GetWorkdayItems().Select(wi => wi.ToString()))}";
    }
    
    class WorkdayItem
    {
        public int Hours { get; set; }
        public string Type { get; set; }
        public int ID { get; set; }
        public string Description { get; set; }

        public override string ToString() => $"{Type}-{ID}, {Hours}hr(s).";
    }
}
