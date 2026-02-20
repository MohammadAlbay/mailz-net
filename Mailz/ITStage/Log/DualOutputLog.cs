namespace ITStage.Log;

/*
    This class handles logging to standard output and to a file simultaneously.
*/
public class DualOutputLog
{
    private readonly string logFilePath;
    private readonly System.IO.StreamWriter fileWriter;
    private readonly System.IO.TextWriter consoleOutput;
    private readonly string prefix;
    // Implementation of DualOutputLog
    public DualOutputLog(string prefix = "", string logFilePath = "log.txt", System.IO.TextWriter? consoleOutput = null)
    {
        this.prefix = prefix;
        // Initialize logging to file and console
        this.logFilePath = logFilePath;
        this.consoleOutput = consoleOutput ?? Console.Out;
        fileWriter = new StreamWriter(logFilePath, append: true) { AutoFlush = true };
    }

    public async Task LogAsync(string message)
    {
        string timestampedMessage = $"[{prefix}][{DateTime.Now}]: {message}";

        await consoleOutput.WriteLineAsync(timestampedMessage);

        try { await fileWriter.WriteLineAsync(timestampedMessage); } catch { }

    }

    public void Log(string message)
    {
        string timestampedMessage = $"[{prefix}][{DateTime.Now}]: {message}";

        consoleOutput.WriteLine(timestampedMessage);

        try { fileWriter.WriteLine(timestampedMessage); } catch { }

    }

    public void Close()
    {
        fileWriter.Close();
    }
}
