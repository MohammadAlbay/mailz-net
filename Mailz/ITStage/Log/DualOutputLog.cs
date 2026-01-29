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
        // Initialize logging to file and console
        this.logFilePath = logFilePath;
        this.consoleOutput = consoleOutput ?? Console.Out;
        this.fileWriter = new System.IO.StreamWriter(logFilePath, append: true);
    }

    public async void Log(string message)
    {
        string timestampedMessage = $"[{prefix}][{DateTime.Now}]: {message}";

        // Log to console
        await consoleOutput.WriteLineAsync(timestampedMessage);

        // Log to file
        await fileWriter.WriteLineAsync(timestampedMessage);
        await fileWriter.FlushAsync();
    }

    public void Close()
    {
        fileWriter.Close();
    }
}
