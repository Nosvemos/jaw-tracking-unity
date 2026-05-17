using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JawTracking.Network
{
    public sealed class UdpJawReceiver : IDisposable
    {
        private const int MaxQueuedPackets = 256;

        private readonly ConcurrentQueue<string> packetQueue = new ConcurrentQueue<string>();
        private readonly object lifecycleLock = new object();

        private UdpClient udpClient;
        private Thread receiveThread;
        private volatile bool isRunning;
        private int queuedPacketCount;
        private int windowPacketCount;
        private DateTime packetRateWindowStartUtc;

        public string ListenAddress { get; private set; } = "0.0.0.0";
        public int ListenPort { get; private set; }
        public long TotalPackets { get; private set; }
        public float PacketsPerSecond { get; private set; }
        public DateTime LastPacketUtc { get; private set; }
        public bool IsRunning => isRunning;
        public int PendingPackets => Math.Max(0, Volatile.Read(ref queuedPacketCount));

        public void Start(string listenAddress, int listenPort)
        {
            lock (lifecycleLock)
            {
                if (isRunning)
                {
                    return;
                }

                ListenAddress = NormalizeListenAddress(listenAddress);
                ListenPort = listenPort;
                TotalPackets = 0;
                PacketsPerSecond = 0f;
                LastPacketUtc = DateTime.MinValue;
                windowPacketCount = 0;
                packetRateWindowStartUtc = DateTime.UtcNow;
                ClearQueue();

                IPAddress address = ResolveListenAddress(ListenAddress);
                udpClient = new UdpClient(new IPEndPoint(address, listenPort));
                isRunning = true;
                receiveThread = new Thread(ReceiveLoop)
                {
                    IsBackground = true,
                    Name = "Jaw UDP Receiver"
                };
                receiveThread.Start();
            }
        }

        public void Start(int listenPort)
        {
            Start("0.0.0.0", listenPort);
        }

        public void Stop()
        {
            lock (lifecycleLock)
            {
                if (!isRunning)
                {
                    return;
                }

                isRunning = false;
                udpClient?.Close();
                udpClient?.Dispose();
                udpClient = null;
            }

            if (receiveThread != null && receiveThread.IsAlive)
            {
                receiveThread.Join(200);
            }

            receiveThread = null;
        }

        public bool TryDequeue(out string packet)
        {
            if (!packetQueue.TryDequeue(out packet))
            {
                return false;
            }

            Interlocked.Decrement(ref queuedPacketCount);
            return true;
        }

        public bool TryDequeueLatest(out string packet, int maxDrainCount, out int drainedCount)
        {
            packet = null;
            drainedCount = 0;
            int safeDrainCount = Math.Max(1, maxDrainCount);

            while (drainedCount < safeDrainCount && TryDequeue(out string nextPacket))
            {
                packet = nextPacket;
                drainedCount++;
            }

            return packet != null;
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveLoop()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            while (isRunning)
            {
                try
                {
                    byte[] bytes = udpClient.Receive(ref endpoint);
                    string packet = Encoding.UTF8.GetString(bytes);
                    packetQueue.Enqueue(packet);
                    Interlocked.Increment(ref queuedPacketCount);
                    TrimQueueIfNeeded();
                    UpdateStats();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    if (!isRunning)
                    {
                        break;
                    }
                }
            }
        }

        private void UpdateStats()
        {
            TotalPackets++;
            LastPacketUtc = DateTime.UtcNow;
            windowPacketCount++;

            double elapsedSeconds = (LastPacketUtc - packetRateWindowStartUtc).TotalSeconds;
            if (elapsedSeconds >= 1.0)
            {
                PacketsPerSecond = (float)(windowPacketCount / elapsedSeconds);
                windowPacketCount = 0;
                packetRateWindowStartUtc = LastPacketUtc;
            }
        }

        private void ClearQueue()
        {
            while (packetQueue.TryDequeue(out _))
            {
            }

            Interlocked.Exchange(ref queuedPacketCount, 0);
        }

        private void TrimQueueIfNeeded()
        {
            while (PendingPackets > MaxQueuedPackets && packetQueue.TryDequeue(out _))
            {
                Interlocked.Decrement(ref queuedPacketCount);
            }
        }

        private static string NormalizeListenAddress(string listenAddress)
        {
            return string.IsNullOrWhiteSpace(listenAddress) ? "0.0.0.0" : listenAddress.Trim();
        }

        private static IPAddress ResolveListenAddress(string listenAddress)
        {
            if (listenAddress == "0.0.0.0" || listenAddress == "*")
            {
                return IPAddress.Any;
            }

            if (listenAddress.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return IPAddress.Loopback;
            }

            if (IPAddress.TryParse(listenAddress, out IPAddress address))
            {
                return address;
            }

            throw new FormatException($"Geçersiz UDP dinleme IP adresi: {listenAddress}");
        }
    }
}
