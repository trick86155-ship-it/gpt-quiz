using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

internal static class UpdateInstall {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string source, string target, uint flags);

    internal static int Run(string[] args) {
        try {
            Install(args);
            return 0;
        } catch (Exception error) {
            MessageBox.Show(error.Message, "루나 퀴즈 업데이트", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    internal static void Install(string[] args) {
            if (args.Length != 7) throw new ArgumentException("업데이트 인수가 올바르지 않습니다.");
            string target = Path.GetFullPath(args[1]), stage = Path.GetFullPath(args[2]);
            string hash = args[3];
            if (!String.Equals(Path.GetFileName(target), "LunaQuiz.exe", StringComparison.OrdinalIgnoreCase) ||
                !String.Equals(Path.GetDirectoryName(stage), Path.GetDirectoryName(target), StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(stage).StartsWith(".LunaQuiz-update-", StringComparison.Ordinal) ||
                Path.GetExtension(stage) != ".download") throw new ArgumentException("업데이트 대상 경로가 올바르지 않습니다.");
            foreach (int id in new[] { Int32.Parse(args[4]), Int32.Parse(args[5]) }) {
                try {
                    using (var process = Process.GetProcessById(id))
                        if (!process.WaitForExit(30000)) throw new IOException("실행 중인 프로그램이 종료되지 않았습니다.");
                } catch (ArgumentException) {}
            }
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(stage)) {
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != hash)
                    throw new InvalidDataException("업데이트 파일 체크섬이 다릅니다.");
            }
            string version = FileVersionInfo.GetVersionInfo(stage).FileVersion;
            if (Version.Parse(version).ToString(3) != Version.Parse(args[6]).ToString(3))
                throw new InvalidDataException("업데이트 버전이 다릅니다.");
            bool replaced = false;
            for (int attempt = 0; attempt < 30; attempt++) {
                if (MoveFileEx(stage, target, 1 | 8)) { replaced = true; break; }
                Thread.Sleep(200);
            }
            if (!replaced) throw new IOException("업데이트 파일을 교체하지 못했습니다.", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(target) });
    }
}
