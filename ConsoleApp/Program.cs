string[] AllowedExtensions = [".ncm", ".kgm", ".kgma", ".kgg"];

const string KggExtension = ".kgg";
const string DefaultKgDbRelativePath = @"AppData\Roaming\KuGou8\KGMusicV3.db";

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

await Parser.Default.ParseArguments<Options>(args)
    .WithParsedAsync(RunAsync);

async Task RunAsync(Options options)
{
    var input = Path.GetFullPath(options.Input);
    var outputDir = Path.GetFullPath(options.Output);
    var isFile = File.Exists(input);

    if (!isFile && !Directory.Exists(input))
    {
        logger.Error("输入的文件或目录不存在: {Input}", input);
        return;
    }

    logger.Information("输入[{Type}]: {Input}", isFile ? "文件" : "目录", input);
    logger.Information("输出目录: {OutputDir}", outputDir);

    var files = isFile ? [input] : SearchFiles(options);
    logger.Information("找到 {Count} 个待解密文件", files.Length);

    var autoDecrypter = new AutoDecrypter();

    // 检查是否需要处理 KGG 文件
    if (files.Any(f => KggExtension.Equals(Path.GetExtension(f), StringComparison.OrdinalIgnoreCase)))
    {
        string dbPath = options.KgDb ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            DefaultKgDbRelativePath);

        if (File.Exists(dbPath))
        {
            autoDecrypter.AddKGG(dbPath);
            logger.Information("已加载 KGG 数据库: {DbPath}", dbPath);
        }
        else
        {
            logger.Warning("未找到 kgdb 文件，无法解密 kgg 格式文件: {DbPath}", dbPath);
        }
    }

    if (!Directory.Exists(outputDir))
    {
        Directory.CreateDirectory(outputDir);
    }

    // 处理所有文件
    for (int i = 0; i < files.Length; i++)
    {
        var file = files[i];
        logger.Information("[{Index}/{Total}] 处理文件: {File}", i + 1, files.Length, Path.GetFileName(file));

        try
        {
            await ProcessFileAsync(autoDecrypter, file, outputDir, options.Overwrite);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Error("无权限访问文件: {File} - {Message}", Path.GetFileName(file), ex.Message);
        }
        catch (IOException ex)
        {
            logger.Error("IO 错误: {File} - {Message}", Path.GetFileName(file), ex.Message);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "处理文件时发生未知错误: {File} - {Message}", Path.GetFileName(file), ex.Message);
        }
    }

    logger.Information("处理完成！");
}

async Task ProcessFileAsync(AutoDecrypter autoDecrypter, string filePath, string outputDir, bool overwrite)
{
    var fileName = Path.GetFileName(filePath);
    var outputPath = Path.Combine(outputDir, fileName);

    using var fs = File.OpenRead(filePath);

    if (!autoDecrypter.TryGetDecrypter(fs, out var decrypter))
    {
        logger.Warning("不支持的文件格式，跳过: {File}", fileName);
        return;
    }

    var audioFormat = decrypter.DetectAudioFormat(fs);
    var ext = audioFormat.ToString().ToLowerInvariant();
    var finalOutputPath = Path.ChangeExtension(outputPath, ext);

    if (File.Exists(finalOutputPath) && !overwrite)
    {
        logger.Warning("输出文件已存在且未指定覆盖，跳过: {OutputFile}", Path.GetFileName(finalOutputPath));
        return;
    }

    // 重置流位置以便解密
    fs.Position = 0;

    using (var outputFs = File.Create(finalOutputPath))
    {
        await decrypter.DecryptStreamAsync(fs, outputFs);
    }

    // NCM 格式需要额外处理封面
    if (decrypter is NCM ncm)
    {
        fs.Position = 0; // 重置流位置
        ncm.PatchCoverImage(fs, finalOutputPath);
    }

    logger.Information("成功解密: {Output}", Path.GetFileName(finalOutputPath));
}

string[] SearchFiles(Options options)
{
    var files = new List<string>();
    var searchOption = options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

    foreach (var ext in AllowedExtensions)
    {
        try
        {
            files.AddRange(Directory.GetFiles(options.Input, $"*{ext}", searchOption));
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.Warning("无权限访问目录: {Message}", ex.Message);
        }
    }

    return [.. files];
}

class Options
{
    [Option('i', "input", Required = true, HelpText = "待解密文件或目录")]
    public string Input { get; set; } = string.Empty;

    [Option('o', "output", Required = true, HelpText = "解密后文件输出目录")]
    public string Output { get; set; } = string.Empty;

    [Option('w', "overwrite", Default = false, HelpText = "是否覆盖已存在的文件")]
    public bool Overwrite { get; set; }

    [Option('r', "recursive", Default = false, HelpText = "是否递归处理目录下的文件(当输入为文件夹时有效)")]
    public bool Recursive { get; set; }

    [Option("kgdb", HelpText = "指定 kgdb 文件路径")]
    public string? KgDb { get; set; }
}
