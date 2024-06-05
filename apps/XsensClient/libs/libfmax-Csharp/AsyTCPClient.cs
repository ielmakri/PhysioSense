using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.IO;

namespace Libfmax
{
    public class AsyTcpClient
    {
        public TcpClient client;
        private IPEndPoint ipe;
        private CancellationToken token;
        private int timeout; // timeout in miliseconds
        public event InfoMessageDelegate InfoMessageReceived;
        public delegate void AsyDataReceivedDelegate(object sender, byte[] buffer);
        public event AsyDataReceivedDelegate DataReceived;

        static readonly object sendLock = new object();

        public AsyTcpClient(string ipAddress, int port, CancellationToken token, int timeout = 0)
        {
            ipe = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            client = new TcpClient();
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.token = token;
            this.timeout = timeout;
        }

        ~AsyTcpClient()
        {
            Close();
        }

        public void Close()
        {
            if (client != null && client.Connected)
            {
                client.GetStream()?.Close();
                client.Close();
            }

            client = null;

            if (ipe != null)
            {
                InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Connection closed.", Info.Mode.CriticalEvent));
            }

        }

        public bool Connect()
        {
            InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Waiting for connection.", Info.Mode.Event));
            try
            {
                IAsyncResult ar = client.BeginConnect(ipe.Address, ipe.Port, null, null);
                WaitHandle wh = ar.AsyncWaitHandle;
                if (!ar.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10), false))
                {
                    client.Close();
                    InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Connection request timed out.", Info.Mode.CriticalError));

                    return false;
                }

                client.EndConnect(ar);
                wh.Close();

                InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Connected.", Info.Mode.CriticalEvent));

                return true;
            }
            catch (Exception e)
            {
                client.Close();
                InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Failed to connect to server.", Info.Mode.CriticalError));
            };

            return false;

        }

        public void Start()
        {
            Task.Run(async () => await DataReceiver(token), token);
        }

        protected virtual void InfoMessage(Info info)
        {
            InfoMessageReceived?.Invoke(this, info);
        }

        protected virtual void OnDataReceived(byte[] receivedData)
        {
            DataReceived?.Invoke(this, receivedData);
        }

        private async Task DataReceiver(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested
                        || client == null
                        || !client.Connected)
                    //|| !IsClientConnected(client))
                    {
                        InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Server disconnected.", Info.Mode.CriticalEvent));
                        break;
                    }

                    byte[] receivedData = null;

                    if (timeout > 0)
                    {

                        var task = DataReadAsync(token);
                        if (await Task.WhenAny(task, Task.Delay(timeout, token)) == task)
                        {
                            // Task completed within timeout limit.                        
                            receivedData = task.Result;
                        }
                        else
                        {
                            InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Timeout: data not recevied yet for {timeout} ms.", Info.Mode.Error));

                            task.Wait();
                            receivedData = task.Result;

                            InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Timeout: Data recevied after timeout.", Info.Mode.Event));

                        }
                    }
                    else
                    {
                        receivedData = await DataReadAsync(token);
                    }

                    OnDataReceived(receivedData);

                }
            }
            catch (Exception e)
            {
                InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, " + $"Exception in data receive loop", Info.Mode.Error));
            }

            Close();

            InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, Data recevier loop terminated.", Info.Mode.Event));

        }

        private async Task<byte[]> DataReadAsync(CancellationToken token)
        {
            if (client == null
                || !client.Connected)
            {
                throw new OperationCanceledException();
            }

            byte[] buffer = new byte[2048];
            int read = 0;

            NetworkStream networkStream = client.GetStream();
            if (!networkStream.CanRead && !networkStream.DataAvailable)
            {
                throw new IOException();
            }

            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    read = await networkStream.ReadAsync(buffer, 0, buffer.Length, token);

                    if (read > 0)
                    {
                        ms.Write(buffer, 0, read);
                        return ms.ToArray();
                    }
                    else
                    {
                        throw new SocketException();
                    }
                }
            }
        }

        public void SendData(string data)
        {
            if (String.IsNullOrEmpty(data)) return;

            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            try
            {
                lock (sendLock)
                {
                    client.GetStream().Write(dataBytes, 0, dataBytes.Length);
                }
            }
            catch (Exception)
            {
                InfoMessage(new Info($"{ipe.Address}:{ipe.Port}, " + $"Exception in data sending.", Info.Mode.Error));
            };
        }

    }

}