using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace testandroid
{
    internal class Program
    {
        static void Main(string[] args)
        {
            AdbServer server = new AdbServer();
            var result = server.StartServer(@"C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe", restartServerIfNewer: false);
            Console.WriteLine("adb 服务启动，开始监听设备");
            var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            monitor.DeviceConnected += OnDeviceConnected;
            monitor.Start();

            Console.ReadLine();
        }

        static void OnDeviceConnected(object sender, DeviceDataEventArgs e)
        {
            AdbClient client = new AdbClient();
            var devices = client.GetDevices();
            var device = devices.Find(a => a.Serial == e.Device.Serial);
            Console.WriteLine($"{device.Model} 已经连接PC");

            var receiver = new ConsoleOutputReceiver();
            PackageManager manager = new PackageManager(client, device);
            if (!manager.Packages.ContainsKey("com.tencent.android.qqdownloader"))
            {
                Console.WriteLine($"未找到应用宝，开始安装应用");
                string apkpath = AppDomain.CurrentDomain.BaseDirectory + "MobileAssistant_1.apk";
                manager.InstallPackage(apkpath, reinstall: false);
                Console.WriteLine($"应用宝安装完成");
            }
            else
            {
                Console.WriteLine($"设备已安装应用宝，跳过安装");
            }
            Thread.Sleep(3000);
            Console.WriteLine("打开应用宝");
            client.ExecuteRemoteCommand("am start -n com.tencent.android.qqdownloader/com.tencent.assistantv2.activity.MainActivity", device, receiver);
            Thread.Sleep(3000);
            Console.WriteLine("点击游戏频道");
            client.ExecuteRemoteCommand("input tap 325 2300", device, receiver);
            Thread.Sleep(3000);
            Console.WriteLine("点击榜单");
            client.ExecuteRemoteCommand("input tap 330 360", device, receiver);
            Thread.Sleep(3000);
            Dictionary<string, string> rank = new Dictionary<string, string>();
            string filepath = "C:\\Users\\liuyang\\Documents\\GitProject\\testandroid\\testandroid\\ui.xml";
            do
            {
                Console.WriteLine("解析页面元素");
                client.ExecuteRemoteCommand("uiautomator dump /sdcard/ui.xml", device, receiver);

                using (SyncService service = new SyncService(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), device))
                using (Stream stream = File.Create(filepath))
                {
                    service.Pull("/sdcard/ui.xml", stream, null, CancellationToken.None);
                }

                XmlDocument doc = new XmlDocument();
                doc.Load(filepath);
                var endnode = doc.SelectSingleNode("//*[@resource-id='com.tencent.android.qqdownloader:id/ahk']");


                var namelist = doc.SelectNodes("//*[@resource-id='com.tencent.android.qqdownloader:id/ki']");
                var seqlist = doc.SelectNodes("//*[@resource-id='com.tencent.android.qqdownloader:id/ox']");
                if (namelist.Count == seqlist.Count)
                {
                    for (int i = 0; i < namelist.Count; i++)
                    {

                        string gamename = namelist[i].Attributes["text"].Value;
                        string seq = seqlist[i].Attributes["text"].Value;
                        if (!rank.ContainsKey(seq))
                        {
                            Console.WriteLine($"排名{seq}：{gamename}");
                            rank.Add(seq, gamename);
                        }
                    }
                }
                if (endnode != null && endnode.Attributes["text"].Value.Contains("没有更多内容"))
                {
                    Console.WriteLine("已获取所有数据");
                    break;
                }
                client.ExecuteRemoteCommand("input swipe 750 1250 750 450", device, receiver);
                Thread.Sleep(200);
            } while (true);

        }
    }
}
