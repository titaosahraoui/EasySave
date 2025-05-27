using BackupApp.Services;
using BackupApp.Models;
using BackupApp.Data;
using BackupApp.Logging;
using BackupApp;

class Program
{
    private static BackupRepository _repository;
    private static BackupService _backupService;
    private static LanguageService _languageService;
    private static AppConfig _config;

    static void Main(string[] args)
    {
        InitializeServices();

        if (args.Length > 0)
        {
            ProcessCommandLine(args[0]);
            return;
        }

        ShowMainMenu();
    }

    private static void InitializeServices()
    {
        _config = AppConfig.Load();
        _repository = new BackupRepository();
        _backupService = new BackupService(_config.DefaultLogFormat);
        _languageService = new LanguageService();
    }

    private static void ProcessCommandLine(string command)
    {
        try
        {
            if (command.Contains("-")) // Range (1-3)
            {
                var range = command.Split('-');
                if (range.Length == 2 && int.TryParse(range[0], out int start) && int.TryParse(range[1], out int end))
                {
                    for (int i = start; i <= end; i++)
                    {
                        ExecuteBackup(i);
                    }
                }
            }
            else if (command.Contains(",")) // List (2,3)
            {
                var jobs = command.Split(',');
                foreach (var job in jobs)
                {
                    if (int.TryParse(job, out int jobId))
                    {
                        ExecuteBackup(jobId);
                    }
                }
            }
            else // Single job
            {
                if (int.TryParse(command, out int jobId))
                {
                    ExecuteBackup(jobId);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(_languageService.GetString("CommandError") + ex.Message);
        }
    }

    private static void ExecuteBackup(int jobId)
    {
        var job = _repository.GetBackupJob(jobId);
        if (job != null)
        {
            Console.WriteLine($"\n{_languageService.GetString("StartingBackup")} {job.Name} (ID: {jobId})");
            _backupService = new BackupService(_config.DefaultLogFormat);
            _backupService.PerformBackup(job);
        }
        else
        {
            Console.WriteLine(_languageService.GetString("JobNotFound") + jobId);
        }
    }

    public static void DisplayLogo()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;

        string[] logo = {
            @" /$$$$$$$$                                /$$$$$$$",
            @"| $$_____/                               /$$__  $$",
            @"| $$        /$$$$$$   /$$$$$$$ /$$   /$$| $$  \__/  /$$$$$$  /$$    /$$ /$$$$$$",
            @"| $$$$$    |____  $$ /$$_____/| $$  | $$|  $$$$$$  |____  $$|  $$  /$$//$$__  $$",
            @"| $$__/     /$$$$$$$|  $$$$$$ | $$  | $$ \____  $$  /$$$$$$$ \  $$/$$/| $$$$$$$$",
            @"| $$       /$$__  $$ \____  $$| $$  | $$ /$$  \ $$ /$$__  $$  \  $$$/ | $$_____/",
            @"| $$$$$$$$|  $$$$$$$ /$$$$$$$/|  $$$$$$$|  $$$$$$/|  $$$$$$$   \  $/  |  $$$$$$$",
            @"|________/ \_______/|_______/  \____  $$ \______/  \_______/    \_/    \_______/",
            @"                               /$$  | $$",
            @"                              |  $$$$$$/",
            @"                               \______/"
        };

        Console.Clear();
        foreach (string line in logo)
        {
            Console.WriteLine(line);
        }

        Console.ResetColor();
        Console.WriteLine("\n           Backup Manager v1.0");
        Console.WriteLine("   ──────────────────────────────");
        Thread.Sleep(1000);
    }

    private static void ShowMainMenu()
    {
        bool exit = false;

        while (!exit)
        {
<<<<<<< HEAD

=======
>>>>>>> 8ce2fea5a421066591ddeb380423a10f9604cb18
            Console.Clear();
            DisplayLogo();
            Console.WriteLine("====================================");
            Console.WriteLine(_languageService.GetString("MainMenuTitle"));
            Console.WriteLine("====================================");
            Console.WriteLine($"1. {_languageService.GetString("CreateBackupJob")}");
            Console.WriteLine($"2. {_languageService.GetString("EditBackupJob")}");
            Console.WriteLine($"3. {_languageService.GetString("DeleteBackupJob")}");
            Console.WriteLine($"4. {_languageService.GetString("ListBackupJobs")}");
            Console.WriteLine($"5. {_languageService.GetString("ExecuteBackupJob")}");
            Console.WriteLine($"6. {_languageService.GetString("ExecuteAllBackupJobs")}");
            Console.WriteLine($"7. {_languageService.GetString("ChangeLanguage")}");
            Console.WriteLine($"8. {_languageService.GetString("ChangeLogFormat")}");
            Console.WriteLine($"9. {_languageService.GetString("Exit")}");
            Console.WriteLine("====================================");
            Console.Write(_languageService.GetString("ChooseOption") + ": ");

            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    CreateBackupJob();
                    break;
                case "2":
                    EditBackupJob();
                    break;
                case "3":
                    DeleteBackupJob();
                    break;
                case "4":
                    ListBackupJobs();
                    break;
                case "5":
                    ExecuteSingleBackup();
                    break;
                case "6":
                    ExecuteAllBackups();
                    break;
                case "7":
                    ChangeLanguage();
                    break;
                case "8":
                    ChangeLogFormat();
                    break;
                case "9":
                    exit = true;
                    break;
                default:
                    Console.WriteLine(_languageService.GetString("InvalidOption"));
                    Console.ReadKey();
                    break;
            }
        }
    }

