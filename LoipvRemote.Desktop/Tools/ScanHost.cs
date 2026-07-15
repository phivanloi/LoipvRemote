using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.Versioning;
using LoipvRemote.Messages;
using LoipvRemote.Resources.Language;


namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class ScanHost(string host)
    {
        #region Properties

        public static int SshPort { get; set; } = 22;
        public static int TelnetPort { get; set; } = 23;
        public static int HttpPort { get; set; } = 80;
        public static int HttpsPort { get; set; } = 443;
        public static int RloginPort { get; set; } = 513;
        public static int RdpPort { get; set; } = 3389;
        public static int VncPort { get; set; } = 5900;
        public ArrayList OpenPorts { get; set; } = [];
        public ArrayList ClosedPorts { get; set; } = [];
        public bool Rdp { get; set; }
        public bool Vnc { get; set; }
        public bool Ssh { get; set; }
        public bool Telnet { get; set; }
        public bool Rlogin { get; set; }
        public bool Http { get; set; }
        public bool Https { get; set; }
        public string HostIp { get; set; } = host;
        public string HostName { get; set; } = "";

        public string HostNameWithoutDomain
        {
            get
            {
                if (string.IsNullOrEmpty(HostName) || HostName == HostIp)
                {
                    return HostIp;
                }

                return HostName.Split('.')[0];
            }
        }

        #endregion
        #region Methods

        public override string ToString()
        {
            try
            {
                return "SSH: " + Convert.ToString(Ssh) + " Telnet: " + Convert.ToString(Telnet) + " HTTP: " +
                       Convert.ToString(Http) + " HTTPS: " + Convert.ToString(Https) + " Rlogin: " +
                       Convert.ToString(Rlogin) + " RDP: " + Convert.ToString(Rdp) + " VNC: " + Convert.ToString(Vnc);
            }
            catch (Exception)
            {
                Trace.TraceWarning("ToString failed (Tools.PortScan)");
                return "";
            }
        }

        //Adpating to objectlistview instaed of listview
        public string HostIPorName
        {
            get
            {
                if (string.IsNullOrEmpty(HostName))
                    return HostIp;
                else
                    return HostName;
            }
        }

        public string RdpName => BoolToYesNo(Rdp);

        public string VncName => BoolToYesNo(Vnc);

        public string SshName => BoolToYesNo(Rdp);

        public string TelnetName => BoolToYesNo(Telnet);

        public string RloginName => BoolToYesNo(Rlogin);

        public string HttpName => BoolToYesNo(Http);

        public string HttpsName => BoolToYesNo(Https);

        public string OpenPortsName
        {
            get
            {
                string strOpen = "";
                foreach (int p in OpenPorts)
                {
                    strOpen += p + ", ";
                }

                return strOpen;
            }
        }

        public string ClosedPortsName
        {
            get
            {
                string strClosed = "";
                foreach (int p in ClosedPorts)
                {
                    strClosed += p + ", ";
                }

                return strClosed;
            }
        }


        private static string BoolToYesNo(bool value)
        {
            return value ? Language.Yes : Language.No;
        }

        public void SetAllProtocols(bool value)
        {
            Vnc = value;
            Telnet = value;
            Ssh = value;
            Rlogin = value;
            Rdp = value;
            Https = value;
            Http = value;
        }

        #endregion
    }
}
