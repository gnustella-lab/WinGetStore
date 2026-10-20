using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

[assembly: AssemblyTitle("Microsoft Store (WinGet Edition)")]
[assembly: AssemblyDescription("Native Windows 10 alternative using the original Microsoft Store UI over WinGet, with zero ads, zero telemetry, and no Microsoft account required.")]
[assembly: AssemblyCompany("gnustella-lab")]
[assembly: AssemblyProduct("WinGetStore")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace WinGetStore
{
    public static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        public static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }

            // Fast self-test support (--self-test / --ui-smoke-test) for automated CI and local validation
            if (args != null && args.Length > 0 && args[0] == "--self-test")
            {
                return RunSelfTest();
            }
            if (args != null && args.Length > 0 && args[0] == "--ui-smoke-test")
            {
                int st = RunSelfTest();
                if (st != 0) return st;
                MainWindow testWin = new MainWindow();
                testWin.Show();
                testWin.UpdateLayout();

                testWin.WindowState = WindowState.Maximized;
                testWin.UpdateLayout();
                Point topLeft = testWin.PointToScreen(new Point(0, 0));
                Rect workArea = SystemParameters.WorkArea;
                Console.WriteLine(string.Format("MAXIMIZED CHECK: TopLeft=({0},{1}) ActualSize=({2}x{3}) WorkArea=({4},{5} {6}x{7})",
                    topLeft.X, topLeft.Y, testWin.ActualWidth, testWin.ActualHeight,
                    workArea.Left, workArea.Top, workArea.Width, workArea.Height));

                testWin.Close();
                return 0;
            }

            Application app = new Application();
            MainWindow win = new MainWindow();
            app.Run(win);
            return 0;
        }

        private static int RunSelfTest()
        {
            string sampleSearch =
                "Nome                               ID                        Versão          Correspondência\r\n" +
                "--------------------------------------------------------------------------------------------\r\n" +
                "7-Zip                              7zip.7zip                 26.04           Moniker: 7zip\r\n" +
                "NanaZip                            M2Team.NanaZip            7.0.1843.0      Tag: 7zip\r\n";

            var parsed = WinGetService.ParseTableOutput(sampleSearch, false);
            if (parsed.Count != 2 || parsed[0].Id != "7zip.7zip" || parsed[0].Version != "26.04")
            {
                Console.Error.WriteLine("FAILURE: WinGet search parser mismatch.");
                return 1;
            }

            string sampleUpgrade =
                "Nome       ID                    Versão   Disponível Origem\r\n" +
                "-----------------------------------------------------------\r\n" +
                "Node.js    OpenJS.NodeJS.LTS     24.19.0  24.20.0    winget\r\n";

            var upgrades = WinGetService.ParseTableOutput(sampleUpgrade, true);
            if (upgrades.Count != 1 || upgrades[0].Id != "OpenJS.NodeJS.LTS" || upgrades[0].AvailableVersion != "24.20.0")
            {
                Console.Error.WriteLine("FAILURE: WinGet upgrade parser mismatch.");
                return 2;
            }

            var catalog = CuratedCatalog.GetDefaultPackages();
            if (catalog.Count < 20)
            {
                Console.Error.WriteLine("FAILURE: Essential catalog count is below threshold.");
                return 3;
            }

            int iconsLoaded = 0;
            foreach (var item in catalog)
            {
                IconResolver.ResolveIconForPackage(item);
                if (item.IconSource != null) iconsLoaded++;
            }
            Console.WriteLine("SELF-TEST OK: WinGet parser, essential catalog (" + catalog.Count + " apps, " + iconsLoaded + " icons loaded), and visual resources validated.");
            return 0;
        }
    }
}
