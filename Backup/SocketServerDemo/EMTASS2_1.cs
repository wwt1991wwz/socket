//## Project  : ETMASS����Extensible Multi-Thread Asynchronous Socket Server Framework
//## Author   : Hulihui(ehulh@163.com)
//## Creation Date : 2008-10-13
//## Modified Date : 2008-11-09

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Data.OleDb;

namespace CSUST.Net
{
    public class TSocketServerBase<TSession, TDatabase>: IDisposable, IDatabaseEvent, ISessionEvent
        where TSession : TSessionBase, new()
        where TDatabase: TDatabaseBase, new()
    {
        #region  member fields

        private Socket m_serverSocket;
        private bool m_serverClosed = true;
        private bool m_serverListenPaused = false;

        private int m_acceptListenTimeInterval = 25;         // ������ѯʱ����(ms)
        private int m_checkSessionTableTimeInterval = 100;   // ����Timer��ʱ����(ms)
        private int m_checkDatagramQueueTimeInterval = 100;  // ������ݰ�����ʱ����Ϣ���(ms)
        private int m_servertPort = 3130;

        private int m_sessionSequenceNo = 0;  // sessionID ��ˮ��
        private int m_sessionCount;
        private int m_receivedDatagramCount;
        private int m_errorDatagramCount;
        private int m_datagramQueueLength;

        private int m_databaseExceptionCount;
        private int m_serverExceptCount;
        private int m_sessionExceptionCount;

        private int m_maxSessionCount = 1024;
        private int m_recvBufferSize = 16 * 1024;  // 16 K
        private int m_sendBufferSize = 16 * 1024;

        private int m_maxDatagramSize = 1024 * 1024;  // 1M
        private int m_maxSessionTimeout = 120;   // 2 minutes
        private int m_maxListenQueueLength = 16;
        private int m_maxSameIPCount = 64;

        private Dictionary<int, TSession> m_sessionTable;
        private TDatabase m_databaseObj = null;

        private bool m_disposed = false;

        private ManualResetEvent m_checkAcceptListenResetEvent;
        private ManualResetEvent m_checkSessionTableResetEvent;
        private ManualResetEvent m_checkDatagramQueueResetEvent;

        private Mutex m_ServerMutex;  // ֻ����һ��������
        private BufferManager m_bufferManager;

        #endregion

        #region  public properties

        public bool Closed
        {
            get { return m_serverClosed; }
        }

        public bool ListenPaused
        {
            get { return m_serverListenPaused; }
        }

        public int ServerPort
        {
            get { return m_servertPort; }
            set { m_servertPort = value; }
        }

        public int ServerExceptionCount
        {
            get { return m_serverExceptCount; }
        }

        public int DatabaseExceptionCount
        {
            get { return m_databaseExceptionCount; }
        }

        public int SessionExceptionCount
        {
            get { return m_sessionExceptionCount; }
        }

        public int SessionCount
        {
            get { return m_sessionCount; }
        }

        public int ReceivedDatagramCount
        {
            get { return m_receivedDatagramCount; }
        }

        public int ErrorDatagramCount
        {
            get { return m_errorDatagramCount; }
        }

        public int DatagramQueueLength
        {
            get { return m_datagramQueueLength; }
        }

        [Obsolete("Use AcceptListenTimeInterval instead.")]
        public int LoopWaitTime
        {
            get { return m_acceptListenTimeInterval; }
            set { this.AcceptListenTimeInterval = value; }
        }

        public int AcceptListenTimeInterval
        {
            get { return m_acceptListenTimeInterval; }
            set
            {
                if (value < 0)
                {
                    m_acceptListenTimeInterval = value;
                }
                else
                {
                    m_acceptListenTimeInterval = value;
                }
            }
        }

        public int checkSessionTableTimeInterval
        {
            get { return m_checkSessionTableTimeInterval; }
            set
            {
                if (value < 10)
                {
                    m_checkSessionTableTimeInterval = 10;
                }
                else
                {
                    m_checkSessionTableTimeInterval = value;
                }
            }
        }

        public int CheckDatagramqueueTimeInterval
        {
            get { return m_checkDatagramQueueTimeInterval; }
            set
            {
                if (value < 10)
                {
                    m_checkDatagramQueueTimeInterval = 10;
                }
                else
                {
                    m_checkDatagramQueueTimeInterval = value;
                }
            }
        }

        public int MaxSessionCount
        {
            get { return m_maxSessionCount; }
        }

        [Obsolete]
        public int MaxSessionTableLength
        {
            get { return m_maxSessionCount; }
            set
            {
                if (value <= 1)
                {
                    m_maxSessionCount = 1;
                }
                else
                {
                    m_maxSessionCount = value;
                }
            }
        }

        public int ReceiveBufferSize
        {
            get { return m_recvBufferSize; }
        }

        public int SendBufferSize
        {
            get { return m_sendBufferSize; }
        }

        [Obsolete]
        public int MaxReceiveBufferSize
        {
            get { return m_recvBufferSize; }
            set
            {
                if (value < 1024)
                {
                    m_recvBufferSize = 1024;
                    m_sendBufferSize = 1024;
                }
                else
                {
                    m_recvBufferSize = value;
                    m_sendBufferSize = value;
                }
            }
        }
        
        public int MaxDatagramSize
        {
            get { return m_maxDatagramSize; }
            set
            {
                if (value < 1024)
                {
                    m_maxDatagramSize = 1024;
                }
                else
                {
                    m_maxDatagramSize = value;
                }
            }
        }

        public int MaxListenQueueLength
        {
            get { return m_maxListenQueueLength; }
            set
            {
                if (value <= 1)
                {
                    m_maxListenQueueLength = 2;
                }
                else
                {
                    m_maxListenQueueLength = value;
                }
            }
        }

        public int MaxSessionTimeout
        {
            get { return m_maxSessionTimeout; }
            set
            {
                if (value < 120)
                {
                    m_maxSessionTimeout = 120;
                }
                else
                {
                    m_maxSessionTimeout = value;
                }
            }
        }

        public int MaxSameIPCount
        {
            get { return m_maxSameIPCount; }
            set
            {
                if (value < 1)
                {
                    m_maxSameIPCount = 1;
                }
                else
                {
                    m_maxSameIPCount = value;
                }
            }
        }

        [Obsolete("Use SessionCoreInfoCollection instead.")]
        public List<TSessionCoreInfo> SessionCoreInfoList
        {
            get
            {
                List<TSessionCoreInfo> sessionList = new List<TSessionCoreInfo>();
                lock (m_sessionTable)
                {
                    foreach (TSession session in m_sessionTable.Values)
                    {
                        sessionList.Add((TSessionCoreInfo)session);
                    }
                }
                return sessionList;
            }
        }

        public Collection<TSessionCoreInfo> SessionCoreInfoCollection
        {
            get
            {
                Collection<TSessionCoreInfo> sessionCollection = new Collection<TSessionCoreInfo>();
                lock(m_sessionTable)
                {
                    foreach(TSession session in m_sessionTable.Values)
                    {
                        sessionCollection.Add((TSessionCoreInfo)session);
                    }
                }
                return sessionCollection;
            }
        }

        #endregion

        #region  class events

        public event EventHandler ServerStarted;
        public event EventHandler ServerClosed;
        public event EventHandler ServerListenPaused;
        public event EventHandler ServerListenResumed;
        public event EventHandler<TExceptionEventArgs> ServerException;

        public event EventHandler SessionRejected;
        public event EventHandler<TSessionEventArgs> SessionConnected;
        public event EventHandler<TSessionEventArgs> SessionDisconnected;
        public event EventHandler<TSessionEventArgs> SessionTimeout;

        public event EventHandler<TSessionEventArgs> DatagramDelimiterError;
        public event EventHandler<TSessionEventArgs> DatagramOversizeError;
        public event EventHandler<TSessionExceptionEventArgs> SessionReceiveException;
        public event EventHandler<TSessionExceptionEventArgs> SessionSendException;
        public event EventHandler<TSessionEventArgs> DatagramAccepted;
        public event EventHandler<TSessionEventArgs> DatagramError;
        public event EventHandler<TSessionEventArgs> DatagramHandled;

        public event EventHandler<TExceptionEventArgs> DatabaseOpenException;
        public event EventHandler<TExceptionEventArgs> DatabaseCloseException;
        public event EventHandler<TExceptionEventArgs> DatabaseException;

        public event EventHandler<TExceptionEventArgs> ShowDebugMessage;
        
        #endregion

        #region  class constructor

        public TSocketServerBase()
        {
            this.Initiate(m_maxSessionCount, m_recvBufferSize, m_sendBufferSize, null);
        }

        public TSocketServerBase(string dbConnectionString)
        {
            this.Initiate(m_maxSessionCount, m_recvBufferSize, m_sendBufferSize, dbConnectionString);
        }

        public TSocketServerBase(int maxSessionCount, int recvBufferSize, int sendBufferSize)
        {
            this.Initiate(maxSessionCount, recvBufferSize, sendBufferSize, null);
        }

        public TSocketServerBase(int maxSessionCount, int recvBufferSize, int sendBufferSize, string dbConnectionString)
        {
            this.Initiate(maxSessionCount, recvBufferSize, sendBufferSize, dbConnectionString);
        }

        [Obsolete]
        public TSocketServerBase(int tcpPort, string dbConnectionString)
        {
            m_servertPort = tcpPort;
            this.Initiate(m_maxSessionCount, m_recvBufferSize, m_sendBufferSize, dbConnectionString);
        }

        private void Initiate(int maxSessionCount, int recvBufferSize, int sendBufferSize, string dbConnectionString)
        {
            bool canCreateNew;
            m_ServerMutex = new Mutex(true, "EMTASS_SERVER", out canCreateNew);
            if (!canCreateNew)
            {
                throw new Exception("Can create two or more server!");
            }

            m_maxSessionCount = maxSessionCount;
            m_recvBufferSize = recvBufferSize;
            m_sendBufferSize = sendBufferSize;

            m_bufferManager = new BufferManager(maxSessionCount, recvBufferSize, sendBufferSize);
            m_sessionTable = new Dictionary<int, TSession>();

            m_checkAcceptListenResetEvent = new ManualResetEvent(true);
            m_checkSessionTableResetEvent = new ManualResetEvent(true);
            m_checkDatagramQueueResetEvent = new ManualResetEvent(true);

            if (dbConnectionString != null)
            {
                m_databaseObj = new TDatabase();
                m_databaseObj.Initiate(dbConnectionString);

                m_databaseObj.DatabaseOpenException += new EventHandler<TExceptionEventArgs>(this.OnDatabaseOpenException);  // ת�����ݿ��¼�
                m_databaseObj.DatabaseCloseException += new EventHandler<TExceptionEventArgs>(this.OnDatabaseCloseException);
                m_databaseObj.DatabaseException += new EventHandler<TExceptionEventArgs>(this.OnDatabaseException);
            }
        }


        ~TSocketServerBase()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            if (!m_disposed)
            {
                m_disposed = true;
                this.Close();
                this.Dispose(true);
                GC.SuppressFinalize(this);  // Finalize ����ڶ���ִ��
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)  // �������ڱ���ʾ�ͷ�, ����ִ�� Finalize()
            {
                m_sessionTable = null;  // �ͷ��й���Դ
            }

            if (m_ServerMutex != null)
            {
                m_ServerMutex.Close();
            }

            if (m_checkAcceptListenResetEvent != null)
            {
                m_checkAcceptListenResetEvent.Close();  // �ͷŷ��й���Դ
            }

            if (m_checkSessionTableResetEvent != null)
            {
                m_checkSessionTableResetEvent.Close();
            }

            if (m_checkDatagramQueueResetEvent != null)
            {
                m_checkDatagramQueueResetEvent.Close();
            }
        }

