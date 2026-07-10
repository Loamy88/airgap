using System;

namespace AIRGAP.Shared.Netcode
{
    public enum PlayerRole : byte
    {
        None = 0,
        Infiltrator = 1,
        Warden = 2
    }

    public enum StartMode
    {
        Interactive,
        Host,
        Client
    }

    /// <summary>
    /// Session-level configuration resolved once at startup, either from the
    /// host-or-join screen or from command-line args (headless/CI):
    ///   -airgap-role host|client      auto-start in that network mode
    ///   -airgap-ip 192.168.x.x        server address (client) — default 127.0.0.1
    ///   -airgap-port 7777             UDP port — default 7777
    ///   -airgap-playerrole infiltrator|warden   host's picked game role — default infiltrator
    ///   -airgap-smoke                 CI smoke mode: ping round-trip then auto-quit
    /// </summary>
    public static class SessionConfig
    {
        public const ushort DefaultPort = 7777;

        public static StartMode Mode = StartMode.Interactive;
        public static string ServerAddress = "127.0.0.1";
        public static ushort Port = DefaultPort;
        public static PlayerRole DesiredRole = PlayerRole.Infiltrator;
        public static bool SmokeMode;

        public static PlayerRole Complement(PlayerRole role) =>
            role == PlayerRole.Infiltrator ? PlayerRole.Warden : PlayerRole.Infiltrator;

        public static void ParseCommandLine(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-airgap-role":
                        if (i + 1 < args.Length)
                        {
                            Mode = args[++i].Equals("host", StringComparison.OrdinalIgnoreCase)
                                ? StartMode.Host
                                : StartMode.Client;
                        }
                        break;
                    case "-airgap-ip":
                        if (i + 1 < args.Length) ServerAddress = args[++i];
                        break;
                    case "-airgap-port":
                        if (i + 1 < args.Length && ushort.TryParse(args[++i], out ushort port)) Port = port;
                        break;
                    case "-airgap-playerrole":
                        if (i + 1 < args.Length)
                        {
                            DesiredRole = args[++i].Equals("warden", StringComparison.OrdinalIgnoreCase)
                                ? PlayerRole.Warden
                                : PlayerRole.Infiltrator;
                        }
                        break;
                    case "-airgap-smoke":
                        SmokeMode = true;
                        break;
                }
            }
        }
    }
}
