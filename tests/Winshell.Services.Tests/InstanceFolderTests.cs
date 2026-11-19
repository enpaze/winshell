using System.Text.Json;

namespace Winshell.Services.Tests;

public sealed class InstanceFolderTests : IDisposable
{
    private readonly string _root = TempFolder.NewPath();

    public InstanceFolderTests()
    {
        Directory.CreateDirectory(_root);
    }

    private Instances Instances => new(_root);

    [Fact]
    public void The_instance_file_keeps_process_start_and_exited_at()
    {
        var info = new InstanceInfo("id", 1, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now, "pipe", "C:\\log.txt", 42,
            DateTime.Now);
        var file = JsonSerializer.Serialize(info, InstanceJson.Default.InstanceInfo);
        Assert.Contains("\"processStart\": 42", file, StringComparison.Ordinal);
        Assert.Contains("\"exitedAt\":", file, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clean_exit_keeps_the_folder_and_is_not_listed_as_running()
    {
        var id = "abc123abc123";
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);
        var info = new InstanceInfo(id, 0, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now, "winshell-" + id,
            Path.Combine(dir, "log.txt"), 1);
        File.WriteAllText(Path.Combine(dir, "instance.json"),
            JsonSerializer.Serialize(info, InstanceJson.Default.InstanceInfo));
        File.WriteAllText(info.Log, "hello");

        Instances.Remove(id);

        Assert.True(File.Exists(Path.Combine(dir, "instance.json")));
        Assert.Equal("hello", File.ReadAllText(info.Log));
        var written = JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(dir, "instance.json")),
            InstanceJson.Default.InstanceInfo);
        Assert.NotNull(written!.ExitedAt);
        Assert.DoesNotContain(Instances.Running(), info => info.Id == id);
        Assert.Contains(Instances.All(), row => row.Info.Id == id && !row.Alive);
    }

    [Fact]
    public void Clean_deletes_an_old_exit_and_an_old_folder_with_no_instance_file()
    {
        var oldExit = Path.Combine(_root, "oldexitoldexi");
        Directory.CreateDirectory(oldExit);
        var exited = new InstanceInfo("oldexitoldexi", 0, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now.AddDays(-3),
            "pipe", Path.Combine(oldExit, "log.txt"), 1, DateTime.Now.AddDays(-2));
        File.WriteAllText(Path.Combine(oldExit, "instance.json"),
            JsonSerializer.Serialize(exited, InstanceJson.Default.InstanceInfo));

        var young = Path.Combine(_root, "youngeyoungex");
        Directory.CreateDirectory(young);
        var fresh = new InstanceInfo("youngeyoungex", 0, "shell", "C:\\cfg\\MyShell.exe", DateTime.Now, "pipe",
            Path.Combine(young, "log.txt"), 1, DateTime.Now);
        File.WriteAllText(Path.Combine(young, "instance.json"),
            JsonSerializer.Serialize(fresh, InstanceJson.Default.InstanceInfo));

        var orphan = Path.Combine(_root, "orphanorphan");
        Directory.CreateDirectory(orphan);
        var orphanLog = Path.Combine(orphan, "log.txt");
        File.WriteAllText(orphanLog, "old");
        File.SetLastWriteTime(orphanLog, DateTime.Now.AddDays(-2));

        var recentOrphan = Path.Combine(_root, "recentorphan");
        Directory.CreateDirectory(recentOrphan);
        File.WriteAllText(Path.Combine(recentOrphan, "log.txt"), "new");

        Instances.Clean();

        Assert.False(Directory.Exists(oldExit));
        Assert.False(Directory.Exists(orphan));
        Assert.True(Directory.Exists(young));
        Assert.True(Directory.Exists(recentOrphan));
    }

    [Fact]
    public void A_record_without_shell_name_and_program_is_unreadable_like_a_corrupt_one()
    {
        using var self = System.Diagnostics.Process.GetCurrentProcess();
        var old = Path.Combine(_root, "oldfieldsoldf");
        Directory.CreateDirectory(old);
        var oldRecord = $$"""
            {
              "id": "oldfieldsoldf",
              "pid": {{Environment.ProcessId}},
              "shellId": "work",
              "configPath": "C:\\shells\\work\\bin\\shell\\Work.exe",
              "launchTime": "{{DateTime.Now.AddDays(-3):O}}",
              "pipe": "winshell-oldfieldsoldf",
              "log": "{{Path.Combine(old, "log.txt").Replace("\\", "\\\\", StringComparison.Ordinal)}}",
              "processStart": {{self.StartTime.ToUniversalTime().Ticks}},
              "processName": "{{self.ProcessName}}"
            }
            """;
        File.WriteAllText(Path.Combine(old, "instance.json"), oldRecord);
        var corrupt = Path.Combine(_root, "corruptcorru");
        Directory.CreateDirectory(corrupt);
        File.WriteAllText(Path.Combine(corrupt, "instance.json"), "{ \"id\": ");

        Assert.Empty(Instances.All());
        Assert.Empty(Instances.Running());
        Instances.Remove("oldfieldsoldf");
        Instances.Clean();

        Assert.Equal(oldRecord, File.ReadAllText(Path.Combine(old, "instance.json")));
        Assert.True(Directory.Exists(corrupt));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
