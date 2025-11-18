using System;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using System.Diagnostics;

namespace CampusLoginUI
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool silent = args.Any(a => a.Equals("/silent", StringComparison.OrdinalIgnoreCase));
            bool enableAuto = args.Any(a => a.Equals("/enableAuto", StringComparison.OrdinalIgnoreCase));
            bool disableAuto = args.Any(a => a.Equals("/disableAuto", StringComparison.OrdinalIgnoreCase));

            // 处理启用/关闭自动登录（管理员模式）
            if (enableAuto || disableAuto)
            {
                if (!AutoStartManager.IsRunAsAdmin())
                {
                    MessageBox.Show("请以管理员身份运行此程序来配置自动登录。", "权限不足",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string exePath = Application.ExecutablePath;

                try
                {
                    if (enableAuto)
                    {
                        AutoStartManager.EnableScheduledAutoLogin(exePath);
                        MessageBox.Show("已启用开机自动登录（任务计划程序）。", "成功",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        AutoStartManager.DisableScheduledAutoLogin();
                        MessageBox.Show("已关闭开机自动登录。", "成功",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("配置自动登录时出错：\n" + ex.Message, "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                return; // 配置完就退出
            }

            // 正常 / 静默登录模式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new LoginForm(silent));
        }
    }

    /// <summary>
    /// 负责用 schtasks.exe 创建 / 删除「吉外校园网自动登录」的计划任务。
    /// </summary>
    public static class AutoStartManager
    {
        public const string TaskName = "CampusAutoLogin";

        public static bool IsRunAsAdmin()
        {
            var id = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// 创建「用户登录时」运行 exe /silent 的计划任务，权限最高。
        /// </summary>
        public static void EnableScheduledAutoLogin(string exePath)
        {
            // 先删掉旧的，避免重复
            DisableScheduledAutoLogin();

            // 为 schtasks 转义路径里的引号
            string escapedPath = exePath.Replace("\"", "\"\"");

            // /SC ONLOGON：用户登录时触发
            // /RL HIGHEST：最高权限
            // /F：覆盖已有同名任务
            string arguments =
                $"/Create /SC ONLOGON /RL HIGHEST /F /TN \"{TaskName}\" " +
                $"/TR \"\"{escapedPath}\" /silent\"";

            RunSchTasks(arguments);
        }

        /// <summary>
        /// 删除计划任务，关闭自动登录。
        /// </summary>
        public static void DisableScheduledAutoLogin()
        {
            string arguments = $"/Delete /TN \"{TaskName}\" /F";
            RunSchTasks(arguments, ignoreError: true);
        }

        /// <summary>
        /// 查询计划任务是否存在，用于在 UI 上显示当前状态。
        /// </summary>
        public static bool IsAutoLoginEnabled()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskName}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (!p.WaitForExit(2000))
                {
                    try { p.Kill(); } catch { }
                    return false;
                }
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private static void RunSchTasks(string arguments, bool ignoreError = false)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            string _ = p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (!ignoreError && p.ExitCode != 0)
            {
                throw new Exception("schtasks 失败：" + err);
            }
        }
    }
}

