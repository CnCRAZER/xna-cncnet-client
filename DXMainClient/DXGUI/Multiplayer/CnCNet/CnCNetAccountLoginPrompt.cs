﻿using ClientGUI;
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
                Text = "CONNECT TO CNCNET"
            };
            AddChild(lblWindowTitle);
            lblWindowTitle.CenterOnParent();
            lblWindowTitle.ClientRectangle = new Rectangle(lblWindowTitle.X, 12, lblWindowTitle.Width, lblWindowTitle.Height);

            var lblWindowDescription = new XNALabel(WindowManager)
            {
                Name = "lblWindowDescription",
                FontIndex = 1,
                Text = "Choose how you would like to connect to CnCNet"
            };
            AddChild(lblWindowDescription);
            lblWindowDescription.CenterOnParent();
            lblWindowDescription.ClientRectangle = new Rectangle(lblWindowDescription.X, 50, lblWindowDescription.Width, lblWindowDescription.Height);

            var btnConnectAsGuest = new XNAClientButton(WindowManager)
            {
                Name = "btnConnectAsGuest",
                ClientRectangle = new Rectangle(12, ClientRectangle.Bottom - 35, 133, 23),
                Text = "As a Guest"
            };
            btnConnectAsGuest.LeftClick += BtnConnectAsGuest_LeftClick;

            var btnLoginWithAccount = new XNAClientButton(WindowManager)
            {
                Name = "btnLoginWithAccount",
                ClientRectangle = new Rectangle(Width - 140, btnConnectAsGuest.Y, 133, 23),
                Text = "With my Account"
            };
            btnLoginWithAccount.LeftClick += BtnLoginWithAccount_LeftClick;

            AddChild(btnConnectAsGuest);
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

        private void BtnConnectAsGuest_LeftClick(object? sender, EventArgs e)
        {
            ConnectAsGuest?.Invoke(this, EventArgs.Empty);
            Disable();
        }
    }
}
