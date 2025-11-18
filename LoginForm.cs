using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

namespace CampusLoginUI
{
    public class LoginForm : Form
    {
        // ==== 按需要修改的配置 ====
        private const string LoginUrl = "http://1.1.1.5/ac_portal/login.php";
        private const string TestHost = "1.1.1.5";   // 用于检测网络是否通
        private const int MaxWaitSeconds = 60;        // 每次最多等网络 60 秒
        // ==========================

        TextBox txtUser;
        TextBox txtPass;
        Button btnSaveAndTest;
        Button btnEnableAuto;
        Button btnDisableAuto;
        Button btnAbout;
        Label lblStatus;
        Label lblAutoStatus;

        bool _silent;

        string ConfigFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CampusLoginUI");
        string ConfigFilePath => Path.Combine(ConfigFolder, "config.ini");

        string _userName = "";
        string _password = "";

        public LoginForm(bool silent)
        {
            _silent = silent;
            InitializeComponent();
            LoadConfigToUI();
            UpdateAutoStatusLabel();

            // 静默模式：开机自动登录（由计划任务触发）
            if (_silent &&
                !string.IsNullOrWhiteSpace(_userName) &&
                !string.IsNullOrWhiteSpace(_password))
            {
                Shown += async (_, __) =>
                {
                    try { await LoginAsync(silent: true); } catch { }
                    Close();
                };
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
                Opacity = 0;
            }
        }

        private void InitializeComponent()
        {
            this.Text = "吉外校园网自动登录";
            this.Width = 460;
            this.Height = 260;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            var lblUser = new Label
            {
                Text = "用户名：",
                Left = 20,
                Top = 20,
                AutoSize = true
            };
            txtUser = new TextBox
            {
                Left = 130,
                Top = 18,
                Width = 280
            };

            var lblPass = new Label
            {
                Text = "密码：",
                Left = 20,
                Top = 60,
                AutoSize = true
            };
            txtPass = new TextBox
            {
                Left = 130,
                Top = 58,
                Width = 280,
                UseSystemPasswordChar = true
            };

            lblAutoStatus = new Label
            {
                Text = "自动登录状态：检测中...",
                Left = 20,
                Top = 95,
                Width = 390,
                AutoSize = false
            };

            btnEnableAuto = new Button
            {
                Text = "启用自动登录",
                Left = 20,
                Top = 125,
                Width = 160,
                Height = 30
            };
            btnEnableAuto.Click += BtnEnableAuto_Click;

            btnDisableAuto = new Button
            {
                Text = "关闭自动登录",
                Left = 250,
                Top = 125,
                Width = 160,
                Height = 30
            };
            btnDisableAuto.Click += BtnDisableAuto_Click;

            btnAbout = new Button
            {
                Text = "关于",
                Left = 20,
                Top = 165,
                Width = 80,
                Height = 30
            };
            btnAbout.Click += (s, e) => new AboutForm().ShowDialog(this);

            btnSaveAndTest = new Button
            {
                Text = "保存账号并测试登录",
                Left = 130,
                Top = 165,
                Width = 280,
                Height = 30
            };
            btnSaveAndTest.Click += async (s, e) =>
            {
                SaveFromUI();
                SaveConfig();

                btnSaveAndTest.Enabled = false;
                await LoginAsync(silent: false);
                btnSaveAndTest.Enabled = true;
            };

            lblStatus = new Label
            {
                Text = "状态：等待操作",
                Left = 20,
                Top = 205,
                Width = 390,
                AutoSize = false
            };

            this.Controls.Add(lblUser);
            this.Controls.Add(txtUser);
            this.Controls.Add(lblPass);
            this.Controls.Add(txtPass);
            this.Controls.Add(lblAutoStatus);
            this.Controls.Add(btnEnableAuto);
            this.Controls.Add(btnDisableAuto);
            this.Controls.Add(btnAbout);
            this.Controls.Add(btnSaveAndTest);
            this.Controls.Add(lblStatus);
        }

        #region 配置读写

