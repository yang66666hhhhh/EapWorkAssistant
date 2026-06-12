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
    public List<ContentTemplate> ContentTemplates => _data.ContentTemplates;

    public bool EnableShortcuts
    {
        get => _data.EnableShortcuts;
        set { _data.EnableShortcuts = value; Save(); }
    }

    public bool EnableReminder
    {
        get => _data.EnableReminder;
        set { _data.EnableReminder = value; Save(); }
    }

    public int ReminderHour
    {
        get => _data.ReminderHour;
        set { _data.ReminderHour = value; Save(); }
    }

    public int ReminderMinute
    {
        get => _data.ReminderMinute;
        set { _data.ReminderMinute = value; Save(); }
    }

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
                },
                ContentTemplates = new List<ContentTemplate>
                {
                    new() { Name = "晨会", Content = "1. 参加晨会，汇报昨日进展和今日计划\n2. 同步项目进度和风险" },
                    new() { Name = "代码评审", Content = "1. 评审XX模块代码\n2. 提出优化建议\n3. 确认代码质量" },
                    new() { Name = "Bug修复", Content = "1. 定位问题根因\n2. 修复XX功能异常\n3. 编写单元测试验证" },
                    new() { Name = "需求分析", Content = "1. 与产品确认需求细节\n2. 编写技术方案\n3. 评估工时" },
                    new() { Name = "文档编写", Content = "1. 编写XX接口文档\n2. 更新项目README\n3. 整理技术文档" },
                    new() { Name = "环境部署", Content = "1. 部署测试环境\n2. 配置服务参数\n3. 验证服务状态" }
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

    public void AddContentTemplate(ContentTemplate template)
    {
        lock (_lock)
        {
            if (!_data.ContentTemplates.Any(t => t.Name == template.Name))
            {
                _data.ContentTemplates.Add(template);
                Save();
            }
        }
    }

    public void RemoveContentTemplate(string name)
    {
        lock (_lock)
        {
            var template = _data.ContentTemplates.FirstOrDefault(t => t.Name == name);
            if (template != null)
            {
                _data.ContentTemplates.Remove(template);
                Save();
            }
        }
    }

    public void UpdateContentTemplate(string oldName, ContentTemplate newTemplate)
    {
        lock (_lock)
        {
            var index = _data.ContentTemplates.FindIndex(t => t.Name == oldName);
            if (index >= 0)
            {
                _data.ContentTemplates[index] = newTemplate;
                Save();
            }
        }
    }
}
