﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace KinectPlayerTracker.Sockets
{
    /// <summary>
    /// 异步TCP客户端
    /// </summary>
    public class AsyncTcpClient : IDisposable
    {
        #region Fields

        private TcpClient _tcpClient;
        private bool _disposed = false;
        private int _retries = 0;

        #endregion

        #region Ctors

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteEP">远端服务器终结点</param>
        public AsyncTcpClient(IPEndPoint remoteEP)
          : this(new[] { remoteEP.Address }, remoteEP.Port)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteEP">远端服务器终结点</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(IPEndPoint remoteEP, IPEndPoint localEP)
          : this(new[] { remoteEP.Address }, remoteEP.Port, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(IPAddress remoteIPAddress, int remotePort)
          : this(new[] { remoteIPAddress }, remotePort)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddress">远端服务器IP地址</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP)
          : this(new[] { remoteIPAddress }, remotePort, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(string remoteHostName, int remotePort)
          : this(Dns.GetHostAddresses(remoteHostName), remotePort)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteHostName">远端服务器主机名</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(string remoteHostName, int remotePort, IPEndPoint localEP)
          : this(Dns.GetHostAddresses(remoteHostName), remotePort, localEP)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        public AsyncTcpClient(IPAddress[] remoteIPAddresses, int remotePort)
          : this(remoteIPAddresses, remotePort, null)
        {
        }

        /// <summary>
        /// 异步TCP客户端
        /// </summary>
        /// <param name="remoteIPAddresses">远端服务器IP地址列表</param>
        /// <param name="remotePort">远端服务器端口</param>
        /// <param name="localEP">本地客户端终结点</param>
        public AsyncTcpClient(IPAddress[] remoteIPAddresses, int remotePort, IPEndPoint localEP)
        {
            this.Addresses = remoteIPAddresses;
            this.Port = remotePort;
            this.LocalIPEndPoint = localEP;
            this.Encoding = Encoding.Default;

            if (this.LocalIPEndPoint != null)
            {
                this._tcpClient = new TcpClient(this.LocalIPEndPoint);
            }
            else
            {
                this._tcpClient = new TcpClient();
            }

            Retries = 3;
            RetryInterval = 5;
        }

        #endregion

        #region Properties

        /// <summary>
        /// 是否已与服务器建立连接
        /// </summary>
        public bool Connected { get { return _tcpClient.Client.Connected; } }
        /// <summary>
        /// 远端服务器的IP地址列表
        /// </summary>
        public IPAddress[] Addresses { get; private set; }
        /// <summary>
        /// 远端服务器的端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 连接重试次数
        /// </summary>
        public int Retries { get; set; }
        /// <summary>
        /// 连接重试间隔
        /// </summary>
        public int RetryInterval { get; set; }
        /// <summary>
        /// 远端服务器终结点
        /// </summary>
        public IPEndPoint RemoteIPEndPoint { get { return new IPEndPoint(Addresses[0], Port); } }
        /// <summary>
        /// 本地客户端终结点
        /// </summary>
        protected IPEndPoint LocalIPEndPoint { get; private set; }
        /// <summary>
        /// 通信所使用的编码
        /// </summary>
        public Encoding Encoding { get; set; }

        #endregion

        #region Connect

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <returns>异步TCP客户端</returns>
        public AsyncTcpClient Connect()
        {
            if (!Connected)
            {
                // start the async connect operation
                _tcpClient.BeginConnect(Addresses, Port, HandleTcpServerConnected, _tcpClient);
            }

            return this;
        }

        /// <summary>
        /// 关闭与服务器的连接
        /// </summary>
        /// <returns>异步TCP客户端</returns>
        public AsyncTcpClient Close()
        {
            if (Connected)
            {
                _retries = 0;
                _tcpClient.Close();
                RaiseServerDisconnected(Addresses, Port);
            }

            return this;
        }

        #endregion

        #region Receive

        private void HandleTcpServerConnected(IAsyncResult ar)
        {
            try
            {
                _tcpClient.EndConnect(ar);
                RaiseServerConnected(Addresses, Port);
                _retries = 0;
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex);

                _retries++;
                if (_retries > Retries)
                {
                    // we have failed to connect to all the IP Addresses, connection has failed overall.
                    RaiseServerExceptionOccurred(Addresses, Port, ex);
                    return;
                }
                else
                {
                    Thread.Sleep(TimeSpan.FromSeconds(RetryInterval));
                    Connect();
                    return;
                }
            }

            // we are connected successfully and start asyn read operation.
            byte[] buffer = new byte[_tcpClient.ReceiveBufferSize];
            _tcpClient.GetStream().BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
        }

        private void HandleDatagramReceived(IAsyncResult ar)
        {
            NetworkStream stream = _tcpClient.GetStream();

            int numberOfReadBytes = 0;
            try
            {
                numberOfReadBytes = stream.EndRead(ar);
            }
            catch
            {
                numberOfReadBytes = 0;
            }

            if (numberOfReadBytes == 0)
            {
                // connection has been closed
                Close();
                return;
            }

            // received byte and trigger event notification
            byte[] buffer = (byte[])ar.AsyncState;
            byte[] receivedBytes = new byte[numberOfReadBytes];
            Buffer.BlockCopy(buffer, 0, receivedBytes, 0, numberOfReadBytes);
            RaiseDatagramReceived(_tcpClient, receivedBytes);
            RaisePlaintextReceived(_tcpClient, receivedBytes);

            // then start reading from the network again
            stream.BeginRead(buffer, 0, buffer.Length, HandleDatagramReceived, buffer);
        }

        #endregion

        #region Events

        /// <summary>
        /// 接收到数据报文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<byte[]>> DatagramReceived;
        /// <summary>
        /// 接收到数据报文明文事件
        /// </summary>
        public event EventHandler<TcpDatagramReceivedEventArgs<string>> PlaintextReceived;

        private void RaiseDatagramReceived(TcpClient sender, byte[] datagram)
        {
            if (DatagramReceived != null)
            {
                DatagramReceived(this, new TcpDatagramReceivedEventArgs<byte[]>(sender, datagram));
            }
        }

        private void RaisePlaintextReceived(TcpClient sender, byte[] datagram)
        {
            if (PlaintextReceived != null)
            {
                PlaintextReceived(this, new TcpDatagramReceivedEventArgs<string>(sender, this.Encoding.GetString(datagram, 0, datagram.Length)));
            }
        }

        /// <summary>
        /// 与服务器的连接已建立事件
        /// </summary>
        public event EventHandler<TcpServerConnectedEventArgs> ServerConnected;
        /// <summary>
        /// 与服务器的连接已断开事件
        /// </summary>
        public event EventHandler<TcpServerDisconnectedEventArgs> ServerDisconnected;
        /// <summary>
        /// 与服务器的连接发生异常事件
        /// </summary>
        public event EventHandler<TcpServerExceptionOccurredEventArgs> ServerExceptionOccurred;

        private void RaiseServerConnected(IPAddress[] ipAddresses, int port)
        {
            if (ServerConnected != null)
            {
                ServerConnected(this, new TcpServerConnectedEventArgs(ipAddresses, port));
            }
        }

        private void RaiseServerDisconnected(IPAddress[] ipAddresses, int port)
        {
            if (ServerDisconnected != null)
            {
                ServerDisconnected(this, new TcpServerDisconnectedEventArgs(ipAddresses, port));
            }
        }

        private void RaiseServerExceptionOccurred(IPAddress[] ipAddresses, int port, Exception innerException)
        {
            if (ServerExceptionOccurred != null)
            {
                ServerExceptionOccurred(this, new TcpServerExceptionOccurredEventArgs(ipAddresses, port, innerException));
            }
        }

        #endregion

        #region Send

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(byte[] datagram)
        {
            if (datagram == null)
                throw new ArgumentNullException("datagram");

            if (!Connected)
            {
                RaiseServerDisconnected(Addresses, Port);
                throw new InvalidProgramException("This client has not connected to server.");
            }

            _tcpClient.GetStream().BeginWrite(datagram, 0, datagram.Length, HandleDatagramWritten, _tcpClient);
        }

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(byte datagram)
        {
            byte[] data = new byte[1] { datagram };
            Send(data);
        }

        private void HandleDatagramWritten(IAsyncResult ar)
        {
            ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
        }

        /// <summary>
        /// 发送报文
        /// </summary>
        /// <param name="datagram">报文</param>
        public void Send(string datagram)
        {
            Send(this.Encoding.GetBytes(datagram));
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Close();

                        if (_tcpClient != null)
                        {
                            _tcpClient = null;
                        }
                    }
                    catch (SocketException ex)
                    {
                        ExceptionHandler.Handle(ex);
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
