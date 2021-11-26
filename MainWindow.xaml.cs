using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using MicroPods.service;
using System.Threading;

namespace MicroPods
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private PordService ps;
        private static readonly object Lock = new object();
        private Dictionary<string, Object> status;



        public delegate void UpdateUIDelegate(Dictionary<string, Object> status);
        public UpdateUIDelegate updateUIDelegate;

        public MainWindow()
        {
            InitializeComponent();
            
            Init();
        }

        public void Init()
        {
            this.ps = new PordService();
            var that = this;
            this.ps.updateUIDelegate += (status) =>
            {
                that.status = status;
            };

            this.updateUIDelegate += (status) =>
            {
                int validTime = int.Parse(status.GetValueOrDefault("validTime", -1).ToString());
                bool valid = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() < validTime;
                that.valid.Content = valid ? "已连接" : "未连接";
                that.leftStatus.Content = getBatteryStatus(status, "leftStatus", 0) + "0%";
                that.rightStatus.Content = getBatteryStatus(status, "rightStatus", 0) + "0%";
                that.caseStatus.Content = getBatteryStatus(status, "caseStatus", 0) + "0%";

            };

            new Thread(() =>
            {
                while (true)
                {

                    if(status != null)
                    {
                        this.Dispatcher.Invoke(updateUIDelegate, status);
                        
                    }

                    Thread.Sleep(500);//睡眠500毫秒，也就是0.5秒
                }

            }).Start();
        }



        private void DrawOver(object sender, DragEventArgs e)
        {
            this.DragMove();
        }

        private void Drag(object sender, MouseEventArgs e)
        {
            //判断鼠标是否拖动窗口
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //执行移动方法
                this.DragMove();
            }
        }


        private void minimizeClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void closeClick(object sender, RoutedEventArgs e)
        {
            //终止所有线程 
            Environment.Exit(Environment.ExitCode);
        }

        private int hexStrToInt(string hexStr)
        {
            return Int32.Parse(hexStr, System.Globalization.NumberStyles.HexNumber);
        }

        private int getBatteryStatus(Dictionary<string, Object> status, string key, int defaultValue)
        {
            string v = status.GetValueOrDefault(key, defaultValue).ToString();
            return hexStrToInt(v);
        }
    }
}
