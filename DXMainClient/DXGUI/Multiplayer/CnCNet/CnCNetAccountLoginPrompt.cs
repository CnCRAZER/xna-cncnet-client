using ClientCore.Extensions;
using ClientGUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rampastring.XNAUI;
using Rampastring.XNAUI.Input;
using Rampastring.XNAUI.XNAControls;
using System;

#nullable enable

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    public class CnCNetAccountLoginPrompt : XNAWindow
    {
        public event EventHandler? ConnectAsGuest;
        public event EventHandler? ConnectWithAccount;
        public event EventHandler? Logout;

        private XNAClientButton btnLogout = null!;

        public CnCNetAccountLoginPrompt(WindowManager windowManager) : base(windowManager) { }

        public override void Initialize()
        {
            Name = nameof(CnCNetAccountLoginPrompt);
            BackgroundTexture = AssetLoader.LoadTextureUncached("logindialogbg.png");
            ClientRectangle = new Rectangle(0, 0, 350, 150);

            var lblWindowTitle = new XNALabel(WindowManager)
            {
                Name = "lblWindowTitle",
                FontIndex = 1,
                Text = "CONNECT TO CNCNET".L10N("Client:CnCNet:ConnectToCnCNetTitle")
            };
            AddChild(lblWindowTitle);
            lblWindowTitle.CenterOnParent();
            lblWindowTitle.ClientRectangle = new Rectangle(lblWindowTitle.X, 12, lblWindowTitle.Width, lblWindowTitle.Height);

            var lblWindowDescription = new XNALabel(WindowManager)
            {
                Name = "lblWindowDescription",
                FontIndex = 1,
                Text = "Choose how you would like to connect to CnCNet".L10N("Client:CnCNet:ConnectToCnCNetDescription")
            };
            AddChild(lblWindowDescription);
            lblWindowDescription.CenterOnParent();
            lblWindowDescription.ClientRectangle = new Rectangle(lblWindowDescription.X, 50, lblWindowDescription.Width, lblWindowDescription.Height);

            var btnConnectAsGuest = new XNAClientButton(WindowManager)
            {
                Name = "btnConnectAsGuest",
                ClientRectangle = new Rectangle(12, ClientRectangle.Bottom - 35, 133, 23),
                Text = "As a Guest".L10N("Client:CnCNet:ConnectAsGuest")
            };
            btnConnectAsGuest.LeftClick += BtnConnectAsGuest_LeftClick;

            btnLogout = new XNAClientButton(WindowManager)
            {
                Name = "btnLogout",
                ClientRectangle = new Rectangle((Width - 60) / 2, btnConnectAsGuest.Y, 60, 23),
                Text = "Logout".L10N("Client:CnCNet:LogoutButton"),
                Visible = false,
                Enabled = false
            };
            btnLogout.LeftClick += BtnLogout_LeftClick;

            var btnLoginWithAccount = new XNAClientButton(WindowManager)
            {
                Name = "btnLoginWithAccount",
                ClientRectangle = new Rectangle(Width - 140, btnConnectAsGuest.Y, 133, 23),
                Text = "With my Account".L10N("Client:CnCNet:ConnectWithAccount")
            };
            btnLoginWithAccount.LeftClick += BtnLoginWithAccount_LeftClick;

            AddChild(btnConnectAsGuest);
            AddChild(btnLogout);
            AddChild(btnLoginWithAccount);

            base.Initialize();
            CenterOnParent();
            Keyboard.OnKeyPressed += Keyboard_OnKeyPressed;
        }

        private void Keyboard_OnKeyPressed(object? sender, KeyPressEventArgs e)
        {
            if (Enabled && e.PressedKey == Keys.Enter)
                BtnConnectAsGuest_LeftClick(this, EventArgs.Empty);
        }

        private void BtnLoginWithAccount_LeftClick(object? sender, EventArgs e)
        {
            ConnectWithAccount?.Invoke(this, EventArgs.Empty);
            Disable();
        }

        public void SetLogoutVisibility(bool visible)
        {
            btnLogout.Visible = visible;
            btnLogout.Enabled = visible;
        }

        private void BtnLogout_LeftClick(object? sender, EventArgs e)
        {
            Logout?.Invoke(this, EventArgs.Empty);
        }

        private void BtnConnectAsGuest_LeftClick(object? sender, EventArgs e)
        {
            ConnectAsGuest?.Invoke(this, EventArgs.Empty);
            Disable();
        }
    }
}
