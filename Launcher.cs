using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

internal static class LunaLauncher {
    private static readonly string[] Payload = {
        "LunaQuiz.ps1", "FrameState.ps1", "QuizOcr.ps1", "MapleTheme.cs", "QuizHeader.cs", "QuizUpdater.cs", "UpdateOverlay.cs", "VERSION.txt",
        "Maplestory Bold.ttf", "LICENSE-Maplestory.txt"
    };

    [STAThread]
    private static int Main(string[] args) {
        if (args.Length > 0 && args[0] == "--install-update") return UpdateInstall.Run(args);
        string dir = Path.Combine(Path.GetTempPath(), "LunaQuiz-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(dir);
            foreach (string file in Payload) {
                using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
                using (Stream output = File.Create(Path.Combine(dir, file))) {
                    if (input == null) throw new InvalidOperationException("실행 리소스 누락: " + file);
                    input.CopyTo(output);
                }
            }
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(powershell,
                "-NoProfile -STA -ExecutionPolicy Bypass -File \"" + Path.Combine(dir, "LunaQuiz.ps1") +
                "\" -LauncherPath \"" + Assembly.GetExecutingAssembly().Location + "\"");
            bool smokeTest = args.Length == 1 && args[0] == "--smoke-test";
            if (smokeTest) start.Arguments += " -SmokeTest";
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardError = true;
            start.StandardErrorEncoding = System.Text.Encoding.UTF8;
            using (Process child = Process.Start(start)) {
                string errors = child.StandardError.ReadToEnd();
                child.WaitForExit();
                if (child.ExitCode == 42 && !smokeTest) {
                    string instructions = Path.Combine(dir, "update-ready.txt");
                    string[] fields = File.ReadAllLines(instructions);
                    if (fields.Length != 3) throw new InvalidDataException("업데이트 정보를 읽지 못했습니다.");
                    string worker = Path.Combine(dir, "LunaUpdate.exe");
                    File.Copy(Assembly.GetExecutingAssembly().Location, worker);
                    var install = new ProcessStartInfo(worker, "--install-update \"" +
                        Assembly.GetExecutingAssembly().Location + "\" \"" + fields[0] + "\" " +
                        fields[1] + " " + Process.GetCurrentProcess().Id + " " + child.Id + " " + fields[2]);
                    install.UseShellExecute = false;
                    install.CreateNoWindow = true;
                    Process.Start(install);
                    return 0;
                }
                if (child.ExitCode != 0 && !smokeTest)
                    MessageBox.Show(errors, "Luna 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return child.ExitCode;
            }
        } catch (Exception error) {
            MessageBox.Show(error.Message, "Luna 실행 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        } finally {
            foreach (string file in Payload) {
                try { File.Delete(Path.Combine(dir, file)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            try { File.Delete(Path.Combine(dir, "update-ready.txt")); } catch (IOException) {} catch (UnauthorizedAccessException) {}
            try { Directory.Delete(dir); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
