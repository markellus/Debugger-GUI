using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Debugger.Source
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static App Instance { get; private set; }

        [STAThread]
        static void Main()
        {
            Instance = new App();
            Instance.InitializeComponent();
            Instance.Run();
        }
    }
}
