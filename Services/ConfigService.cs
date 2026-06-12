using System.IO;
using System.Text.Json;

namespace EapWorkAssistant.Services;

public class ConfigService
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EapWorkAssistant");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public static ConfigService Instance { get; } = new ConfigService();

    private ConfigData _data = new();
    private readonly object _lock = new();

    static ConfigService()
    {
        Instance.Load();
    }

    public List<string> Projects => _data.Projects;
    public List<string> WorkTypes => _data.WorkTypes;

    public void Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var loaded = JsonSerializer.Deserialize<ConfigData>(json);
                    if (loaded != null)
                    {
                        _data = loaded;
                        return;
                    }
                }
            }
            catch { }

            // 默认配置
            _data = new ConfigData
            {
                Projects = new List<string>
                {
                    "HDI工业物联网平台",
                    "EAP设备自动化",
                    "MES制造执行",
                    "AGV物流调度",
                    "SECS/GEM通信",
                    "其他"
                },
                WorkTypes = new List<string>
                {
                    "开发",
                    "运维",
                    "会议",
                    "学习",
                    "调试",
                    "文档"
                }
            };
            Save();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);

                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_data, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
            catch { }
        }
    }

    public void AddProject(string project)
    {
        lock (_lock)
        {
            if (!_data.Projects.Contains(project))
            {
                _data.Projects.Add(project);
                Save();
            }
        }
    }

    public void RemoveProject(string project)
    {
        lock (_lock)
        {
            _data.Projects.Remove(project);
            Save();
        }
    }

    public void UpdateProject(string oldProject, string newProject)
    {
        lock (_lock)
        {
            var index = _data.Projects.IndexOf(oldProject);
            if (index >= 0)
            {
                _data.Projects[index] = newProject;
                Save();
            }
        }
    }

    public void AddWorkType(string workType)
    {
        lock (_lock)
        {
            if (!_data.WorkTypes.Contains(workType))
            {
                _data.WorkTypes.Add(workType);
                Save();
            }
        }
    }

    public void RemoveWorkType(string workType)
    {
        lock (_lock)
        {
            _data.WorkTypes.Remove(workType);
            Save();
        }
    }

    public void UpdateWorkType(string oldWorkType, string newWorkType)
    {
        lock (_lock)
        {
            var index = _data.WorkTypes.IndexOf(oldWorkType);
            if (index >= 0)
            {
                _data.WorkTypes[index] = newWorkType;
                Save();
            }
        }
    }
}
