using GuerrillaNtp;
using NAudio.Wave;
using ihnm.Managers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ihnm.Helpers
{
    public static class SkipOverHelper
    {
        static HttpClient client = new HttpClient();

        public static TimeSpan getDuration(string filename)
        {
            AudioFileReader input = new AudioFileReader(filename);

            return input.TotalTime;
        }

        public static TimeSpan getStartTime(TimeSpan TotalTime)
        {
            DateTime currentTime = GetTimeAsync("https://time.google.com").Result;
            long mSeconds = currentTime.Minute * 60 * 1000 + currentTime.Second * 1000 + currentTime.Millisecond;


            int duration = (int)TotalTime.TotalMilliseconds;
            int songsIn = (int)(mSeconds / duration);

            long newMseconds = duration * songsIn;


            int startTime = (int)(mSeconds - newMseconds);


            return TimeSpan.FromMilliseconds(startTime);

        }

        public static async Task<TimeSpan> getStartTimeTask(TimeSpan TotalTime, int ping=0)
        {
            DateTime currentTime = await GetNetworkTime();
            long mSeconds = currentTime.Minute * 60 * 1000 + currentTime.Second * 1000 + currentTime.Millisecond;


            int duration = (int)TotalTime.TotalMilliseconds;
            int songsIn = (int)(mSeconds / duration);

            long newMseconds = duration * songsIn;


            int startTime = (int)(mSeconds - newMseconds);


            return TimeSpan.FromMilliseconds(startTime) - TimeSpan.FromMilliseconds(ping);

        }

        static async Task<DateTime> GetTimeAsync(string path)
        {
            var tcp = new TcpClient("time.google.com", 13);
            string response;
            using (var stream = new System.IO.StreamReader(tcp.GetStream()))
            {
                response = stream.ReadToEnd();
            }
            string utc = response.Substring(7, 17);
            var dt = DateTime.Parse(utc);
            return dt;
        }

        public static async Task<DateTime> GetNetworkTime()
        {
            //const string ntpServer = "time.google.com";
            //var ntpData = new byte[48];
            //ntpData[0] = 0x1B; //LeapIndicator = 0 (no warning), VersionNum = 3 (IPv4 only), Mode = 3 (Client Mode)

            //var addresses = Dns.GetHostEntry(ntpServer).AddressList;
            //var ipEndPoint = new IPEndPoint(addresses[0], 123);
            //var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            //socket.Connect(ipEndPoint);
            //socket.Send(ntpData);

            //socket.Receive(ntpData);
            //socket.Close();

            //ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | (ulong)ntpData[43];
            //ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | (ulong)ntpData[47];

            //var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            //var utc = (new DateTime(1900, 1, 1)).AddMilliseconds((long)milliseconds);

            NtpClient client = NtpClient.Default;
            NtpClock clock = client.Query();

            DateTime utc = clock.UtcNow.UtcDateTime;


            return utc;
        }

    }


    public class jsString
    {
        public int year { get; set; }
        public int month { get; set; }
        public int day { get; set; }
        public int hour { get; set; }
        public int minute { get; set; }
        public int seconds { get; set; }
        public int milliSeconds { get; set; }
        public DateTime dateTime { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public string timeZone { get; set; }
        public string dayOfWeek { get; set; }
        public bool dstActive { get; set; }
    }


    internal sealed class HttpEventListener : EventListener
    {
        // Constant necessary for attaching ActivityId to the events.
        public const EventKeywords TasksFlowActivityIds = (EventKeywords)0x80;
        private AsyncLocal<HttpRequestTimingDataRaw> _timings = new AsyncLocal<HttpRequestTimingDataRaw>();

        internal HttpEventListener()
        {
            // set variable here
            _timings.Value = new HttpRequestTimingDataRaw();
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // List of event source names provided by networking in .NET 5.
            if (eventSource.Name == "System.Net.Http" ||
                eventSource.Name == "System.Net.Sockets" ||
                eventSource.Name == "System.Net.Security" ||
                eventSource.Name == "System.Net.NameResolution")
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
            // Turn on ActivityId.
            else if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            {
                // Attach ActivityId to the events.
                EnableEvents(eventSource, EventLevel.LogAlways, TasksFlowActivityIds);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            var timings = _timings.Value;
            if (timings == null)
                return; // some event which is not related to this scope, ignore it
            var fullName = eventData.EventSource.Name + "." + eventData.EventName;

            switch (fullName)
            {
                case "System.Net.Http.RequestStart":
                    timings.RequestStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestStop":
                    timings.RequestStop = eventData.TimeStamp;
                    break;
                case "System.Net.NameResolution.ResolutionStart":
                    timings.DnsStart = eventData.TimeStamp;
                    break;
                case "System.Net.NameResolution.ResolutionStop":
                    timings.DnsStop = eventData.TimeStamp;
                    break;
                case "System.Net.Sockets.ConnectStart":
                    timings.SocketConnectStart = eventData.TimeStamp;
                    break;
                case "System.Net.Sockets.ConnectStop":
                    timings.SocketConnectStop = eventData.TimeStamp;
                    break;
                case "System.Net.Security.HandshakeStart":
                    timings.SslHandshakeStart = eventData.TimeStamp;
                    break;
                case "System.Net.Security.HandshakeStop":
                    timings.SslHandshakeStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestHeadersStart":
                    timings.RequestHeadersStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.RequestHeadersStop":
                    timings.RequestHeadersStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseHeadersStart":
                    timings.ResponseHeadersStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseHeadersStop":
                    timings.ResponseHeadersStop = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseContentStart":
                    timings.ResponseContentStart = eventData.TimeStamp;
                    break;
                case "System.Net.Http.ResponseContentStop":
                    timings.ResponseContentStop = eventData.TimeStamp;
                    break;
            }
        }

        public HttpRequestTimings GetTimings()
        {
            var raw = _timings.Value!;
            return new HttpRequestTimings
            {
                Request = raw.RequestStop - raw.RequestStart,
                Dns = raw.DnsStop - raw.DnsStart,
                SslHandshake = raw.SslHandshakeStop - raw.SslHandshakeStart,
                SocketConnect = raw.SocketConnectStop - raw.SocketConnectStart,
                RequestHeaders = raw.RequestHeadersStop - raw.RequestHeadersStart,
                ResponseHeaders = raw.ResponseHeadersStop - raw.ResponseHeadersStart,
                ResponseContent = raw.ResponseContentStop - raw.ResponseContentStart
            };
        }

        public class HttpRequestTimings
        {
            public TimeSpan? Request { get; set; }
            public TimeSpan? Dns { get; set; }
            public TimeSpan? SslHandshake { get; set; }
            public TimeSpan? SocketConnect { get; set; }
            public TimeSpan? RequestHeaders { get; set; }
            public TimeSpan? ResponseHeaders { get; set; }
            public TimeSpan? ResponseContent { get; set; }
        }

        private class HttpRequestTimingDataRaw
        {
            public DateTime? DnsStart { get; set; }
            public DateTime? DnsStop { get; set; }
            public DateTime? RequestStart { get; set; }
            public DateTime? RequestStop { get; set; }
            public DateTime? SocketConnectStart { get; set; }
            public DateTime? SocketConnectStop { get; set; }
            public DateTime? SslHandshakeStart { get; set; }
            public DateTime? SslHandshakeStop { get; set; }
            public DateTime? RequestHeadersStart { get; set; }
            public DateTime? RequestHeadersStop { get; set; }
            public DateTime? ResponseHeadersStart { get; set; }
            public DateTime? ResponseHeadersStop { get; set; }
            public DateTime? ResponseContentStart { get; set; }
            public DateTime? ResponseContentStop { get; set; }
        }
    }

}
