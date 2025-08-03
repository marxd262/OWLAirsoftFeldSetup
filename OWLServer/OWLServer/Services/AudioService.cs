using System.Diagnostics;
using System.Runtime.InteropServices;
using OWLServer.Models;

namespace OWLServer.Services;

public class AudioService
{
    private string GetFile(Sounds sound)
    {
        switch (sound)
        {
            case Sounds.Start:
                return "./wwwroot/Sounds/GameStart.mp3";
            case Sounds.Stop:
                return "./wwwroot/Sounds/GameOver.mp3";
            case Sounds.Countdown:
                return "";
            case Sounds.Freeze:
                return "";
            default:
                return "";
        }
    }

    public void PlaySound(Sounds sound)
    {
        string file = GetFile(sound);

        if (file != "" && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string command = "play -q " + file;
            string commandRet = RunCommandWithBash(command); //("pwd");//
            
            Console.WriteLine(commandRet);
        }
    }
    
    public string RunCommandWithBash(string command)
    {
        var arg = "-c \"" + command + "\"";
        
        Console.WriteLine("arg: " + arg);
        
        var psi = new ProcessStartInfo();
        psi.FileName = "/bin/bash";
        psi.Arguments = arg;
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

        using var process = Process.Start(psi);

        if (process == null) return "";

        process?.WaitForExit();

        var output = process?.StandardOutput.ReadToEnd();

        return "output";
    }
}