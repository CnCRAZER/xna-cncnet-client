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
using System.Threading.Tasks;

#nullable enable

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class CnCNetAccountManagerWindow : XNAWindow
    {
        public event EventHandler? Logout;
        public event EventHandler? Connect;

        private XNADropDown ddAccounts = null!;
        private XNATextBox tbNewNickname = null!;
        private XNAClientButton btnCreate = null!;
        private XNALabel lblTitle = null!;
        private XNALabel lblAccounts = null!;
        private XNALabel lblError = null!;
        private XNAClientButton btnConnect = null!;

        public CnCNetAccountManagerWindow(WindowManager windowManager) : base(windowManager) { }

        public override void Initialize()
        {
            Name = nameof(CnCNetAccountManagerWindow);
            BackgroundTexture = AssetLoader.LoadTextureUncached("logindialogbg.png");
            ClientRectangle = new Rectangle(0, 0, 400, 200);

            lblTitle = new XNALabel(WindowManager)
            {
                Name = "lblTitle",
                FontIndex = 1,
                Text = "YOUR NICKNAMES".L10N("Client:CnCNet:YourNicknamesTitle")
            };
            lblTitle.ClientRectangle = new Rectangle(12, 12, lblTitle.Width, lblTitle.Height);
            AddChild(lblTitle);

            btnConnect = new XNAClientButton(WindowManager)
            {
                Name = "btnConnect",
                ClientRectangle = new Rectangle(12, ClientRectangle.Bottom - 35, 92, 23),
                Text = "Connect".L10N("Client:CnCNet:ConnectButton")
            };
            btnConnect.LeftClick += BtnConnect_LeftClick;

            var btnLogout = new XNAClientButton(WindowManager)
            {
                Name = "btnLogout",
                ClientRectangle = new Rectangle(Width - 104, btnConnect.Y, 92, 23),
                Text = "Logout".L10N("Client:CnCNet:LogoutButton")
            };
            btnLogout.LeftClick += BtnLogout_LeftClick;

            ddAccounts = new XNADropDown(WindowManager)
            {
                Name = "ddAccounts",
                Text = "Accounts",
                ClientRectangle = new Rectangle(100, ClientRectangle.Y + 50, 200, 19)
            };

            lblAccounts = new XNALabel(WindowManager)
            {
                Name = "lblAccounts",
                FontIndex = 1,
                Text = "Nicknames:".L10N("Client:CnCNet:NicknamesLabel")
            };
            lblAccounts.ClientRectangle = new Rectangle(12, ddAccounts.ClientRectangle.Y + 1, lblAccounts.ClientRectangle.Width, lblAccounts.ClientRectangle.Height);

            tbNewNickname = new XNATextBox(WindowManager)
            {
                Name = "tbNewNickname",
                ClientRectangle = new Rectangle(100, ClientRectangle.Y + 50, 200, 21),
                MaximumTextLength = 16
            };

            btnCreate = new XNAClientButton(WindowManager)
            {
                Name = "btnCreate",
                ClientRectangle = new Rectangle(12, ClientRectangle.Bottom - 35, 92, 23),
                Text = "Create".L10N("Client:CnCNet:CreateButton")
            };
            btnCreate.LeftClick += BtnCreate_LeftClick;

            lblError = new XNALabel(WindowManager)
            {
                Name = "lblError",
                ClientRectangle = new Rectangle(12, ClientRectangle.Y + 80, Width - 24, 40),
                TextColor = Color.Red
            };

            AddChild(ddAccounts);
            AddChild(tbNewNickname);
            AddChild(lblAccounts);
            AddChild(btnConnect);
            AddChild(btnCreate);
            AddChild(btnLogout);
            AddChild(lblError);

            base.Initialize();
            CenterOnParent();
            Keyboard.OnKeyPressed += Keyboard_OnKeyPressed;

            CnCNetAPI.Instance.AccountUpdated += CnCNetAuthApi_AccountUpdated;
        }

        private void CnCNetAuthApi_AccountUpdated(object? sender, EventArgs e)
        {
            // AccountUpdated may fire on a background thread; marshal to the UI thread.
            AddCallback(new Action(PopulateAccountList));
        }

        private void Keyboard_OnKeyPressed(object? sender, KeyPressEventArgs e)
        {
            if (!Enabled || e.PressedKey != Keys.Enter)
                return;

            if (btnCreate.Visible)
                BtnCreate_LeftClick(this, EventArgs.Empty);
            else
                BtnConnect_LeftClick(this, EventArgs.Empty);
        }

        private void BtnLogout_LeftClick(object? sender, EventArgs e)
        {
            CnCNetAPI.Instance.Logout();
            Logout?.Invoke(this, EventArgs.Empty);
            Disable();
        }

        private void BtnConnect_LeftClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(ddAccounts.SelectedItem?.Text))
                return;

            string nickname = ddAccounts.SelectedItem.Text;

            NameValidationError validationError = NameValidator.IsNameValid(nickname, out string errorMessage);
            if (validationError != NameValidationError.None)
            {
                XNAMessageBox.Show(WindowManager, "Invalid Player Name", errorMessage);
                return;
            }

            ProgramConstants.PLAYERNAME = nickname;
            UserINISettings.Instance.PlayerName.Value = ProgramConstants.PLAYERNAME;
            UserINISettings.Instance.SaveSettings();

            Connect?.Invoke(this, EventArgs.Empty);
        }

        private void BtnCreate_LeftClick(object? sender, EventArgs e)
        {
            if (!btnCreate.Enabled)
                return;

            // Disable immediately to prevent re-entrant clicks during validation
            btnCreate.Enabled = false;

            lblError.Text = string.Empty;
            string nickname = tbNewNickname.Text.Trim();

            if (string.IsNullOrEmpty(nickname))
            {
                btnCreate.Enabled = true;
                return;
            }

            NameValidationError validationError = NameValidator.IsNameValid(nickname, out string errorMessage);
            if (validationError != NameValidationError.None)
            {
                lblError.Text = errorMessage;
                btnCreate.Enabled = true;
                return;
            }

            _ = Task.Run(async () =>
            {
                bool success = await CnCNetAPI.Instance.CreatePlayerAsync(nickname);

                AddCallback(new Action(() =>
                {
                    btnCreate.Enabled = true;
                    if (!success)
                        lblError.Text = CnCNetAPI.Instance.ErrorMessage ?? "Failed to create nickname.".L10N("Client:CnCNet:CreateNicknameFailed");
                    else
                        tbNewNickname.Text = string.Empty;
                }));
            });
        }

        private void PopulateAccountList()
        {
            ddAccounts.Items.Clear();
            string ladderAbbrev = ClientConfiguration.Instance.CnCNetLadderAbbrev;

            foreach (AuthPlayer player in CnCNetAPI.Instance.Accounts)
            {
                if (player.Ladder == null ||
                    !string.Equals(player.Ladder.Abbreviation, ladderAbbrev, StringComparison.OrdinalIgnoreCase) ||
                    player.Username == null)
                {
                    continue;
                }

                ddAccounts.AddItem(new XNADropDownItem { Text = player.Username });
            }

            bool hasAccounts = ddAccounts.Items.Count > 0;

            if (hasAccounts)
                ddAccounts.SelectedIndex = 0;

            SetMode(hasAccounts);
        }

        private void SetMode(bool selectMode)
        {
            lblError.Text = string.Empty;

            // Select existing nickname mode
            ddAccounts.Visible = selectMode;
            ddAccounts.Enabled = selectMode;
            btnConnect.Visible = selectMode;
            btnConnect.Enabled = selectMode;

            // Create new nickname mode
            tbNewNickname.Visible = !selectMode;
            tbNewNickname.Enabled = !selectMode;
            btnCreate.Visible = !selectMode;
            btnCreate.Enabled = !selectMode;

            lblTitle.Text = selectMode
                ? "YOUR NICKNAMES".L10N("Client:CnCNet:YourNicknamesTitle")
                : "CREATE A NICKNAME".L10N("Client:CnCNet:CreateNicknameTitle");

            lblAccounts.Text = selectMode
                ? "Nicknames:".L10N("Client:CnCNet:NicknamesLabel")
                : "Nickname:".L10N("Client:CnCNet:NicknameLabel");
        }
    }
}
