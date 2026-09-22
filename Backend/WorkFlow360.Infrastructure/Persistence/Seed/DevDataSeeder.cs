using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkFlow360.Application.Attendance;
using WorkFlow360.Application.Common;
using WorkFlow360.Domain.Entities;
using WorkFlow360.Domain.Enums;

namespace WorkFlow360.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo data for local development. Each section only runs when its table is empty,
/// so it is safe to run on every startup and new sections can be added later.
/// </summary>
public static class DevDataSeeder
{
    public const string DemoPassword = "Passw0rd!";
    private const string EmailDomain = "workflow360.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DevDataSeeder));

        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            await SeedOrganisationAsync(db, hasher);
            logger.LogInformation("Seeded demo organisation. Demo accounts are listed in the README.");
        }

        if (!await db.Holidays.AnyAsync())
        {
            SeedHolidays(db, DateTime.UtcNow.Year);
            await db.SaveChangesAsync();
        }

        if (!await db.Projects.AnyAsync() && await db.Employees.AnyAsync())
        {
            await SeedProjectsAsync(db);
            logger.LogInformation("Seeded demo projects and tasks.");
        }

        if (!await db.LeaveRequests.AnyAsync() && await db.Employees.AnyAsync())
        {
            var calendar = scope.ServiceProvider.GetRequiredService<BusinessCalendar>();
            var today = scope.ServiceProvider.GetRequiredService<CompanyTime>().Today;
            await SeedLeaveRequestsAsync(db, calendar, today);
            logger.LogInformation("Seeded demo leave requests.");
        }

        if (!await db.AttendanceRecords.AnyAsync() && await db.Employees.AnyAsync())
        {
            var companyTime = scope.ServiceProvider.GetRequiredService<CompanyTime>();
            var attendance = scope.ServiceProvider.GetRequiredService<IOptions<AttendanceOptions>>().Value;
            await SeedAttendanceAsync(db, companyTime, attendance);
            logger.LogInformation("Seeded demo attendance.");
        }
    }

    /// <summary>
    /// About five weeks of history. A fixed random seed keeps the data the same on every machine:
    /// most people arrive around 9:00, some are late, a few days are missed. Today is left empty to try check-in.
    /// </summary>
    private static async Task SeedAttendanceAsync(AppDbContext db, CompanyTime companyTime, AttendanceOptions options)
    {
        var random = new Random(360);
        var today = companyTime.Today;
        var from = today.AddDays(-35);
        var lateAfter = options.OfficeStartTime.AddMinutes(options.GraceMinutes);

        var employees = await db.Employees
            .Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
            .Select(e => new { e.Id, e.JoiningDate })
            .ToListAsync();
        var holidays = (await db.Holidays.Where(h => h.Date >= from && h.Date < today).Select(h => h.Date).ToListAsync()).ToHashSet();
        var leaves = await db.LeaveRequests
            .Where(r => r.Status == LeaveStatus.Approved && r.EndDate >= from && r.StartDate < today)
            .Select(r => new { r.EmployeeId, r.StartDate, r.EndDate })
            .ToListAsync();

        foreach (var employee in employees)
        {
            for (var date = from; date < today; date = date.AddDays(1))
            {
                var onLeave = leaves.Any(l => l.EmployeeId == employee.Id && l.StartDate <= date && l.EndDate >= date);
                if (date < employee.JoiningDate || companyTime.WeekendDays.Contains(date.DayOfWeek) || holidays.Contains(date) || onLeave)
                    continue;

                if (random.NextDouble() < 0.05)
                    continue; // absent

                var checkIn = new TimeOnly(8, 35).AddMinutes(random.Next(0, 65));
                var checkOut = new TimeOnly(17, 0).AddMinutes(random.Next(0, 100));
                var checkInUtc = companyTime.ToUtc(date, checkIn);
                var checkOutUtc = companyTime.ToUtc(date, checkOut);

                db.AttendanceRecords.Add(new AttendanceRecord
                {
                    EmployeeId = employee.Id,
                    WorkDate = date,
                    CheckInAt = checkInUtc,
                    CheckOutAt = checkOutUtc,
                    IsLate = checkIn > lateAfter,
                    WorkedMinutes = (int)(checkOutUtc - checkInUtc).TotalMinutes
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedLeaveRequestsAsync(AppDbContext db, BusinessCalendar calendar, DateOnly today)
    {
        var employees = await db.Employees.ToDictionaryAsync(e => e.EmployeeCode);
        var leaveTypes = await db.LeaveTypes.ToDictionaryAsync(t => t.Code);
        var balances = await db.LeaveBalances.Where(b => b.Year == today.Year).ToListAsync();
        var now = DateTime.UtcNow;

        async Task AddAsync(string employeeCode, string leaveTypeCode, int startOffset, int endOffset, string reason,
            LeaveStatus status, string? reviewerCode = null, string? reviewComment = null)
        {
            var start = today.AddDays(startOffset);
            var end = today.AddDays(endOffset);

            // Keep the demo inside the current leave year so it matches the seeded balances.
            if (start.Year != today.Year || end.Year != today.Year)
                return;

            var days = await calendar.CountWorkingDaysAsync(start, end, CancellationToken.None);
            if (days == 0)
                return;

            var employee = employees[employeeCode];
            var leaveType = leaveTypes[leaveTypeCode];
            var reviewer = reviewerCode == null ? null : employees[reviewerCode];

            db.LeaveRequests.Add(new LeaveRequest
            {
                EmployeeId = employee.Id,
                LeaveTypeId = leaveType.Id,
                StartDate = start,
                EndDate = end,
                TotalDays = days,
                Reason = reason,
                Status = status,
                ReviewedByUserId = reviewer?.UserId,
                ReviewedAt = reviewer == null ? null : now.AddDays(Math.Min(startOffset, 0) - 3),
                ReviewComment = reviewComment,
                CreatedAt = now.AddDays(Math.Min(startOffset, 0) - 7)
            });

            if (status == LeaveStatus.Approved)
                balances.First(b => b.EmployeeId == employee.Id && b.LeaveTypeId == leaveType.Id).UsedDays += days;
        }

        await AddAsync("E1004", "AL", -40, -36, "Family trip to Cox's Bazar.", LeaveStatus.Approved, "E1002");
        await AddAsync("E1005", "SL", -20, -19, "Fever, doctor advised rest.", LeaveStatus.Approved, "E1002");
        await AddAsync("E1010", "AL", 20, 24, "Sister's wedding.", LeaveStatus.Approved, "E1003", "Enjoy the wedding!");
        await AddAsync("E1006", "CL", 5, 5, "Personal errand.", LeaveStatus.Rejected, "E1002",
            "It's release week for MAA. Could you take the following Monday instead?");
        await AddAsync("E1004", "AL", 10, 12, "Moving to a new apartment.", LeaveStatus.Pending);
        await AddAsync("E1008", "CL", 3, 3, "Bank appointment in the morning.", LeaveStatus.Pending);
        await AddAsync("E1013", "AL", 14, 18, "Visiting parents in Sylhet.", LeaveStatus.Pending);

        await db.SaveChangesAsync();
    }

    private static async Task SeedProjectsAsync(AppDbContext db)
    {
        var employees = await db.Employees.ToDictionaryAsync(e => e.EmployeeCode);
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        Project AddProject(string code, string name, string description, string managerCode, ProjectStatus status,
            int startOffset, int? endOffset, params (string Code, string Role)[] members)
        {
            var manager = employees[managerCode];
            var project = new Project
            {
                Code = code,
                Name = name,
                Description = description,
                ManagerId = manager.Id,
                Status = status,
                StartDate = today.AddDays(startOffset),
                EndDate = endOffset.HasValue ? today.AddDays(endOffset.Value) : null,
                CreatedByUserId = manager.UserId!.Value
            };

            foreach (var (memberCode, role) in members)
                project.Members.Add(new ProjectMember { EmployeeId = employees[memberCode].Id, RoleInProject = role, AddedAt = now });

            db.Projects.Add(project);
            return project;
        }

        ProjectTask AddTask(Project project, string title, string? assigneeCode, TaskPriority priority,
            ProjectTaskStatus status, int? dueOffset)
        {
            var managerUserId = project.CreatedByUserId;
            var assignee = assigneeCode == null ? null : employees[assigneeCode];

            var task = new ProjectTask
            {
                Project = project,
                Title = title,
                AssigneeId = assignee?.Id,
                Priority = priority,
                Status = status,
                DueDate = dueOffset.HasValue ? today.AddDays(dueOffset.Value) : null,
                CompletedAt = status == ProjectTaskStatus.Done ? now.AddDays(-3) : null,
                CreatedByUserId = managerUserId,
                CreatedAt = now.AddDays(-14)
            };

            task.History.Add(new TaskHistoryEntry { ChangedByUserId = managerUserId, FieldName = "Created", NewValue = title, ChangedAt = now.AddDays(-14) });
            if (status != ProjectTaskStatus.Todo)
            {
                task.History.Add(new TaskHistoryEntry
                {
                    ChangedByUserId = assignee?.UserId ?? managerUserId,
                    FieldName = "Status",
                    OldValue = nameof(ProjectTaskStatus.Todo),
                    NewValue = status.ToString(),
                    ChangedAt = now.AddDays(-4)
                });
            }

            db.ProjectTasks.Add(task);
            return task;
        }

        var hrPortal = AddProject("HRP", "HR Portal Revamp",
            "Replace the spreadsheet-based leave and profile process with self-service screens.",
            "E1002", ProjectStatus.Active, -60, 45,
            ("E1004", "Tech lead"), ("E1005", "Developer"), ("E1008", "QA"), ("E1010", "Designer"));

        var mobile = AddProject("MAA", "Mobile Attendance App",
            "Mobile check-in and check-out for field staff, including offline support.",
            "E1002", ProjectStatus.Active, -30, 90,
            ("E1006", "Mobile developer"), ("E1009", "DevOps"), ("E1007", "Developer"));

        var onboarding = AddProject("COW", "Client Onboarding Workflow",
            "Standardise how new clients are onboarded, from contract signature to first delivery.",
            "E1003", ProjectStatus.Active, -45, 30,
            ("E1010", "Designer"), ("E1011", "Business analyst"), ("E1005", "Developer"));

        var infrastructure = AddProject("INF", "Server Infrastructure Upgrade",
            "Move production workloads to the new database and backup servers.",
            "E1002", ProjectStatus.Completed, -150, -40,
            ("E1009", "DevOps"));

        AddProject("BIR", "Management Reporting",
            "Monthly headcount, utilisation and leave reports for the leadership team.",
            "E1003", ProjectStatus.Planned, 14, 120,
            ("E1011", "Business analyst"));

        AddTask(hrPortal, "Design new leave request form", "E1010", TaskPriority.High, ProjectTaskStatus.Done, -20);
        AddTask(hrPortal, "Build leave request API", "E1004", TaskPriority.High, ProjectTaskStatus.Done, -10);
        var approvalScreen = AddTask(hrPortal, "Leave approval screen for managers", "E1005", TaskPriority.High, ProjectTaskStatus.InProgress, 5);
        AddTask(hrPortal, "Email reminders for pending approvals", "E1004", TaskPriority.Medium, ProjectTaskStatus.Todo, 12);
        AddTask(hrPortal, "Regression test the leave workflow", "E1008", TaskPriority.Medium, ProjectTaskStatus.Todo, 18);
        var datePickerBug = AddTask(hrPortal, "Fix date picker showing the previous day", "E1005", TaskPriority.Critical, ProjectTaskStatus.InReview, -2);
        AddTask(hrPortal, "Update employee profile page layout", "E1010", TaskPriority.Low, ProjectTaskStatus.InProgress, -5);

        AddTask(mobile, "Set up mobile project and CI build", "E1009", TaskPriority.High, ProjectTaskStatus.Done, -15);
        AddTask(mobile, "GPS-based check-in prototype", "E1006", TaskPriority.High, ProjectTaskStatus.InProgress, 7);
        AddTask(mobile, "Offline sync for attendance records", "E1006", TaskPriority.Medium, ProjectTaskStatus.Todo, 25);
        AddTask(mobile, "Login screen and secure token storage", "E1007", TaskPriority.Medium, ProjectTaskStatus.InProgress, -1);
        AddTask(mobile, "Push notification setup", null, TaskPriority.Low, ProjectTaskStatus.Todo, 40);

        AddTask(onboarding, "Map the current onboarding steps", "E1011", TaskPriority.Medium, ProjectTaskStatus.Done, -25);
        AddTask(onboarding, "Onboarding checklist wireframes", "E1010", TaskPriority.High, ProjectTaskStatus.InReview, 3);
        AddTask(onboarding, "Document approval rules", "E1011", TaskPriority.Medium, ProjectTaskStatus.InProgress, 9);
        AddTask(onboarding, "Client welcome email templates", "E1005", TaskPriority.Low, ProjectTaskStatus.Todo, 20);

        AddTask(infrastructure, "Migrate the database server", "E1009", TaskPriority.Critical, ProjectTaskStatus.Done, -60);
        AddTask(infrastructure, "Configure nightly backups", "E1009", TaskPriority.High, ProjectTaskStatus.Done, -50);
        AddTask(infrastructure, "Retire the old file server", "E1009", TaskPriority.Medium, ProjectTaskStatus.Cancelled, -45);

        var tanvirUserId = employees["E1002"].UserId!.Value;
        var ayeshaUserId = employees["E1005"].UserId!.Value;

        datePickerBug.Comments.Add(new TaskComment
        {
            AuthorUserId = tanvirUserId,
            Body = "Users in Dhaka see the previous day when they pick a date late in the evening.",
            CreatedAt = now.AddDays(-3)
        });
        datePickerBug.Comments.Add(new TaskComment
        {
            AuthorUserId = ayeshaUserId,
            Body = "Found it: the date was converted to UTC before being formatted. The fix is ready for review.",
            CreatedAt = now.AddDays(-1)
        });
        approvalScreen.Comments.Add(new TaskComment
        {
            AuthorUserId = tanvirUserId,
            Body = "Please show the remaining balance next to each request so managers don't have to look it up.",
            CreatedAt = now.AddDays(-2)
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedOrganisationAsync(AppDbContext db, IPasswordHasher hasher)
    {
        var roleIds = await db.Roles.ToDictionaryAsync(r => r.Name, r => r.Id);
        var passwordHash = hasher.Hash(DemoPassword);

        var departments = new[]
        {
            new Department { Code = "ENG", Name = "Engineering" },
            new Department { Code = "PRD", Name = "Product" },
            new Department { Code = "HR", Name = "Human Resources" },
            new Department { Code = "FIN", Name = "Finance" }
        }.ToDictionary(d => d.Code);

        var designations = new[]
        {
            "HR Manager", "HR Executive", "Engineering Manager", "Product Manager",
            "Senior Software Engineer", "Software Engineer", "Junior Software Engineer",
            "QA Engineer", "DevOps Engineer", "UI/UX Designer", "Business Analyst", "Accountant"
        }.ToDictionary(t => t, t => new Designation { Title = t });

        db.Departments.AddRange(departments.Values);
        db.Designations.AddRange(designations.Values);

        db.Users.Add(new User
        {
            Email = $"admin@{EmailDomain}",
            FullName = "System Administrator",
            PasswordHash = passwordHash,
            RoleId = roleIds[RoleNames.Admin]
        });

        Employee AddEmployee(string code, string firstName, string lastName, string role, string department,
            string designation, Employee? manager, DateOnly joiningDate, EmploymentStatus status = EmploymentStatus.Active)
        {
            var email = $"{firstName}.{lastName}@{EmailDomain}".ToLowerInvariant();

            var employee = new Employee
            {
                EmployeeCode = code,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = $"+880 17{int.Parse(code[1..]):D8}",
                Department = departments[department],
                Designation = designations[designation],
                Manager = manager,
                JoiningDate = joiningDate,
                Status = status,
                User = new User
                {
                    Email = email,
                    FullName = $"{firstName} {lastName}",
                    PasswordHash = passwordHash,
                    RoleId = roleIds[role]
                }
            };

            db.Employees.Add(employee);
            return employee;
        }

        var hrManager = AddEmployee("E1001", "Farhana", "Rahman", RoleNames.HR, "HR", "HR Manager", null, new DateOnly(2019, 2, 10));
        var engManager = AddEmployee("E1002", "Tanvir", "Ahmed", RoleNames.Manager, "ENG", "Engineering Manager", null, new DateOnly(2018, 7, 1));
        var productManager = AddEmployee("E1003", "Sarah", "Collins", RoleNames.Manager, "PRD", "Product Manager", null, new DateOnly(2020, 1, 15));

        AddEmployee("E1004", "Rafiq", "Hasan", RoleNames.Employee, "ENG", "Senior Software Engineer", engManager, new DateOnly(2019, 9, 1));
        AddEmployee("E1005", "Ayesha", "Siddiqua", RoleNames.Employee, "ENG", "Software Engineer", engManager, new DateOnly(2021, 4, 12));
        AddEmployee("E1006", "Daniel", "Brooks", RoleNames.Employee, "ENG", "Software Engineer", engManager, new DateOnly(2022, 3, 1));
        AddEmployee("E1007", "Mehedi", "Hasan", RoleNames.Employee, "ENG", "Junior Software Engineer", engManager,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-2)), EmploymentStatus.Probation);
        AddEmployee("E1008", "Priya", "Sharma", RoleNames.Employee, "ENG", "QA Engineer", engManager, new DateOnly(2021, 11, 8));
        AddEmployee("E1009", "Kamrul", "Islam", RoleNames.Employee, "ENG", "DevOps Engineer", engManager, new DateOnly(2020, 6, 22));
        AddEmployee("E1010", "Emily", "Carter", RoleNames.Employee, "PRD", "UI/UX Designer", productManager, new DateOnly(2022, 8, 15));
        AddEmployee("E1011", "Nusrat", "Jahan", RoleNames.Employee, "PRD", "Business Analyst", productManager, new DateOnly(2023, 1, 9));
        AddEmployee("E1012", "Sumaiya", "Akter", RoleNames.HR, "HR", "HR Executive", hrManager, new DateOnly(2023, 5, 2));
        // No manager on purpose: shows HR approving leave for employees outside a reporting line.
        AddEmployee("E1013", "Arif", "Chowdhury", RoleNames.Employee, "FIN", "Accountant", null, new DateOnly(2021, 2, 1));

        var year = DateTime.UtcNow.Year;
        var leaveTypes = await db.LeaveTypes.Where(t => t.IsActive).ToListAsync();

        foreach (var employee in db.Employees.Local.ToList())
        {
            foreach (var leaveType in leaveTypes)
            {
                db.LeaveBalances.Add(new LeaveBalance
                {
                    Employee = employee,
                    LeaveTypeId = leaveType.Id,
                    Year = year,
                    AllocatedDays = leaveType.DefaultDaysPerYear
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static void SeedHolidays(AppDbContext db, int year)
    {
        db.Holidays.AddRange(
            new Holiday { Date = new DateOnly(year, 2, 21), Name = "International Mother Language Day" },
            new Holiday { Date = new DateOnly(year, 3, 26), Name = "Independence Day" },
            new Holiday { Date = new DateOnly(year, 5, 1), Name = "May Day" },
            new Holiday { Date = new DateOnly(year, 12, 16), Name = "Victory Day" },
            new Holiday { Date = new DateOnly(year, 12, 25), Name = "Christmas Day" });
    }
}