    private static void ChangeLogFormat()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("CurrentLogFormat") + ": " +
                        (_config.DefaultLogFormat == LogFormat.Json ? "JSON" : "XML"));
        Console.WriteLine($"1. JSON {(_config.DefaultLogFormat == LogFormat.Json ? "(" + _languageService.GetString("Current") + ")" : "")}");
        Console.WriteLine($"2. XML {(_config.DefaultLogFormat == LogFormat.Xml ? "(" + _languageService.GetString("Current") + ")" : "")}");
        Console.Write(_languageService.GetString("ChooseOption") + ": ");

        var input = Console.ReadLine();
        if (input == "1")
        {
            _config.DefaultLogFormat = LogFormat.Json;
            Console.WriteLine(_languageService.GetString("LogFormatChanged") + ": JSON");
        }
        else if (input == "2")
        {
            _config.DefaultLogFormat = LogFormat.Xml;
            Console.WriteLine(_languageService.GetString("LogFormatChanged") + ": XML");
        }
        else
        {
            Console.WriteLine(_languageService.GetString("InvalidOption"));
        }

        _config.Save();
        Console.ReadKey();
    }

    private static void CreateBackupJob()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("CreateBackupJobTitle"));

        if (_repository.GetAllBackupJobs().Count >= 5)
        {
            Console.WriteLine(_languageService.GetString("MaxJobsReached"));
            Console.ReadKey();
            return;
        }

        Console.Write(_languageService.GetString("EnterJobName") + ": ");
        string name = Console.ReadLine();

        Console.Write(_languageService.GetString("EnterSourcePath") + ": ");
        string source = Console.ReadLine();

        Console.Write(_languageService.GetString("EnterTargetPath") + ": ");
        string target = Console.ReadLine();

        Console.WriteLine($"1. {_languageService.GetString("FullBackup")}");
        Console.WriteLine($"2. {_languageService.GetString("DifferentialBackup")}");
        Console.Write(_languageService.GetString("ChooseBackupType") + ": ");
        string typeInput = Console.ReadLine();

        BackupType type = typeInput == "1" ? BackupType.Full : BackupType.Differential;

        var job = new BackupJob
        {
            Name = name,
            SourcePath = source,
            TargetPath = target,
            Type = type
        };

        _repository.AddBackupJob(job);
        Console.WriteLine(_languageService.GetString("JobCreatedSuccess"));
        Console.ReadKey();
    }

    private static void EditBackupJob()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("EditBackupJobTitle"));
        ListBackupJobs(false);

        Console.Write(_languageService.GetString("EnterJobIdToEdit") + ": ");
        if (!int.TryParse(Console.ReadLine(), out int id) || id < 1 || id > 5)
        {
            Console.WriteLine(_languageService.GetString("InvalidJobId"));
            Console.ReadKey();
            return;
        }

        var job = _repository.GetBackupJob(id);
        if (job == null)
        {
            Console.WriteLine(_languageService.GetString("JobNotFound") + id);
            Console.ReadKey();
            return;
        }

        Console.Write(_languageService.GetString("EnterJobName") + $" ({job.Name}): ");
        string name = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(name)) job.Name = name;

        Console.Write(_languageService.GetString("EnterSourcePath") + $" ({job.SourcePath}): ");
        string source = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(source)) job.SourcePath = source;

        Console.Write(_languageService.GetString("EnterTargetPath") + $" ({job.TargetPath}): ");
        string target = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(target)) job.TargetPath = target;

        Console.WriteLine(_languageService.GetString("CurrentBackupType") + $": {job.Type}");
        Console.WriteLine($"1. {_languageService.GetString("FullBackup")}");
        Console.WriteLine($"2. {_languageService.GetString("DifferentialBackup")}");
        Console.Write(_languageService.GetString("ChooseBackupType") + " (leave blank to keep current): ");
        string typeInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(typeInput))
        {
            job.Type = typeInput == "1" ? BackupType.Full : BackupType.Differential;
        }

        _repository.UpdateBackupJob(job);
        Console.WriteLine(_languageService.GetString("JobUpdatedSuccess"));
        Console.ReadKey();
    }

    private static void DeleteBackupJob()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("DeleteBackupJobTitle"));
        ListBackupJobs(false);

        Console.Write(_languageService.GetString("EnterJobIdToDelete") + ": ");
        if (!int.TryParse(Console.ReadLine(), out int id) || id < 1 || id > 5)
        {
            Console.WriteLine(_languageService.GetString("InvalidJobId"));
            Console.ReadKey();
            return;
        }

        _repository.DeleteBackupJob(id);
        Console.WriteLine(_languageService.GetString("JobDeletedSuccess"));
        Console.ReadKey();
    }

    private static void ListBackupJobs(bool waitForKey = true)
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("BackupJobsListTitle"));
        Console.WriteLine("--------------------------------------------------");

        var jobs = _repository.GetAllBackupJobs();
        if (jobs.Count == 0)
        {
            Console.WriteLine(_languageService.GetString("NoJobsFound"));
        }
        else
        {
            foreach (var job in jobs)
            {
                Console.WriteLine($"ID: {job.Id}");
                Console.WriteLine($"{_languageService.GetString("Name")}: {job.Name}");
                Console.WriteLine($"{_languageService.GetString("Source")}: {job.SourcePath}");
                Console.WriteLine($"{_languageService.GetString("Target")}: {job.TargetPath}");
                Console.WriteLine($"{_languageService.GetString("Type")}: {job.Type}");
                Console.WriteLine("--------------------------------------------------");
            }
        }

        if (waitForKey)
        {
            Console.ReadKey();
        }
    }

    private static void ExecuteSingleBackup()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("ExecuteBackupOptions"));
        Console.WriteLine(_languageService.GetString("ExecuteFormatHint"));
        ListBackupJobs(false);

        Console.Write(_languageService.GetString("EnterJobSelection") + ": ");
        var input = Console.ReadLine();

        if (!string.IsNullOrWhiteSpace(input))
        {
            ProcessCommandLine(input);
        }

        Console.WriteLine(_languageService.GetString("PressAnyKeyToContinue"));
        Console.ReadKey();
    }

    private static void ExecuteAllBackups()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("ExecutingAllBackups"));

        var jobs = _repository.GetAllBackupJobs();
        if (jobs.Count == 0)
        {
            Console.WriteLine(_languageService.GetString("NoJobsFound"));
        }
        else
        {
            foreach (var job in jobs)
            {
                ExecuteBackup(job.Id);
            }
        }

        Console.ReadKey();
    }

    private static void ChangeLanguage()
    {
        Console.Clear();
        Console.WriteLine(_languageService.GetString("ChangeLanguageTitle"));
        Console.WriteLine($"1. {_languageService.GetString("English")} ({_languageService.GetString("Current")})");
        Console.WriteLine($"2. {_languageService.GetString("French")}");
        Console.Write(_languageService.GetString("ChooseLanguage") + ": ");

        var input = Console.ReadLine();
        if (input == "1")
        {
            _languageService.SetLanguage("en");
            Console.WriteLine(_languageService.GetString("LanguageChanged"));
        }
        else if (input == "2")
        {
            _languageService.SetLanguage("fr");
            Console.WriteLine(_languageService.GetString("LanguageChanged"));
        }
        else
        {
            Console.WriteLine(_languageService.GetString("InvalidOption"));
        }

        Console.ReadKey();
    }
}