        private void LoadConfigToUI()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    var lines = File.ReadAllLines(ConfigFilePath, Encoding.UTF8);
                    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                        var parts = trimmed.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            dict[parts[0].Trim()] = parts[1].Trim();
                        }
                    }

                    dict.TryGetValue("user", out _userName);
                    dict.TryGetValue("pass", out _password);
                }
                catch
                {
                    // 出错就忽略，用默认值
                }
            }

            txtUser.Text = _userName ?? "";
            txtPass.Text = _password ?? "";
        }

        private void SaveFromUI()
        {
            _userName = txtUser.Text.Trim();
            _password = txtPass.Text;
        }

        private void SaveConfig()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                    Directory.CreateDirectory(ConfigFolder);

                var lines = new List<string>
                {
                    "# 吉外校园网自动登录 配置文件",
                    $"user={_userName}",
                    $"pass={_password}"
                };
                File.WriteAllLines(ConfigFilePath, lines, Encoding.UTF8);
            }
            catch
            {
                // 保存失败不影响登录
            }
        }

        #endregion

        #region 自动登录按钮逻辑

        private async void BtnEnableAuto_Click(object? sender, EventArgs e)
        {
            // 保存账号密码（用于之后静默登录）
            SaveFromUI();
            SaveConfig();

            // 以管理员身份重新启动自己，传入 /enableAuto
            var psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = "/enableAuto",
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(psi);
                // 等一会儿，让计划任务创建完成，然后刷新状态
                await Task.Delay(1500);
                UpdateAutoStatusLabel();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 1223 = 用户取消 UAC
                if (ex.NativeErrorCode == 1223)
                {
                    SetStatus("用户取消了管理员授权。", true);
                }
                else
                {
                    SetStatus("启动管理员配置失败：" + ex.Message, true);
                }
            }
        }

        private async void BtnDisableAuto_Click(object? sender, EventArgs e)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                Arguments = "/disableAuto",
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                Process.Start(psi);
                await Task.Delay(1500);
                UpdateAutoStatusLabel();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                if (ex.NativeErrorCode == 1223)
                {
                    SetStatus("用户取消了管理员授权。", true);
                }
                else
                {
                    SetStatus("启动管理员配置失败：" + ex.Message, true);
                }
            }
        }

        private void UpdateAutoStatusLabel()
        {
            bool enabled = AutoStartManager.IsAutoLoginEnabled();
            lblAutoStatus.Text = enabled
                ? "自动登录状态：已启用（使用任务计划程序，开机自动运行 /silent）"
                : "自动登录状态：未启用";
        }

        #endregion

        #region 网络检测 + 登录逻辑

        private async Task LoginAsync(bool silent)
        {
            if (string.IsNullOrWhiteSpace(_userName) || string.IsNullOrWhiteSpace(_password))
            {
                if (!silent)
                    SetStatus("用户名或密码为空。", true);
                return;
            }

            if (!silent)
                SetStatus("等待网络就绪...", false);

            bool netOk = await Task.Run(() => WaitForNetwork(TestHost, MaxWaitSeconds));
            if (!netOk)
            {
                if (!silent)
                    SetStatus("在限定时间内未检测到网络可用。", true);
                return;
            }

            if (!silent)
                SetStatus("正在发送登录请求...", false);

            long authTag = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string pwdEncrypted = Rc4Hex(_password, authTag.ToString());

            var data = new Dictionary<string, string>
            {
                { "opr", "pwdLogin" },
                { "userName", _userName },
                { "pwd", pwdEncrypted },
                { "auth_tag", authTag.ToString() },
                { "rememberPwd", "1" }
            };

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var content = new FormUrlEncodedContent(data);
                var resp = await client.PostAsync(LoginUrl, content);
                string _ = await resp.Content.ReadAsStringAsync();

                if (!silent)
                    SetStatus($"登录请求已发送，HTTP { (int)resp.StatusCode }。", false);
            }
            catch (Exception ex)
            {
                if (!silent)
                    SetStatus("登录请求失败：" + ex.Message, true);
            }
        }

        private bool WaitForNetwork(string host, int timeoutSeconds)
        {
            int elapsed = 0;
            using var ping = new Ping();

            while (elapsed < timeoutSeconds)
            {
                try
                {
                    var reply = ping.Send(host, 2000);
                    if (reply.Status == IPStatus.Success)
                        return true;
                }
                catch
                {
                    // 忽略异常
                }

                System.Threading.Thread.Sleep(3000);
                elapsed += 3;
            }

            return false;
        }

        // RC4 + hex，加密密码
        private string Rc4Hex(string src, string passwd)
        {
            src = src.Trim();
            passwd = passwd ?? "";
            int plen = passwd.Length;
            int slen = src.Length;
            if (plen == 0 || slen == 0)
                return "";

            int[] key = new int[256];
            int[] sbox = new int[256];
            string[] output = new string[slen];

            for (int i = 0; i < 256; i++)
            {
                key[i] = passwd[i % plen];
                sbox[i] = i;
            }

            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + sbox[i] + key[i]) % 256;
                int temp = sbox[i];
                sbox[i] = sbox[j];
                sbox[j] = temp;
            }

            int a = 0;
            int b = 0;
            for (int i = 0; i < slen; i++)
            {
                a = (a + 1) % 256;
                b = (b + sbox[a]) % 256;
                int temp = sbox[a];
                sbox[a] = sbox[b];
                sbox[b] = temp;
                int c = (sbox[a] + sbox[b]) % 256;

                int x = ((int)src[i]) ^ sbox[c];
                output[i] = x.ToString("x2");
            }

            return string.Join("", output);
        }

        private void SetStatus(string text, bool isError)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => SetStatus(text, isError)));
                return;
            }

            lblStatus.Text = "状态：" + text;
            lblStatus.ForeColor = isError ? System.Drawing.Color.Red : System.Drawing.Color.Black;
        }

        #endregion
    }
}
