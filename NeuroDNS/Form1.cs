using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MetroFramework.Forms;
using ARSoft.Tools.Net.Dns;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NeuroDNS
{
    public partial class Form1 : MetroForm
    {
        public Form1()
        {
            InitializeComponent();
        }

        List<Book> books;

        

        private void Form1_Load(object sender, EventArgs e)
        {
            LoadDNS();
            StartServer();
        }

        private DnsServer server;
        void StartServer()
        {
            server = new DnsServer(IPAddress.Any, 10, 10, ProcessQuery);
            server.Start();
        }

        Stack<string> cons = new Stack<string>();

        void WriteToConsole(string s)
        {
            cons.Push(s);
        }
        List<Book> templ=new List<Book>();
        List<Book> enb = null;
        int retcnt = 5;
        int repcnt = 0;

        private void InitDNS()
        {
            var ssf = from sd in books where sd.Enabled == true select sd;
            enb = ssf.ToList<Book>();
            if (repcnt < enb.Count)
            {
                repcnt = enb.Count;
            }
        }

        DnsMessageBase ProcessQuery(DnsMessageBase message, IPAddress clientAddress, ProtocolType protocol)
        {
            message.IsQuery = false;

            DnsMessage query = message as DnsMessage;

        ret1: repcnt++; 
            if ((query != null) && (query.Questions.Count == 1))
            {
                // send query to upstream server
                DnsQuestion question = query.Questions[0];
                Random rnd = new Random();
                if (enb==null )
                {
                    InitDNS();
                }
                
                WriteToConsole(question.Name);
                templ.Clear();
                for (int i = 0; i < retcnt; i++)
                {
                    templ.Add(WeightedRandomization.Choose(enb));
                }

                System.Threading.Tasks.Parallel.ForEach(templ, (site, state) =>
                {
                    WriteToConsole("Get Info from: " + site.IP);
                    DnsClient cd = new DnsClient(IPAddress.Parse(site.IP ), 1000);
                    DnsMessage answer = cd.Resolve(question.Name, question.RecordType, question.RecordClass);
                    if (answer != null)
                    {
                        foreach (DnsRecordBase record in (answer.AnswerRecords))
                        {
                            lock (query)
                            {
                                query.AnswerRecords.Add(record);
                            }
                            site.Selects++;
                            WriteToConsole(record.Name);
                        }
                        foreach (DnsRecordBase record in (answer.AdditionalRecords))
                        {
                            lock (query)
                            {
                                query.AnswerRecords.Add(record);
                            }
                            site.Selects++;
                            WriteToConsole(record.Name);
                        }
                        lock (query)
                        {
                            site.Weight--;
                            query.ReturnCode = ReturnCode.NoError;
                            state.Break();
                        }
                    }
                });
                // if got an answer, copy it to the message sent to the client
            }
            if (query.ReturnCode == ReturnCode.NoError)
            {
                return query;
            }
            if (repcnt>5)
            {
                message.ReturnCode = ReturnCode.ServerFailure;
                return message;
            }
            else
            {
                goto ret1;
            }
        }


        [Serializable]
        public class WeightedRandomization
        {
            public static T Choose<T>(List<T> list) where T : IWeighted
            {
                if (list.Count == 0)
                {
                    return default(T);
                }

                int totalweight = list.Sum(c => c.Weight);
                Random rand = new Random();
                int choice = rand.Next(totalweight);
                int sum = 0;

                foreach (var obj in list)
                {
                    for (int i = sum; i < obj.Weight + sum; i++)
                    {
                        if (i >= choice)
                        {
                            return obj;
                        }
                    }
                    sum += obj.Weight;
                }

                return list.First();
            }
        }

        public interface IWeighted
        {
            int Weight { get; set; }
        }

        [Serializable]
        public class Book : IWeighted
        {
            //public int Isbn { get; set; }
            public string Name { get; set; }
            public string IP { get; set; }
            public string Info { get; set; }
            public int Weight { get; set; }
            public int Selects { get; set; }
            public List<int> AvgTime { get; set; }
            public bool Enabled { get; set; }
            public bool BlockEnabled { get; set; }
        }

        void SaveDNS()
        {
            //создаем объект который будет сериализован
            //откроем поток для записи в файл
            FileStream fs = new FileStream("DNS.db", FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            BinaryFormatter bf = new BinaryFormatter();
            //сериализация
            bf.Serialize(fs, books );
            fs.Close();
        }
        void LoadDNS()
        {
            if (File.Exists("DNS.db"))
            {
                FileStream fs = new FileStream("DNS.db", FileMode.Open, FileAccess.Read, FileShare.Read);
                BinaryFormatter bf = new BinaryFormatter();
                books = (List<Book>)bf.Deserialize(fs);
                fs.Close();
                ReBind();
            }
            else
            {
                books = new List<Book> {
                new Book{Name="A",Weight=1},
                new Book{Name="B",Weight=2},
                new Book{Name="C",Weight=3},
                new Book{Name="D",Weight=4},
                new Book{Name="E",Weight=5}};
                SaveDNS();
                ReBind();
            }
        }

        void ReBind()
        {
            BindingSource bs = new BindingSource();
            bs.DataSource = books;
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.DataSource = bs;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
           
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
           
            SaveDNS();
            if (!exit)
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    this.Visible = false;
                }
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Random rnd = new Random(DateTime.Now.Millisecond);
            foreach (var item in books )
            {
                item.IP = item.IP.Replace(" ", "");
                
                item.Selects = 0;
                if (item.Weight==0)
                {
                    item.Weight = 500 - getping(item.IP);
                    if (item.Weight<0)
                    {
                        item.Weight = 0;
                    }
                }
               
                dataGridView1.Refresh();
            }
            InitDNS();
        }

        int getping(string ip)
        {
            List<long> rt = new List<long>();
            var timeout = 1000;
            var buffer = new byte[] { 0, 0, 0, 0 };
            // создаем и отправляем ICMP request
            var ping = new Ping();
            for (int i = 0; i < 1; i++)
            {
                var reply = ping.Send(ip, timeout, buffer, new PingOptions { Ttl = 128 });
                if (reply.Status == IPStatus.Success)
                {
                    rt.Add(reply.RoundtripTime);
                }
                Application.DoEvents();
                Thread.Sleep(5);
            }
            if (rt.Count > 0)
            {
                double iq = rt.Average();
                iq = Math.Ceiling(iq);
                return (int) iq;
            }
            else
            {
                return 1000;
            }

            // если ответ успешен
        }

        private string ss = "";
        private void timer1_Tick_1(object sender, EventArgs e)
        {
            while (cons.Count>0)
            {
                ss = (string) cons.Pop();
                if (ss!=null )
                {
                    if (ss.Length > 5)
                    {
                        listBox1.Items.Add(ss);
                        if (listBox1.Items.Count > 60)
                        {
                            listBox1.Items.RemoveAt(0);
                        }
                        listBox1.SelectedIndex = listBox1.Items.Count - 1;
                    }
                }
                
                
            }
            
        }

        private bool exit = false;
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            exit = true;
            this.Close();
        }

        private void showToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Visible = true;
        }
    }
}