        #endregion

        #region  public methods

        public bool Start()
        {
            if (!m_serverClosed)
            {
                return true;
            }

            m_serverClosed = true;  // ������������Ҫ�жϸ��ֶ�
            m_serverListenPaused = true;

            this.Close();
            this.ClearCountValues();

            try
            {
                if (m_databaseObj != null)
                {
                    m_databaseObj.Open();
                    if (m_databaseObj.State != ConnectionState.Open)
                    {
                        return false;
                    }
                }
               
                if (!this.CreateServerSocket()) return false;
                if (!ThreadPool.QueueUserWorkItem(this.CheckDatagramQueue)) return false;
                if (!ThreadPool.QueueUserWorkItem(this.StartServerListen)) return false;
                if (!ThreadPool.QueueUserWorkItem(this.CheckSessionTable)) return false;

                m_serverClosed = false;
                m_serverListenPaused = false;

                this.OnServerStarted();
            }
            catch (Exception err)
            {
                this.OnServerException(err);
            }
            return !m_serverClosed;
        }

        public void PauseListen()
        {
            m_serverListenPaused = true;
            this.OnServerListenPaused();
        }

        public void ResumeListen()
        {
            m_serverListenPaused = false;
            this.OnServerListenResumed();
        }

        public void Stop()
        {
            this.Close();
        }

        public void CloseSession(int sessionId)
        {
            TSession session = null;
            lock (m_sessionTable)
            {
                if (m_sessionTable.ContainsKey(sessionId))  // �����ûỰ ID
                {
                    session = (TSession)m_sessionTable[sessionId];
                }
            }

            if (session != null)
            {
                session.SetInactive();
            }
        }

