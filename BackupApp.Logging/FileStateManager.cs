using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BackupApp.Logging
{
    public class FileStateManager : IStateManager
    {
        private readonly string _stateFilePath;
        private readonly Dictionary<string, BackupState> _states;
        private readonly object _lock = new object();

        public FileStateManager()
        {
            _stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EasySave",
                "state.json");

            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath));
            _states = LoadStates();
        }

        public void UpdateState(string backupName, BackupState state)
        {
            lock (_lock)
            {
                _states[backupName] = state;
                SaveStates();
            }
        }

        public BackupState GetCurrentState(string backupName)
        {
            return _states.TryGetValue(backupName, out var state) ? state : null;
        }

        public List<BackupState> GetAllStates()
        {
            return new List<BackupState>(_states.Values);
        }

        private Dictionary<string, BackupState> LoadStates()
        {
            if (!File.Exists(_stateFilePath))
                return new Dictionary<string, BackupState>();

            string json = File.ReadAllText(_stateFilePath);
            return JsonSerializer.Deserialize<Dictionary<string, BackupState>>(json)
                   ?? new Dictionary<string, BackupState>();
        }

        private void SaveStates()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(_states, options);
            File.WriteAllText(_stateFilePath, json);
        }
    }
}