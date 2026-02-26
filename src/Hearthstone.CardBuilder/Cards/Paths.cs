namespace HearthstoneServer.Cards;

public static class Paths
{
    /// <summary>在指定目录下查找资源文件</summary>
    public static string FindResFileIn(string resDir, string file)
    {
        var p = Path.Combine(resDir, file);
        if (File.Exists(p)) return p;
        throw new FileNotFoundException($"找不到资源文件: {p}");
    }

    public static string FindResFile(string file)
    {
        // 1) 运行输出目录（bin/...）
        var p1 = Path.Combine(AppContext.BaseDirectory, "res", file);
        if (File.Exists(p1)) return p1;

        // 2) 当前工作目录（有时是项目根）
        var p2 = Path.Combine(Directory.GetCurrentDirectory(), "res", file);
        if (File.Exists(p2)) return p2;

        // 3) 兜底：当前目录是 bin/Debug/netX 时，回到项目根
        var p3 = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "res", file));
        if (File.Exists(p3)) return p3;

        throw new FileNotFoundException($"找不到资源文件 {file}，尝试过: {p1}, {p2}, {p3}");
    }

    public static string ResOutPath(string file)
        => Path.Combine(AppContext.BaseDirectory, "res", file);
}