        public void CloseAllSessions()
        {
            lock (m_sessionTable)
            {
                foreach (TSession session in m_sessionTable.Values)
                {
                    session.SetInactive();
                }
            }
        }

        public void SendToSession(int sessionId, string datagramText)
        {
            TSession session = null;
            lock (m_sessionTable)
            {
                session = (TSession)m_sessionTable[sessionId];
            }

            if (session != null)
            {
                session.SendDatagram(datagramText);
            }
        }

        public void SendToAllSessions(string datagramText)
        {
            lock (m_sessionTable)
            {
                foreach (TSession session in m_sessionTable.Values)
                {
                    session.SendDatagram(datagramText);
                }
            }
        }

        #endregion

        #region  private methods

        private void Close()
        {
            if (m_serverClosed)
            {
                return;
            }

            m_serverClosed = true;
            m_serverListenPaused = true;

            m_checkAcceptListenResetEvent.WaitOne();  // �ȴ�3���߳�
            m_checkSessionTableResetEvent.WaitOne();
            m_checkDatagramQueueResetEvent.WaitOne();

            if (m_databaseObj != null)
            {
                m_databaseObj.Close();
            }

            if (m_sessionTable != null)
            {
                lock (m_sessionTable)
                {
                    foreach (TSession session in m_sessionTable.Values)
                    {
                        session.Close();
                    }
                }
            }

            this.CloseServerSocket();

            if (m_sessionTable != null)  // ��ջỰ�б�
            {
                lock (m_sessionTable)
                {
                    m_sessionTable.Clear();
                }
            }

            this.OnServerClosed();
        }

        private void ClearCountValues()
        {
            m_sessionCount = 0;
            m_receivedDatagramCount = 0;
            m_errorDatagramCount = 0;
            m_datagramQueueLength = 0;

            m_databaseExceptionCount = 0;
            m_serverExceptCount = 0;
            m_sessionExceptionCount = 0;
        }

        private bool CreateServerSocket()
        {
            try
            {
                m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                m_serverSocket.Bind(new IPEndPoint(IPAddress.Any, m_servertPort));
                m_serverSocket.Listen(m_maxListenQueueLength);

                return true;
            }
            catch (Exception err)
            {
                this.OnServerException(err);
                return false;
            }
        }

