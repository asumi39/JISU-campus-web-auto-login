using System.Drawing;
using System.Windows.Forms;

namespace CampusLoginUI
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            this.Text = "关于";
            this.Width = 360;
            this.Height = 200;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lbl = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei UI", 10),
                Text =
@"吉外校园网自动登录
v1.0.0
如果有任何问题欢迎联络我
我的微信：asumi39"
            };

            this.Controls.Add(lbl);
        }
    }
}
