using ClientCore;
using ClientCore.Extensions;
using ClientGUI;
using DTAClient.Domain.Multiplayer.CnCNet;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rampastring.XNAUI;
using Rampastring.XNAUI.Input;
using Rampastring.XNAUI.XNAControls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

#nullable enable

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class CnCNetAccountLoginWindow : XNAWindow
    {
        public event EventHandler? Cancel;
        public event EventHandler? LoginSuccess;

        private XNATextBox tbPlayerEmail = null!;
        private XNAPasswordBox tbPlayerPassword = null!;
        private XNAClientCheckBox? chkStayLoggedIn;
        private XNALabel lblError = null!;
        private XNAClientButton btnLogin = null!;

        public CnCNetAccountLoginWindow(WindowManager windowManager) : base(windowManager) { }

        public override void Initialize()
        {
            Name = nameof(CnCNetAccountLoginWindow);
            BackgroundTexture = AssetLoader.LoadTextureUncached("logindialogbg.png");
            ClientRectangle = new Rectangle(0, 0, 350, 200);

            var lblLoginWindowTitle = new XNALabel(WindowManager)
            {
                Name = "lblWindowTitle",
                FontIndex = 1,
                Text = "LOGIN TO CNCNET".L10N("Client:CnCNet:LoginToCnCNetTitle")
            };
            AddChild(lblLoginWindowTitle);
            lblLoginWindowTitle.CenterOnParent();
            lblLoginWindowTitle.ClientRectangle = new Rectangle(lblLoginWindowTitle.X, 12, lblLoginWindowTitle.Width, lblLoginWindowTitle.Height);

            btnLogin = new XNAClientButton(WindowManager)
            {
                Name = "btnLogin",
                ClientRectangle = new Rectangle(12, ClientRectangle.Bottom - 35, 92, 23),
                Text = "Login".L10N("Client:CnCNet:LoginButton")
            };
            btnLogin.LeftClick += BtnLogin_LeftClick;

            var btnRegister = new XNAClientButton(WindowManager)
            {
                Name = "btnRegister",
                ClientRectangle = new Rectangle((Width - 92) / 2, ClientRectangle.Bottom - 35, 92, 23),
                Text = "Register".L10N("Client:CnCNet:RegisterButton")
            };
            btnRegister.LeftClick += BtnRegister_LeftClick;

            var btnCancel = new XNAClientButton(WindowManager)
            {
                Name = "btnCancel",
                ClientRectangle = new Rectangle(Width - 104, btnLogin.Y, 92, 23),
                Text = "Cancel".L10N("Client:Main:ButtonCancel")
            };
            btnCancel.LeftClick += BtnCancel_LeftClick;

            tbPlayerEmail = new XNATextBox(WindowManager)
            {
                Name = "tbPlayerEmail",
                ClientRectangle = new Rectangle(100, 50, 200, 19),
                Text = string.Empty
            };

            var lblPlayerEmail = new XNALabel(WindowManager)
            {
                Name = "lblPlayerEmail",
                FontIndex = 1,
                Text = "Email:".L10N("Client:CnCNet:EmailLabel")
            };
            lblPlayerEmail.ClientRectangle = new Rectangle(12, tbPlayerEmail.ClientRectangle.Y + 1, lblPlayerEmail.ClientRectangle.Width, lblPlayerEmail.ClientRectangle.Height);

            var lblPlayerPassword = new XNALabel(WindowManager)
            {
                Name = "lblPlayerPassword",
                FontIndex = 1,
                Text = "Password:".L10N("Client:CnCNet:PasswordLabel")
            };
            lblPlayerPassword.ClientRectangle = new Rectangle(12, tbPlayerEmail.ClientRectangle.Y + 35, lblPlayerPassword.ClientRectangle.Width, lblPlayerPassword.ClientRectangle.Height);

            tbPlayerPassword = new XNAPasswordBox(WindowManager)
            {
                Name = "tbPlayerPassword",
                ClientRectangle = new Rectangle(100, lblPlayerPassword.ClientRectangle.Y, 200, 19),
                Text = string.Empty
            };

            if (ClientConfiguration.Instance.UseCnCNetAPI)
            {
                chkStayLoggedIn = new XNAClientCheckBox(WindowManager)
                {
                    Name = "chkStayLoggedIn",
                    ClientRectangle = new Rectangle(100, tbPlayerPassword.ClientRectangle.Y + 28, 200, 18),
                    Text = "Stay Logged In".L10N("Client:CnCNet:StayLoggedInCheckBox"),
                    Checked = true
                };
            }

            int errorLabelY = chkStayLoggedIn?.Bottom + 7 ?? tbPlayerPassword.ClientRectangle.Y + 35;

            lblError = new XNALabel(WindowManager)
            {
                Name = "lblError",
                FontIndex = 1,
                TextColor = Color.Red,
                Text = string.Empty
            };
            lblError.ClientRectangle = new Rectangle(12, errorLabelY, lblError.ClientRectangle.Width, lblError.ClientRectangle.Height);

            AddChild(tbPlayerEmail);
            AddChild(tbPlayerPassword);
            if (ClientConfiguration.Instance.UseCnCNetAPI)
                AddChild(chkStayLoggedIn);
            AddChild(lblPlayerEmail);
            AddChild(lblPlayerPassword);
            AddChild(btnLogin);
            AddChild(btnCancel);
            AddChild(btnRegister);
            AddChild(lblError);

            base.Initialize();
            CenterOnParent();
            Keyboard.OnKeyPressed += Keyboard_OnKeyPressed;
        }

        private void BtnRegister_LeftClick(object? sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = CnCNetAPI.API_REGISTER_URL,
                UseShellExecute = true
            });
        }

        private void Keyboard_OnKeyPressed(object? sender, KeyPressEventArgs e)
        {
            if (Enabled)
            {
                switch (e.PressedKey)
                {
                    case Keys.Enter:
                        BtnLogin_LeftClick(this, EventArgs.Empty);
                        break;
                    case Keys.Tab:
                        WindowManager.SelectedControl = tbPlayerEmail.IsActive ? (XNAControl)tbPlayerPassword : tbPlayerEmail;
                        break;
                }
            }
        }

        private void BtnCancel_LeftClick(object? sender, EventArgs e)
        {
            Cancel?.Invoke(this, EventArgs.Empty);
            Disable();
        }

        private void BtnLogin_LeftClick(object? sender, EventArgs e)
        {
            if (!btnLogin.Enabled)
                return;

            // Disable immediately to prevent re-entrant clicks during validation
            btnLogin.Enabled = false;

            if (string.IsNullOrEmpty(tbPlayerEmail.Text) || string.IsNullOrEmpty(tbPlayerPassword.Password))
            {
                lblError.Text = "Email and password are required.".L10N("Client:CnCNet:LoginFieldsRequired");
                btnLogin.Enabled = true;
                return;
            }

            lblError.Text = string.Empty;

            // Capture credentials before switching to the background thread
            string email = tbPlayerEmail.Text;
            string password = tbPlayerPassword.Password;

            // The checkbox only exists in the CnCNet API flow; default to true if unavailable.
            bool stayLoggedIn = chkStayLoggedIn?.Checked ?? true;

            _ = Task.Run(async () =>
            {
                bool success = await CnCNetAPI.Instance.LoginAsync(email, password, stayLoggedIn);

                AddCallback(new Action(() =>
                {
                    btnLogin.Enabled = true;
                    if (success)
                        LoginSuccess?.Invoke(this, EventArgs.Empty);
                    else
                        lblError.Text = CnCNetAPI.Instance.ErrorMessage ?? "Login failed.".L10N("Client:CnCNet:LoginFailed");
                }));
            });
        }
    }
}
