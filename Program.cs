using System;
using System.Globalization;
using BackupApp.Services;
using BackupApp.Models;
using BackupApp.Data;

class Program
{
    private static BackupRepository _repository;
    private static BackupService _backupService;
    private static LanguageService _languageService;

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
        _repository = new BackupRepository();
        _backupService = new BackupService();
        _languageService = new LanguageService();

        // Définir la langue par défaut
        _languageService.SetLanguage("fr");
    }

    private static void ProcessCommandLine(string command)
    {
        try
        {
            if (command.Contains("-")) 
            {
                var range = command.Split('-');
                int start = int.Parse(range[0]);
                int end = int.Parse(range[1]);

                for (int i = start; i <= end; i++)
                {
                    ExecuteBackup(i);
                }
            }
            else if (command.Contains(";")) 
            {
                var jobs = command.Split(';');
                foreach (var job in jobs)
                {
                    ExecuteBackup(int.Parse(job));
                }
            }
            else 
            {
                ExecuteBackup(int.Parse(command));
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
            _backupService.PerformBackup(job);
        }
        else
        {
            Console.WriteLine(_languageService.GetString("JobNotFound") + jobId);
        }
    }

    private static void ShowMainMenu()
    {
        bool exit = false;

        while (!exit)
        {
            Console.Clear();
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
            Console.WriteLine($"8. {_languageService.GetString("Exit")}");
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
                    exit = true;
                    break;
                default:
                    Console.WriteLine(_languageService.GetString("InvalidOption"));
                    Console.ReadKey();
                    break;
            }
        }
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
        Console.WriteLine(_languageService.GetString("ExecuteSingleBackupTitle"));
        ListBackupJobs(false);

        Console.Write(_languageService.GetString("EnterJobIdToExecute") + ": ");
        if (!int.TryParse(Console.ReadLine(), out int id) || id < 1 || id > 5)
        {
            Console.WriteLine(_languageService.GetString("InvalidJobId"));
            Console.ReadKey();
            return;
        }

        ExecuteBackup(id);
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
        Console.WriteLine($"1. {_languageService.GetString("English")}");
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