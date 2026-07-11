using System;
using System.Runtime.Versioning;
using LoipvRemote.App.Info;

namespace LoipvRemote.UI.Forms
{
    [SupportedOSPlatform("windows")]
    /// <summary>
    /// Interaction logic for FrmSplashScreenNew.xaml
    /// </summary>
    public partial class FrmSplashScreenNew
    {
        static FrmSplashScreenNew instance = null;
        public FrmSplashScreenNew()
        {
            InitializeComponent();
            lblVersion.Text = $@"Phiên bản {GeneralAppInfo.ApplicationVersion}";
        }
        public static FrmSplashScreenNew GetInstance()
        {
            //instance == null
            instance ??= new FrmSplashScreenNew();
            return instance;
        }

    }
}