        private bool CheckSocketIP(Socket clientSocket)
        {
            IPEndPoint iep = (IPEndPoint)clientSocket.RemoteEndPoint;
            string ip = iep.Address.ToString();

            if (ip.Substring(0, 7) == "127.0.0")   // local machine
            {
                return true;
            }

            lock (m_sessionTable)
            {
                int sameIPCount = 0;
                foreach (TSession session in m_sessionTable.Values)
                {
                    if (session.IP == ip)
                    {
                        sameIPCount++;
                        if (sameIPCount > m_maxSameIPCount)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// �����ͻ�����������
        /// </summary>
        private void StartServerListen(object state)
        {
            m_checkAcceptListenResetEvent.Reset();
            Socket clientSocket = null;

            while (!m_serverClosed)
            {
                if (m_serverListenPaused)  // pause server
                {
                    this.CloseServerSocket();
                    Thread.Sleep(m_acceptListenTimeInterval);
                    continue;
                }

                if (m_serverSocket == null)
                {
                    this.CreateServerSocket();
                    continue;
                }

                try
                {
                    if (m_serverSocket.Poll(m_acceptListenTimeInterval, SelectMode.SelectRead))
                    {
                        // Ƶ���رա�����ʱ���������ײ���������ʾ�׽���ֻ����һ����
                        clientSocket = m_serverSocket.Accept();

                        if (clientSocket != null && clientSocket.Connected)
                        {
                            if (m_sessionCount >= m_maxSessionCount || !this.CheckSocketIP(clientSocket))  // ��ǰ�б��Ѿ����ڸ� IP ��ַ
                            {
                                this.OnSessionRejected(); // �ܾ���¼����
                                this.CloseClientSocket(clientSocket);
                            }
                            else
                            {
                                this.AddSession(clientSocket);  // ��ӵ�������, �������첽���շ���
                            }
                        }
                        else  // clientSocket is null or connected == false
                        {
                            this.CloseClientSocket(clientSocket);
                        }
                    }
                }
                catch (Exception)  // �������ӵ��쳣Ƶ��, �������쳣
                {
                    this.CloseClientSocket(clientSocket);
                }
            }

            m_checkAcceptListenResetEvent.Set();
        }

        private void CloseServerSocket()
        {
            if (m_serverSocket == null)
            {
                return;
            }

            try
            {
                lock (m_sessionTable)
                {
                    if (m_sessionTable != null && m_sessionTable.Count > 0)
                    {
                        // ���ܽ����������˵� AcceptClientConnect �� Poll
//                        m_serverSocket.Shutdown(SocketShutdown.Both);  // �����ӲŹ�
                    }
                }
                m_serverSocket.Close();
            }
            catch (Exception err)
            {
                this.OnServerException(err);
            }
            finally
            {
                m_serverSocket = null;
            }
        }

        /// <summary>
        /// ǿ�ƹرտͻ�������ʱ�� Socket
        /// </summary>
        private void CloseClientSocket(Socket socket)
        {
            if (socket != null)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
                catch(Exception) { }  // ǿ�ƹر�, ���Դ���
            }
        }

        /// <summary>
        /// ����һ���Ự����
        /// </summary>
        private void AddSession(Socket clientSocket)
        {
            Interlocked.Increment(ref m_sessionSequenceNo);

            TSession session = new TSession();
            session.Initiate(m_maxDatagramSize, m_sessionSequenceNo, clientSocket, m_databaseObj, m_bufferManager);

            session.DatagramDelimiterError += new EventHandler<TSessionEventArgs>(this.OnDatagramDelimiterError);
            session.DatagramOversizeError += new EventHandler<TSessionEventArgs>(this.OnDatagramOversizeError);
            session.DatagramError += new EventHandler<TSessionEventArgs>(this.OnDatagramError);
            session.DatagramAccepted += new EventHandler<TSessionEventArgs>(this.OnDatagramAccepted);
            session.DatagramHandled += new EventHandler<TSessionEventArgs>(this.OnDatagramHandled);
            session.SessionReceiveException += new EventHandler<TSessionExceptionEventArgs>(this.OnSessionReceiveException);
            session.SessionSendException += new EventHandler<TSessionExceptionEventArgs>(this.OnSessionSendException);

            session.ShowDebugMessage += new EventHandler<TExceptionEventArgs>(this.ShowDebugMessage);

            lock (m_sessionTable)
            {
                m_sessionTable.Add(session.ID, session);
            }
            session.ReceiveDatagram();

            this.OnSessionConnected(session);
        }

        /// <summary>
        /// ��Դ�����߳�, �����ɲ����
        /// </summary>
        private void CheckSessionTable(object state)
        {
            m_checkSessionTableResetEvent.Reset();

            while (!m_serverClosed)
            {
                lock (m_sessionTable)
                {
                    List<int> sessionIDList = new List<int>();

                    foreach (TSession session in m_sessionTable.Values)
                    {
                        if (m_serverClosed)
                        {
                            break;
                        }

                        if (session.State == TSessionState.Inactive)  // ���������һ�� Session
                        {
                            session.Shutdown();  // ��һ��: shutdown, �����첽�¼�
                        }
                        else if (session.State == TSessionState.Shutdown)
                        {
                            session.Close();  // �ڶ���: Close
                        }
                        else if (session.State == TSessionState.Closed)
                        {
                            sessionIDList.Add(session.ID);
                            this.DisconnectSession(session);

                        }
                        else // �����ĻỰ Active
                        {
                            session.CheckTimeout(m_maxSessionTimeout); // �г�ʱ����������
                        }
                    }

                    foreach (int id in sessionIDList)  // ͳһ���
                    {
                        m_sessionTable.Remove(id);
                    }

                    sessionIDList.Clear();
                }

                Thread.Sleep(m_checkSessionTableTimeInterval);
            }

            m_checkSessionTableResetEvent.Set();
        }

        /// <summary>
        /// ���ݰ������߳�
        /// </summary>
        private void CheckDatagramQueue(object state)
        {
            m_checkDatagramQueueResetEvent.Reset();

            while (!m_serverClosed)
            {
                lock (m_sessionTable)
                {
                    foreach (TSession session in m_sessionTable.Values)
                    {
                        if (m_serverClosed)
                        {
                            break;
                        }

                        session.HandleDatagram();
                    }
                }
                Thread.Sleep(m_checkDatagramQueueTimeInterval);
            }

            m_checkDatagramQueueResetEvent.Set();
        }

        private void DisconnectSession(TSession session)
        {
            if (session.DisconnectType == TDisconnectType.Normal)
            {
                this.OnSessionDisconnected(session);
            }
            else if (session.DisconnectType == TDisconnectType.Timeout)
            {
                this.OnSessionTimeout(session);
            }
        }

        /// <summary>
        /// ���������Ϣ
        /// </summary>
        private void OnShowDebugMessage(string message)
        {
            if (this.ShowDebugMessage != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(message);
                this.ShowDebugMessage(this, e);
            }
        }

        #endregion

        #region  protected virtual methods

        protected virtual void OnDatabaseOpenException(object sender, TExceptionEventArgs e)
        {
            Interlocked.Increment(ref m_databaseExceptionCount);

            EventHandler<TExceptionEventArgs> handler = this.DatabaseOpenException;
            if (handler != null)
            {
                handler(sender, e);  // ת���¼��ļ�����
            }
        }

        protected virtual void OnDatabaseCloseException(object sender, TExceptionEventArgs e)
        {
            Interlocked.Increment(ref m_databaseExceptionCount);

            EventHandler<TExceptionEventArgs> handler = this.DatabaseCloseException;
            if (handler != null)
            {
                handler(sender, e);  // ת���¼��ļ�����
            }
        }

        protected virtual void OnDatabaseException(object sender, TExceptionEventArgs e)
        {
            Interlocked.Increment(ref m_databaseExceptionCount);

            EventHandler<TExceptionEventArgs> handler = this.DatabaseException;
            if (handler != null)
            {
                handler(sender, e);  // ת���¼��ļ�����
            }
        }

        protected virtual void OnSessionRejected()
        {
            EventHandler handler = this.SessionRejected;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual  void OnSessionConnected(TSession session)
        {
            Interlocked.Increment(ref m_sessionCount);

            EventHandler<TSessionEventArgs> handler = this.SessionConnected;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(session);
                handler(this, e);
            }
        }

        protected virtual void OnSessionDisconnected(TSession session)
        {
            Interlocked.Decrement(ref m_sessionCount);

            EventHandler<TSessionEventArgs> handler = this.SessionDisconnected;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(session);
                handler(this, e);
            }
        }

        protected virtual void OnSessionTimeout(TSession session)
        {
            Interlocked.Decrement(ref m_sessionCount);

            EventHandler<TSessionEventArgs> handler = this.SessionTimeout;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(session);
                handler(this, e);
            }
        }

        protected virtual void OnSessionReceiveException(object sender,  TSessionExceptionEventArgs e)
        {
            Interlocked.Decrement(ref m_sessionCount);
            Interlocked.Increment(ref m_sessionExceptionCount);

            EventHandler<TSessionExceptionEventArgs> handler = this.SessionReceiveException;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnSessionSendException(object sender, TSessionExceptionEventArgs e)
        {
            Interlocked.Decrement(ref m_sessionCount);
            Interlocked.Increment(ref m_sessionExceptionCount);

            EventHandler<TSessionExceptionEventArgs> handler = this.SessionSendException;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnServerException(Exception err)
        {
            Interlocked.Increment(ref m_serverExceptCount);

            EventHandler<TExceptionEventArgs> handler = this.ServerException;
            if (handler != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(err);
                handler(this, e);
            }
        }

        protected virtual void OnServerStarted()
        {
            EventHandler handler = this.ServerStarted;
            if(handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnServerListenPaused()
        {
            EventHandler handler = this.ServerListenPaused;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnServerListenResumed()
        {
            EventHandler handler = this.ServerListenResumed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnServerClosed()
        {
            EventHandler handler = this.ServerClosed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected virtual void OnDatagramDelimiterError(object sender, TSessionEventArgs e)
        {
            Interlocked.Increment(ref m_receivedDatagramCount);
            Interlocked.Increment(ref m_errorDatagramCount);

            EventHandler<TSessionEventArgs> handler = this.DatagramDelimiterError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDatagramOversizeError(object sender, TSessionEventArgs e)
        {
            Interlocked.Increment(ref m_receivedDatagramCount);
            Interlocked.Increment(ref m_errorDatagramCount);

            EventHandler<TSessionEventArgs> handler = this.DatagramOversizeError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDatagramAccepted(object sender, TSessionEventArgs e)
        {
            Interlocked.Increment(ref m_receivedDatagramCount);
            Interlocked.Increment(ref m_datagramQueueLength);

            EventHandler<TSessionEventArgs> handler = this.DatagramAccepted;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDatagramError(object sender, TSessionEventArgs e)
        {
            Interlocked.Increment(ref m_errorDatagramCount);
            Interlocked.Decrement(ref m_datagramQueueLength);

            EventHandler<TSessionEventArgs> handler = this.DatagramError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void OnDatagramHandled(object sender, TSessionEventArgs e)
        {
            Interlocked.Decrement(ref m_datagramQueueLength);

            EventHandler<TSessionEventArgs> handler = this.DatagramHandled;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        #endregion

    }

    /// <summary>
    /// �Ự����ĳ�Ա
    /// </summary>
    public class TSessionCoreInfo
    {
        #region  member fields

        private int m_id;
        private string m_ip = string.Empty;
        private string m_name = string.Empty;
        private TSessionState m_state = TSessionState.Active;
        private TDisconnectType m_disconnectType = TDisconnectType.Normal;

        private DateTime m_loginTime;
        private DateTime m_lastSessionTime;

        #endregion

        #region  public properties

        public int ID
        {
            get { return m_id; }
            protected set { m_id = value; }
        }

        public string IP
        {
            get { return m_ip; }
            protected set { m_ip = value; }
        }

        /// <summary>
        /// ���ݰ������ߵ�����/���
        /// </summary>
        public string Name
        {
            get { return m_name; }
            protected set { m_name = value; }
        }

        public DateTime LoginTime
        {
            get { return m_loginTime; }
            protected set 
            { 
                m_loginTime = value;
                m_lastSessionTime = value;
            }
        }

        public DateTime LastSessionTime
        {
            get { return m_lastSessionTime; }
            protected set { m_lastSessionTime = value; }
        }

        public TSessionState State
        {
            get { return m_state; }
            protected set
            {
                lock (this)
                {
                    m_state = value;
                }
            }
        }

        public TDisconnectType DisconnectType
        {
            get { return m_disconnectType; }
            protected set
            {
                lock (this)
                {
                    m_disconnectType = value;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// �Ự����(������, ����ʵ���� AnalyzeDatagram ����)
    /// </summary>
    public abstract class TSessionBase: TSessionCoreInfo, ISessionEvent
    {
        #region  member fields

        private Socket m_socket;
        private int m_maxDatagramSize;

        private BufferManager m_bufferManager;
                
        private int m_recvBufferOffSet;
        private int m_sendBufferOffSet;

        private byte[] m_datagramBuffer;

        private TDatabaseBase m_databaseObj;
        private Queue<byte[]> m_datagramQueue;

        #endregion

        #region class events

        public event EventHandler<TSessionExceptionEventArgs> SessionReceiveException;
        public event EventHandler<TSessionExceptionEventArgs> SessionSendException;
        public event EventHandler<TSessionEventArgs> DatagramDelimiterError;
        public event EventHandler<TSessionEventArgs> DatagramOversizeError;
        public event EventHandler<TSessionEventArgs> DatagramAccepted;
        public event EventHandler<TSessionEventArgs> DatagramError;
        public event EventHandler<TSessionEventArgs> DatagramHandled;
        
        public event EventHandler<TExceptionEventArgs> ShowDebugMessage;

        #endregion

        #region  class constructor
        /// <summary>
        /// �����Ͳ�������ʱ, �������޲ι��캯��
        /// </summary>
        protected TSessionBase() { }

        /// <summary>
        /// �湹�캯����ʼ������
        /// </summary>
        public virtual void Initiate(int maxDatagramsize, int id, Socket socket, TDatabaseBase database, BufferManager bufferManager)
        {
            base.ID = id;
            base.LoginTime = DateTime.Now;

            m_bufferManager = bufferManager;

            m_recvBufferOffSet = bufferManager.GetRecvBuffer();
            m_sendBufferOffSet = bufferManager.GetSendBuffer();

            m_maxDatagramSize = maxDatagramsize;

            m_socket = socket;
            m_databaseObj = database;

            m_datagramQueue = new Queue<byte[]>();

            if (m_socket != null)
            {
                IPEndPoint iep = m_socket.RemoteEndPoint as IPEndPoint;
                if (iep != null)
                {
                    base.IP = iep.Address.ToString();
                }
            }
        }

        #endregion

        #region  properties

        public TDatabaseBase DatabaseObj
        {
            get { return m_databaseObj; }
        }

        #endregion

        #region  public methods

        public void Shutdown()
        {
            lock (this)
            {
                if (this.State != TSessionState.Inactive || m_socket == null)  // Inactive ״̬���� Shutdown
                {
                    return;
                }

                this.State = TSessionState.Shutdown;
                try
                {
                    m_socket.Shutdown(SocketShutdown.Both);  // Ŀ�ģ������첽�¼�
                }
                catch (Exception) { }
            }
        }

        public void Close()
        {
            lock (this)
            {
                if (this.State != TSessionState.Shutdown || m_socket == null)  // Shutdown ״̬���� Close
                {
                    return;
                }

                m_datagramBuffer = null;

                if (m_datagramQueue != null)
                {
                    while (m_datagramQueue.Count > 0)
                    {

                        m_datagramQueue.Dequeue();
                    }
                    m_datagramQueue.Clear();
                }

                m_bufferManager.FreeRecvBuffer(m_recvBufferOffSet);
                m_bufferManager.FreeSendBuffer(m_sendBufferOffSet);

                try
                {
                    this.State = TSessionState.Closed;
                    m_socket.Close();
                }
                catch (Exception) { }
            }
        }

        public void SetInactive()
        {
            lock (this)
            {
                if (this.State == TSessionState.Active)
                {
                    this.State = TSessionState.Inactive;
                    this.DisconnectType = TDisconnectType.Normal;
                }
            }
        }

        public void HandleDatagram()
        {
            lock (this)
            {
                if (this.State != TSessionState.Active || m_datagramQueue.Count == 0)
                {
                    return;
                }

                byte[] datagramBytes = m_datagramQueue.Dequeue();
                this.AnalyzeDatagram(datagramBytes);
            }
        }

        public void ReceiveDatagram()
        {
            lock (this)
            {
                if (this.State != TSessionState.Active)
                {
                    return;
                }

                try  // һ���ͻ������������� �����Ӻ������Ͽ��������ڸô���������ϵͳ����Ϊ�Ǵ���
                {
                    // ��ʼ�������Ըÿͻ��˵�����
                    byte[] recvBuffer = m_bufferManager.RecvBuffer;
                    m_socket.BeginReceive(recvBuffer, m_recvBufferOffSet, m_bufferManager.RecvBufferSize, SocketFlags.None, this.EndReceiveDatagram, this);

                }
                catch (Exception err)  // �� Socket �쳣��׼���رոûỰ
                {
                    this.DisconnectType = TDisconnectType.Exception;
                    this.State = TSessionState.Inactive;

                    this.OnSessionReceiveException(err);
                }
            }
        }

        public void SendDatagram(string datagramText)
        {
            lock (this)
            {
                if (this.State != TSessionState.Active)
                {
                    return;
                }

                try
                {

                    int byteLength = Encoding.ASCII.GetByteCount(datagramText);
                    if (byteLength <= m_bufferManager.SendBufferSize)
                    {
                        byte[] sendBuffer = m_bufferManager.SendBuffer;
                        Encoding.ASCII.GetBytes(datagramText, 0, byteLength, sendBuffer, m_sendBufferOffSet);
                        m_socket.BeginSend(sendBuffer, m_sendBufferOffSet, byteLength, SocketFlags.None, this.EndSendDatagram, this);
                    }
                    else
                    {
                        byte[] data = Encoding.ASCII.GetBytes(datagramText);  // ��������ֽ�����
                        m_socket.BeginSend(data, 0, data.Length, SocketFlags.None, this.EndSendDatagram, this);
                    }
                }
                catch (Exception err)  // д socket �쳣��׼���رոûỰ
                {
                    this.DisconnectType = TDisconnectType.Exception;
                    this.State = TSessionState.Inactive;

                    this.OnSessionSendException(err);
                }
            }
        }

        public void CheckTimeout(int maxSessionTimeout)
        {
            TimeSpan ts = DateTime.Now.Subtract(this.LastSessionTime);
            int elapsedSecond = Math.Abs((int)ts.TotalSeconds);

            if (elapsedSecond > maxSessionTimeout)  // ��ʱ����׼���Ͽ�����
            {
                this.DisconnectType = TDisconnectType.Timeout;
                this.SetInactive();  // ���Ϊ���رա�׼���Ͽ�
            }
        }

        #endregion

        #region  private methods

        /// <summary>
        /// ����������ɴ�����, iar ΪĿ��ͻ��� Session
        /// </summary>
        private void EndSendDatagram(IAsyncResult iar)
        {
            lock (this)
            {
                if (this.State != TSessionState.Active)
                {
                    return;
                }

                if (!m_socket.Connected)
                {
                    this.SetInactive();
                    return;
                }

                try
                {
                    m_socket.EndSend(iar);
                    iar.AsyncWaitHandle.Close();
                }
                catch (Exception err)  // д socket �쳣��׼���رոûỰ
                {
                    this.DisconnectType = TDisconnectType.Exception;
                    this.State = TSessionState.Inactive;

                    this.OnSessionSendException(err);
                }
            }
        }

        private void EndReceiveDatagram(IAsyncResult iar)
        {
            lock (this)
            {
                if (this.State != TSessionState.Active)
                {
                    return;
                }

                if (!m_socket.Connected)
                {
                    this.SetInactive();
                    return;
                }

                try
                {
                    // Shutdown ʱ������ ReceiveData����ʱҲ�����յ� 0 �����ݰ�
                    int readBytesLength = m_socket.EndReceive(iar);
//                    iar.AsyncWaitHandle.Close();

                    if (readBytesLength == 0)
                    {
                        this.DisconnectType = TDisconnectType.Normal;
                        this.State = TSessionState.Inactive;
                    }
                    else  // �������ݰ�
                    {
                        this.LastSessionTime = DateTime.Now;                        
                        // �ϲ����ģ�������ͷ��β�ַ���־��ȡ���ģ������������ݴ�����
                        this.ResolveSessionBuffer(readBytesLength);
                        this.ReceiveDatagram();  // ��������
                    }
                }
                catch (Exception err)  // �� socket �쳣���رոûỰ��ϵͳ����Ϊ�Ǵ������ִ������̫�ࣩ
                {
                    if (this.State == TSessionState.Active)
                    {
                        this.DisconnectType = TDisconnectType.Exception;
                        this.State = TSessionState.Inactive;

                        this.OnSessionReceiveException(err);
                    }
                }
            }
        }

        /// <summary>
        /// �������ջ����������ݵ����ݻ�����������ζ�һ�����ģ�
        /// </summary>
        private void CopyToDatagramBuffer(int start, int length)
        {
            int datagramLength = 0;
            if (m_datagramBuffer != null)
            {
                datagramLength = m_datagramBuffer.Length;
            }

            byte[] recvBuffer = m_bufferManager.RecvBuffer;
            Array.Resize(ref m_datagramBuffer, datagramLength + length);  // �������ȣ�m_datagramBuffer Ϊ null ������
            Array.Copy(recvBuffer, start, m_datagramBuffer, datagramLength, length);  // ���������ݰ�������
        }

        #endregion

        #region protected methods
        
        /// <summary>
        /// ��ȡ��ʱ������������أ�����ʵ�ʹ����ض���
        /// </summary>
        protected virtual void ResolveSessionBuffer(int readBytesLength)
        {

            // �ϴ����°��ķǿ�, ��Ȼ����ʼ�ַ�<
            bool hasHeadDelimiter = (m_datagramBuffer != null);
            
            int headDelimiter = 1;
            int tailDelimiter = 1;

            byte[] recvBuffer = m_bufferManager.RecvBuffer;
            int start = m_recvBufferOffSet;   // m_recvBuffer �������а���ʼλ��
            int length = 0;  // �Ѿ������Ľ��ջ���������

            int subIndex = m_recvBufferOffSet;  // �������±�
            while (subIndex < readBytesLength + m_recvBufferOffSet)
            {
                if (recvBuffer[subIndex] == '<')  // ���ݰ���ʼ�ַ�<��ǰ���������
                {
                    if (hasHeadDelimiter || length > 0)  // ��� < ǰ�������ݣ�����Ϊ�����
                    {
                        this.OnDatagramDelimiterError();
                    }

                    m_datagramBuffer = null;  // ��հ�����������ʼһ���µİ�

                    start = subIndex;         // �°���㣬��<����λ��
                    length = headDelimiter;   // �°��ĳ��ȣ���<��
                    hasHeadDelimiter = true;  // �°��п�ʼ�ַ�
                }
                else if (recvBuffer[subIndex] == '>')  // ���ݰ��Ľ����ַ�>
                {
                    if (hasHeadDelimiter)  // �������������п�ʼ�ַ�<
                    {
                        length += tailDelimiter;  // ���Ȱ��������ַ���>��

                        this.GetDatagramFromBuffer(start, length); // >ǰ���Ϊ��ȷ��ʽ�İ�

                        start = subIndex + tailDelimiter;  // �°���㣨һ��һ�δ�������ѭ����
                        length = 0;  // �°�����
                    }
                    else  // >ǰ��û�п�ʼ�ַ�����ʱ��Ϊ�����ַ�>Ϊһ���ַ����������Ĵ��������
                    {
                        length++;  //  hasHeadDelimiter = false;
                    }
                }
                else  // ���� < Ҳ�� >�� ��һ���ַ������� + 1
                {
                    length++;
                }
                ++subIndex;
            }

            if (length > 0)  // ʣ�µĴ����������������
            {
                int mergedLength = length;
                if (m_datagramBuffer != null)
                {
                    mergedLength += m_datagramBuffer.Length;
                }
                
                // ʣ�µİ��ĺ����ַ��Ҳ�������ת�浽���Ļ������У����´δ���
                if (hasHeadDelimiter && mergedLength <= m_maxDatagramSize)
                {
                    this.CopyToDatagramBuffer(start, length);
                }
                else  // �������ַ��򳬳�
                {
                    this.OnDatagramOversizeError();
                    m_datagramBuffer = null;  // ����ȫ������
                }
            }
        }

        /// <summary>
        /// Session��д���, ��������: 
        /// 1) �жϰ���Ч���������(ע�⣺������ֹ����); 
        /// 2) �ֽ���еĸ��ֶ�����
        /// 3) У�������������Ч��
        /// 4) ����ȷ����Ϣ���ͻ���(���÷��� SendDatagram())
        /// 5) �洢�����ݵ����ݿ���
        /// 6) �洢��ԭ�ĵ����ݿ���(��ѡ)
        /// 7) �����ֶ�m_name, ��ʾ���ݰ������ߵ�����/���
        /// 8) ������ط���
        /// </summary>
        protected abstract void AnalyzeDatagram(byte[] datagramBytes);

        protected virtual void GetDatagramFromBuffer(int startPos, int len)
        {
            byte[] recvBuffer = m_bufferManager.RecvBuffer;
            byte[] datagramBytes;
            if (m_datagramBuffer != null)
            {
                datagramBytes = new byte[len + m_datagramBuffer.Length];
                Array.Copy(m_datagramBuffer, 0, datagramBytes, 0, m_datagramBuffer.Length);  // �ȿ��� Session �����ݻ�����������
                Array.Copy(recvBuffer, startPos, datagramBytes, m_datagramBuffer.Length, len);  // �ٿ��� Session �Ľ��ջ�����������
            }
            else
            {
                datagramBytes = new byte[len];
                Array.Copy(recvBuffer, startPos, datagramBytes, 0, len);  // �ٿ��� Session �Ľ��ջ�����������
            }

            if (m_datagramBuffer != null)
            {
                m_datagramBuffer = null;
            }

            m_datagramQueue.Enqueue(datagramBytes);
        }

        protected virtual void OnDatagramDelimiterError()
        {
            EventHandler<TSessionEventArgs> handler = this.DatagramDelimiterError;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(this);
                handler(this, e);
            }
        }

        protected virtual void OnDatagramOversizeError()
        {
            EventHandler<TSessionEventArgs> handler = this.DatagramOversizeError;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(this);
                handler(this, e);
            }
        }

        protected virtual void OnDatagramAccepted()
        {
            EventHandler<TSessionEventArgs> handler = this.DatagramAccepted;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(this);
                handler(this, e);
            }
        }

        protected virtual void OnDatagramError()
        {
            EventHandler<TSessionEventArgs> handler = this.DatagramError;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(this);
                handler(this, e);
            }
        }

        protected virtual void OnDatagramHandled()
        {
            EventHandler<TSessionEventArgs> handler = this.DatagramHandled;
            if (handler != null)
            {
                TSessionEventArgs e = new TSessionEventArgs(this);
                handler(this, e);
            }
        }

        protected virtual void OnSessionReceiveException(Exception err)
        {
            EventHandler<TSessionExceptionEventArgs> handler = this.SessionReceiveException;
            if (handler != null)
            {
                TSessionExceptionEventArgs e = new TSessionExceptionEventArgs(err, this);
                handler(this, e);
            }
        }

        protected virtual void OnSessionSendException(Exception err)
        {
            EventHandler<TSessionExceptionEventArgs> handler = this.SessionSendException;
            if (handler != null)
            {
                TSessionExceptionEventArgs e = new TSessionExceptionEventArgs(err, this);
                handler(this, e);
            }
        }

        protected void OnShowDebugMessage(string message)
        {
            if (this.ShowDebugMessage != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(message);
                this.ShowDebugMessage(this, e);
            }
        }

        #endregion 
    }

    /// <summary>
    /// ���ͺͽ��չ���������
    /// </summary>
    public sealed class BufferManager
    {
        private byte[] m_recvBuffer;
        private byte[] m_sendBuffer;

        private int m_maxSessionCount;
        
        private int m_recvBufferSize;
        private int m_sendBufferSize;

        private int m_totalRecvLength;
        private int m_totalSendLength;

        private int m_currentRecvIndex;
        private int m_currentSendIndex;

        private Stack<int> m_freeRecvIndexStack;
        private Stack<int> m_freeSendIndexStack;

        public BufferManager(int maxSessionCount, int recvBufferSize, int sendBufferSize)
        {
            m_maxSessionCount = maxSessionCount;

            m_recvBufferSize = recvBufferSize;
            m_sendBufferSize = sendBufferSize;

            m_currentRecvIndex = 0;
            m_currentSendIndex = 0;

            m_freeRecvIndexStack = new Stack<int>();
            m_freeSendIndexStack = new Stack<int>();

            m_totalRecvLength = m_recvBufferSize * m_maxSessionCount;
            m_totalSendLength = m_sendBufferSize * m_maxSessionCount;

            m_recvBuffer = new byte[m_totalRecvLength];
            m_sendBuffer = new byte[m_totalSendLength];
        }

        public int RecvBufferSize
        {
            get { return m_recvBufferSize; }
        }

        public int SendBufferSize
        {
            get { return m_sendBufferSize; }
        }

        public byte[] RecvBuffer
        {
            get { return m_recvBuffer; }
        }

        public byte[] SendBuffer
        {
            get { return m_sendBuffer; }
        }

        public  void FreeRecvBuffer(int recvOffSet)
        {
            m_freeRecvIndexStack.Push(recvOffSet);
        }

        public  void FreeSendBuffer(int sendOffSet)
        {
            m_freeSendIndexStack.Push(sendOffSet);
        }

        public int GetRecvBuffer()
        {
            int index = -1;
            lock (this)
            {
                if (m_freeRecvIndexStack.Count > 0)  // ���ͷŵĻ����
                {
                    index = m_freeRecvIndexStack.Pop();
                }
                else
                {
                    if (m_totalRecvLength >= m_currentRecvIndex + m_recvBufferSize)  // �пռ���
                    {
                        index = m_currentRecvIndex;
                        m_currentRecvIndex += m_recvBufferSize;  // �������ÿ�ָ��
                    }
                }
            }
            return index;
        }

        public int GetSendBuffer()
        {
            int index = -1;

            lock (this)
            {
                if (m_freeSendIndexStack.Count > 0)  // ���ͷŵĻ����
                {
                    index = m_freeSendIndexStack.Pop();
                }
                else
                {
                    if (m_totalSendLength >= m_currentSendIndex + m_sendBufferSize)  // �пռ���
                    {
                        index = m_currentSendIndex;
                        m_currentSendIndex += m_sendBufferSize;  // �������ÿ�ָ��
                    }
                }
            }

            return index;
        }
    }

    /// <summary>
    /// ���ݿ������, ֻ�����˼������󷽷�, ��������Ҫ����ʵ��
    /// 1) Open����, ���������SqlConnection/OleDbConnection
    /// 2) �������󷽷�, ��Щʵ��Ҫ�� TSocketServerBase �е���
    /// 3) �Ѿ����������������ࣺTSqlServerBase/TOleDatabaseBase
    /// </summary>
    public abstract class TDatabaseBase: IDatabaseEvent
    {
        private string m_dbConnectionString = string.Empty;
        private DbConnection m_dbConnection;

        public event EventHandler<TExceptionEventArgs> DatabaseOpenException;
        public event EventHandler<TExceptionEventArgs> DatabaseException;
        public event EventHandler<TExceptionEventArgs> DatabaseCloseException;

        /// <summary>
        /// �����Ͳ�������ʱ, �������޲ι��캯��
        /// </summary>
        protected TDatabaseBase() { }

        public ConnectionState State
        {
            get { return m_dbConnection.State; }
        }

        /// <summary>
        /// ��Session���󷽷�AnalyzeDatagram��Ҫ�����Ӷ���
        /// </summary>
        public virtual DbConnection DbConnection
        {
            get { return m_dbConnection; }
            protected set { m_dbConnection = value; }
        }

        public string DbConnectionString
        {
            get { return m_dbConnectionString; }
        }

        /// <summary>
        /// 1) �湹�캯����ʼ������
        /// 2) dbConnectionString �����ݿ����Ӵ�
        /// </summary>
        public void Initiate(string dbConnectionString)
        {
            m_dbConnectionString = dbConnectionString;
        }

        /// <summary>
        /// ���󷽷�, ��дʱ��Ҫ:
        /// 1) �����������͵����Ӷ���
        ///    (1) Ole���ݿ����ӣ�m_dbConnection = new OleDbConnection();
        ///    (2) SqlServer���ӣ�m_dbConnection = new SqlConnection();
        /// 2) ����������������Ӷ���Ķ����磺SqlCommand/OleDbCommand��
        /// </summary>
        public abstract void Open();

        public virtual void Store(byte[] datagramBytes, TSessionBase session) { }

        public void Close()
        {
            if (m_dbConnection == null)
            {
                return;
            }

            try
            {
                this.Clear();  // ����������������Դ
                m_dbConnection.Close();
            }
            catch (Exception err)
            {
                this.OnDatabaseCloseException(err);
            }
        }

        /// <summary>
        /// 1) �ر����ݿ�ǰ���������(Connection)��Դ
        /// 2) ��������������д�÷���
        /// </summary>
        protected virtual void Clear() { }

        protected virtual void OnDatabaseOpenException(Exception err)
        {
            EventHandler<TExceptionEventArgs> handler = this.DatabaseOpenException;
            if (handler != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(err);
                handler(this, e);
            }
        }

        protected virtual void OnDatabaseCloseException(Exception err)
        {
            EventHandler<TExceptionEventArgs> handler = this.DatabaseCloseException;
            if (handler != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(err);
                handler(this, e);
            }
        }

        /// <summary>
        /// Session�д������¼�
        /// </summary>
        protected virtual void OnDatabaseException(Exception err)
        {
            EventHandler<TExceptionEventArgs> handler = this.DatabaseException;
            if (handler != null)
            {
                TExceptionEventArgs e = new TExceptionEventArgs(err);
                handler(this, e);
            }
        }

    }

    /// <summary>
    /// SqlServer���ݿ���, �����������������������ֶ�
    /// </summary>
    public class TSqlServerBase : TDatabaseBase
    {
        public override DbConnection DbConnection
        {
            get
            {
                SqlConnection dbConn = base.DbConnection as SqlConnection;
                return dbConn;
            }
        }

        public override void Open()
        {
            try
            {
                this.Close();

                base.DbConnection = new SqlConnection(base.DbConnectionString);
                base.DbConnection.Open();
            }
            catch (Exception err)
            {
                this.OnDatabaseOpenException(err);
            }
        }
    }

    /// <summary>
    /// OldDb���ݿ���, �����������������������ֶ�
    /// </summary>
    public class TOleDatabaseBase : TDatabaseBase
    {
        public override DbConnection DbConnection
        {
            get
            {
                OleDbConnection dbConn = base.DbConnection as OleDbConnection;
                return dbConn;
            }
        }

        public override void Open()
        {
            try
            {
                this.Close();
                
                base.DbConnection = new OleDbConnection(base.DbConnectionString);
                base.DbConnection.Open();
            }
            catch (Exception err)
            {
                this.OnDatabaseOpenException(err);
            }
        }
    }


    public interface ISessionEvent
    {
        event EventHandler<TSessionExceptionEventArgs> SessionReceiveException;
        event EventHandler<TSessionExceptionEventArgs> SessionSendException;
        event EventHandler<TSessionEventArgs> DatagramDelimiterError;
        event EventHandler<TSessionEventArgs> DatagramOversizeError;
        event EventHandler<TSessionEventArgs> DatagramAccepted;
        event EventHandler<TSessionEventArgs> DatagramError;
        event EventHandler<TSessionEventArgs> DatagramHandled;
    }

    public interface IDatabaseEvent
    {
        event EventHandler<TExceptionEventArgs> DatabaseOpenException;
        event EventHandler<TExceptionEventArgs> DatabaseException;
        event EventHandler<TExceptionEventArgs> DatabaseCloseException;
    }

    public class TExceptionEventArgs: EventArgs
    {
        private string m_exceptionMessage;

        public TExceptionEventArgs(Exception exception)
        {
            m_exceptionMessage = exception.Message;
        }

        public TExceptionEventArgs(string exceptionMessage)
        {
            m_exceptionMessage = exceptionMessage;
        }

        public string ExceptionMessage
        {
            get { return m_exceptionMessage; }
        }
    }

    public class TSessionEventArgs: EventArgs
    {
        TSessionCoreInfo m_sessionBaseInfo;

        public TSessionEventArgs(TSessionCoreInfo sessionCoreInfo)
        {
            m_sessionBaseInfo = sessionCoreInfo;
        }

        public TSessionCoreInfo SessionBaseInfo
        {
            get { return m_sessionBaseInfo; }
        }
    }

    public class TSessionExceptionEventArgs : TSessionEventArgs
    {
        private string m_exceptionMessage;

        public TSessionExceptionEventArgs(Exception exception, TSessionCoreInfo sessionCoreInfo)
            : base(sessionCoreInfo)
        {
            m_exceptionMessage = exception.Message;
        }

        public string ExceptionMessage
        {
            get { return m_exceptionMessage; }
        }
    }

    public enum TDisconnectType
    {
        Normal,     // disconnect normally
        Timeout,    // disconnect because of timeout
        Exception   // disconnect because of exception
    }

    public enum TSessionState
    {
        Active,    // state is active
        Inactive,  // session is inactive and will be closed
        Shutdown,  // session is shutdownling
        Closed     // session is closed
    }
}