using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ARSoft.Tools.Net.Dns;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;


namespace ARSoft.Tools.Net
{
    class Program
    {
        static void Main(string[] args)
        {
            using (DnsServer server = new DnsServer(IPAddress.Any, 10, 10, ProcessQuery))
            {
                server.Start();

                Console.WriteLine("Press any key to stop server");
                Console.ReadLine();
            }
        }


//set dns netsh interface ip set dns "Подключение по локальной сети" static первый_DNS-сервер


    //Rejector
    //    176.9.147.55
    //    95.154.128.32

    //Базовый:
    //    77.88.8.88
    //    77.88.8.2

    //Безопасный:
    //    77.88.8.7
    //    77.88.8.3

    //Семейный
    //    77.88.8.8
    //    77.88.8.1

    //SkyDNS
    //    193.58.251.251

    //OpenDNS
    //    208.67.222.222
    //    208.67.220.220

//        NortonDNS
//1. Security (198.153.192.40 и 198.153.194.40) - это политика блокирует все сайты на которых размещены вредоносные программы, блокирует фишинговые и мошеннические сайты. 

//2. Security + Pornography (198.153.192.50 и 198.153.194.50) - в дополнение к блокировке небезопасных сайтов, эта политика также блокирует доступ к сайтам, которые содержат откровенные материалы сексуального характера. 

//3. Security + Pornography + Non-Family Friendly (198.153.192.60 и 198.153.194.60) - эта политика идеально подходит для семей с маленькими детьми. В дополнение к блокированию небезопасных сайтов и порнографических сайтов, эта политика также блокирует доступ к сайтам, которые показывают материалы для взрослых, аборты, алкоголь, преступность, культы, наркотики, азартные игры, ненависть, сексуальные ориентации, самоубийства, табак, или насилия. 

        static string[] dnslist = {"198.153.192.40","198.153.194.40","192.168.132.90","192.168.132.154","10.36.64.11","10.36.64.12", "176.9.147.55","95.154.128.32", "77.88.8.88","77.88.8.2","77.88.8.7","77.88.8.3","77.88.8.8",
                                      "77.88.8.1","193.58.251.251","208.67.222.222","208.67.220.220","8.8.8.8","198.153.192.50", "198.153.194.50","198.153.192.60","198.153.194.60"};

        static DnsMessageBase ProcessQuery(DnsMessageBase message, IPAddress clientAddress, ProtocolType protocol)
        {
            message.IsQuery = false;

            DnsMessage query = message as DnsMessage;

            if ((query != null) && (query.Questions.Count == 1))
            {
                // send query to upstream server
                DnsQuestion question = query.Questions[0];
                Random rnd = new Random();
                Console.WriteLine(question.Name);

                System.Threading.Tasks.Parallel.ForEach(dnslist, (site, state) =>
                {
                    Console.WriteLine("Get Info from: " +site);
                    DnsClient cd = new DnsClient(IPAddress.Parse(dnslist[rnd.Next(0, dnslist.Length - 1)]), 500);
                    DnsMessage answer = cd.Resolve(question.Name, question.RecordType, question.RecordClass);
                   
                        if (answer != null)
                        {
                            foreach (DnsRecordBase record in (answer.AnswerRecords))
                            {
                                lock (query)
                                { 
                                    query.AnswerRecords.Add(record);
                                }
                                Console.WriteLine(record.Name);
                            }
                            foreach (DnsRecordBase record in (answer.AdditionalRecords))
                            {
                                lock (query)
                                {
                                    query.AnswerRecords.Add(record);
                                }
                                Console.WriteLine(record.Name);
                            }
                            lock (query)
                            {
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
            // Not a valid query or upstream server did not answer correct
            message.ReturnCode = ReturnCode.ServerFailure;
            return message;
        }
    }
}
