/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Apache.NMS.ActiveMQ.Commands;
using Apache.NMS.ActiveMQ.State;
using Apache.NMS.ActiveMQ.Threads;
using Apache.NMS.Util;

#if NETCF
using ThreadInterruptedException = System.Exception;
#endif


namespace Apache.NMS.ActiveMQ.Transport.Failover
{
	/// <summary>
	/// A Transport that is made reliable by being able to fail over to another
	/// transport when a transport failure is detected.
	/// </summary>
	public class FailoverTransport : ICompositeTransport, IComparable
	{

		private static int _idCounter = 0;
		private int _id;

		private bool disposed;
		private bool connected;
		private List<Uri> uris = new List<Uri>();
		private CommandHandler _commandHandler;
		private ExceptionHandler _exceptionHandler;
		private InterruptedHandler interruptedHandler;
		private ResumedHandler resumedHandler;

		private Mutex reconnectMutex = new Mutex();
		private Mutex backupMutex = new Mutex();
		private Mutex sleepMutex = new Mutex();
		private ConnectionStateTracker stateTracker = new ConnectionStateTracker();
		private Dictionary<int, Command> requestMap = new Dictionary<int, Command>();

		private Uri connectedTransportURI;
		private Uri failedConnectTransportURI;
		private AtomicReference<ITransport> connectedTransport = new AtomicReference<ITransport>(null);
		private TaskRunner reconnectTask = null;
		private bool started;

		private int _initialReconnectDelay = 10;
		private int _maxReconnectDelay = 1000 * 30;
		private int _backOffMultiplier = 2;
		private bool _useExponentialBackOff = true;
		private bool _randomize = true;
		private bool initialized;
		private int _maxReconnectAttempts;
		private int connectFailures;
		private int _reconnectDelay = 10;
		private Exception connectionFailure;
		private bool firstConnection = true;
		//optionally always have a backup created
		private bool _backup = false;
		private List<BackupTransport> backups = new List<BackupTransport>();
		private int _backupPoolSize = 1;
		private bool _trackMessages = false;
		private int _maxCacheSize = 256;
		private TimeSpan requestTimeout = NMSConstants.defaultRequestTimeout;
		private volatile Exception _failure;
		private readonly object _syncLock = new object();

		internal Exception Failure
		{
			get
			{
				return _failure;
			}

			set
			{
				lock(_syncLock)
				{
					_failure = value;
				}

			}
		}

		public TimeSpan RequestTimeout
		{
			get { return requestTimeout; }
			set { requestTimeout = value; }
		}

		private class FailoverTask : Task
		{
			private FailoverTransport parent;

			public FailoverTask(FailoverTransport p)
			{
				parent = p;
			}

			public bool iterate()
			{
				bool result = false;
				bool buildBackup = true;
				bool doReconnect = !parent.disposed;
				try
				{
					parent.backupMutex.WaitOne();
					if(parent.ConnectedTransport == null && doReconnect)
					{
						result = parent.doConnect();
						buildBackup = false;
					}
				}
				finally
				{
					parent.backupMutex.ReleaseMutex();
				}

				if(buildBackup)
				{
					parent.buildBackups();
				}
				else
				{
					//build backups on the next iteration
					result = true;
					try
					{
						parent.reconnectTask.wakeup();
					}
					catch(ThreadInterruptedException)
					{
						Tracer.Debug("Reconnect task has been interrupted.");
					}
				}
				return result;
			}
		}

		public FailoverTransport()
		{
			_id = _idCounter++;

			stateTracker.TrackTransactions = true;
		}

		~FailoverTransport()
		{
			Dispose(false);
		}

		public void onCommand(ITransport sender, Command command)
		{
			if(command != null)
			{
				if(command.IsResponse)
				{
					Object oo = null;
					lock(((ICollection) requestMap).SyncRoot)
					{
						int v = ((Response) command).CorrelationId;
						try
						{
							oo = requestMap[v];
							requestMap.Remove(v);
						}
						catch
						{
						}
					}

					Tracked t = oo as Tracked;
					if(t != null)
					{
						t.onResponses();
					}
				}

				if(!initialized)
				{
					if(command.IsBrokerInfo)
					{
						BrokerInfo info = (BrokerInfo) command;
						BrokerInfo[] peers = info.PeerBrokerInfos;
						if(peers != null)
						{
							for(int i = 0; i < peers.Length; i++)
							{
								String brokerString = peers[i].BrokerURL;
								add(brokerString);
							}
						}

						initialized = true;
					}
				}
			}

			this.Command(sender, command);
		}

		public void onException(ITransport sender, Exception error)
		{
			try
			{
				handleTransportFailure(error);
			}
			catch(Exception e)
			{
				e.GetType();
				// What to do here?
			}
		}

		public void disposedOnCommand(ITransport sender, Command c)
		{
		}

		public void disposedOnException(ITransport sender, Exception e)
		{
		}

		public void handleTransportFailure(Exception e)
		{
			ITransport transport = connectedTransport.GetAndSet(null);
			if(transport != null)
			{
				transport.Command = new CommandHandler(disposedOnCommand);
				transport.Exception = new ExceptionHandler(disposedOnException);
				try
				{
					transport.Stop();
				}
				catch(Exception ex)
				{
					ex.GetType();	// Ignore errors but this lets us see the error during debugging
				}

				try
				{
					reconnectMutex.WaitOne();
					bool reconnectOk = false;
					if(started)
					{
						Tracer.WarnFormat("Transport failed to {0}, attempting to automatically reconnect due to: {1}", ConnectedTransportURI.ToString(), e.Message);
						reconnectOk = true;
					}

					initialized = false;
					failedConnectTransportURI = ConnectedTransportURI;
					ConnectedTransportURI = null;
					connected = false;
					if(reconnectOk)
					{
						reconnectTask.wakeup();
					}
				}
				finally
				{
					reconnectMutex.ReleaseMutex();
				}

				if(this.Interrupted != null)
				{
					this.Interrupted(transport);
				}
			}
		}

		public void Start()
		{
			try
			{
				reconnectMutex.WaitOne();
				if(started)
				{
					Tracer.Debug("FailoverTransport Already Started.");
					return;
				}

				Tracer.Debug("FailoverTransport Started.");
				started = true;
				stateTracker.MaxCacheSize = MaxCacheSize;
				stateTracker.TrackMessages = TrackMessages;
				if(ConnectedTransport != null)
				{
					stateTracker.DoRestore(ConnectedTransport);
				}
				else
				{
					Reconnect();
				}
			}
			finally
			{
				reconnectMutex.ReleaseMutex();
			}
		}

		public virtual void Stop()
		{
			ITransport transportToStop = null;
			try
			{
				reconnectMutex.WaitOne();
				if(!started)
				{
					Tracer.Debug("FailoverTransport Already Stopped.");
					return;
				}

				Tracer.Debug("FailoverTransport Stopped.");
				started = false;
				disposed = true;
				connected = false;
				foreach(BackupTransport t in backups)
				{
					t.Disposed = true;
				}
				backups.Clear();

				if(ConnectedTransport != null)
				{
					transportToStop = connectedTransport.GetAndSet(null);
				}
			}
			finally
			{
				reconnectMutex.ReleaseMutex();
			}

			try
			{
				sleepMutex.WaitOne();
			}
			finally
			{
				sleepMutex.ReleaseMutex();
			}

			if(reconnectTask != null)
			{
				reconnectTask.shutdown();
			}

			if(transportToStop != null)
			{
				transportToStop.Stop();
			}
		}

		public int InitialReconnectDelay
		{
			get { return _initialReconnectDelay; }
			set { _initialReconnectDelay = value; }
		}

		public int MaxReconnectDelay
		{
			get { return _maxReconnectDelay; }
			set { _maxReconnectDelay = value; }
		}

		public int ReconnectDelay
		{
			get { return _reconnectDelay; }
			set { _reconnectDelay = value; }
		}

		public int ReconnectDelayExponent
		{
			get { return _backOffMultiplier; }
			set { _backOffMultiplier = value; }
		}

		public ITransport ConnectedTransport
		{
			get { return connectedTransport.Value; }
			set { connectedTransport.Value = value; }
		}

		public Uri ConnectedTransportURI
		{
			get { return connectedTransportURI; }
			set { connectedTransportURI = value; }
		}

		public int MaxReconnectAttempts
		{
			get { return _maxReconnectAttempts; }
			set { _maxReconnectAttempts = value; }
		}

		public bool Randomize
		{
			get { return _randomize; }
			set { _randomize = value; }
		}

		public bool Backup
		{
			get { return _backup; }
			set { _backup = value; }
		}

		public int BackupPoolSize
		{
			get { return _backupPoolSize; }
			set { _backupPoolSize = value; }
		}

		public bool TrackMessages
		{
			get { return _trackMessages; }
			set { _trackMessages = value; }
		}

		public int MaxCacheSize
		{
			get { return _maxCacheSize; }
			set { _maxCacheSize = value; }
		}

		/// <summary>
		/// </summary>
		/// <param name="command"></param>
		/// <returns>Returns true if the command is one sent when a connection is being closed.</returns>
		private bool IsShutdownCommand(Command command)
		{
			return (command != null && (command.IsShutdownInfo || command is RemoveInfo));
		}

		public void Oneway(Command command)
		{
			Exception error = null;
			try
			{
				reconnectMutex.WaitOne();

				if(IsShutdownCommand(command) && ConnectedTransport == null)
				{
					if(command.IsShutdownInfo)
					{
						// Skipping send of ShutdownInfo command when not connected.
						return;
					}

					if(command is RemoveInfo)
					{
						// Simulate response to RemoveInfo command
						Response response = new Response();
						response.CorrelationId = command.CommandId;
						onCommand(this, response);
						return;
					}
				}
				// Keep trying until the message is sent.
				for(int i = 0; !disposed; i++)
				{
					try
					{
						// Wait for transport to be connected.
						ITransport transport = ConnectedTransport;
						while(transport == null && !disposed
							&& connectionFailure == null
							// && !Thread.CurrentThread.isInterrupted()
							)
						{
							Tracer.Info("Waiting for transport to reconnect.");
							try
							{
								// Release so that the reconnect task can run
								reconnectMutex.ReleaseMutex();
								try
								{
									// Wait for something
									Thread.Sleep(1000);
								}
								catch(ThreadInterruptedException e)
								{
									Tracer.DebugFormat("Interrupted: {0}", e.Message);
								}
							}
							finally
							{
								reconnectMutex.WaitOne();
							}

							transport = ConnectedTransport;
						}

						if(transport == null)
						{
							// Previous loop may have exited due to use being disposed.
							if(disposed)
							{
								error = new IOException("Transport disposed.");
							}
							else if(connectionFailure != null)
							{
								error = connectionFailure;
							}
							else
							{
								error = new IOException("Unexpected failure.");
							}
							break;
						}

						// If it was a request and it was not being tracked by
						// the state tracker, then hold it in the requestMap so
						// that we can replay it later.
						Tracked tracked = stateTracker.track(command);
						lock(((ICollection) requestMap).SyncRoot)
						{
							if(tracked != null && tracked.WaitingForResponse)
							{
								requestMap.Add(command.CommandId, tracked);
							}
							else if(tracked == null && command.ResponseRequired)
							{
								requestMap.Add(command.CommandId, command);
							}
						}

						// Send the message.
						try
						{
							transport.Oneway(command);
							stateTracker.trackBack(command);
						}
						catch(Exception e)
						{

							// If the command was not tracked.. we will retry in
							// this method
							if(tracked == null)
							{

								// since we will retry in this method.. take it
								// out of the request map so that it is not
								// sent 2 times on recovery
								if(command.ResponseRequired)
								{
									lock(((ICollection) requestMap).SyncRoot)
									{
										requestMap.Remove(command.CommandId);
									}
								}

								// Rethrow the exception so it will handled by
								// the outer catch
								throw e;
							}

						}

						return;

					}
					catch(Exception e)
					{
						Tracer.DebugFormat("Send Oneway attempt: {0} failed.", i);
						handleTransportFailure(e);
					}
				}
			}
			finally
			{
				reconnectMutex.ReleaseMutex();
			}

			if(!disposed)
			{
				if(error != null)
				{
					throw error;
				}
			}
		}

		public Object Request(Object command)
		{
			throw new ApplicationException("FailoverTransport does not support Request(Object)");
		}

		public Object Request(Object command, int timeout)
		{
			throw new ApplicationException("FailoverTransport does not support Request(Object, Int)");
		}

		public void add(Uri[] u)
		{
			lock(uris)
			{
				for(int i = 0; i < u.Length; i++)
				{
					if(!uris.Contains(u[i]))
					{
						uris.Add(u[i]);
					}
				}
			}

			Reconnect();
		}

		public void remove(Uri[] u)
		{
			lock(uris)
			{
				for(int i = 0; i < u.Length; i++)
				{
					uris.Remove(u[i]);
				}
			}

			Reconnect();
		}

		public void add(String u)
		{
			try
			{
				Uri uri = new Uri(u);
				lock(uris)
				{
					if(!uris.Contains(uri))
					{
						uris.Add(uri);
					}
				}

				Reconnect();
			}
			catch(Exception e)
			{
				Tracer.ErrorFormat("Failed to parse URI '{0}': {1}", u, e.Message);
			}
		}

		public void Reconnect()
		{
			try
			{
				reconnectMutex.WaitOne();

				if(started)
				{
					if(reconnectTask == null)
					{
						Tracer.Debug("Creating reconnect task");
						reconnectTask = DefaultThreadPools.DefaultTaskRunnerFactory.CreateTaskRunner(new FailoverTask(this),
											"ActiveMQ Failover Worker: " + this.GetHashCode().ToString());
					}

					Tracer.Debug("Waking up reconnect task");
					try
					{
						reconnectTask.wakeup();
					}
					catch(ThreadInterruptedException)
					{
					}
				}
				else
				{
					Tracer.Debug("Reconnect was triggered but transport is not started yet. Wait for start to connect the transport.");
				}
			}
			finally
			{
				reconnectMutex.ReleaseMutex();
			}
		}

		private List<Uri> ConnectList
		{
			get
			{
				List<Uri> l = new List<Uri>(uris);
				bool removed = false;
				if(failedConnectTransportURI != null)
				{
					removed = l.Remove(failedConnectTransportURI);
				}

				if(Randomize)
				{
					// Randomly, reorder the list by random swapping
					Random r = new Random(DateTime.Now.Millisecond);
					for(int i = 0; i < l.Count; i++)
					{
						int p = r.Next(l.Count);
						Uri t = l[p];
						l[p] = l[i];
						l[i] = t;
					}
				}

				if(removed)
				{
					l.Add(failedConnectTransportURI);
				}

				return l;
			}
		}

		protected void restoreTransport(ITransport t)
		{
			Tracer.Info("Restoring previous transport connection.");
			t.Start();
			//send information to the broker - informing it we are an ft client
			ConnectionControl cc = new ConnectionControl();
			cc.FaultTolerant = true;
			t.Oneway(cc);
			stateTracker.DoRestore(t);

			Tracer.Info("Sending queued commands...");
			Dictionary<int, Command> tmpMap = null;
			lock(((ICollection) requestMap).SyncRoot)
			{
				tmpMap = new Dictionary<int, Command>(requestMap);
			}

			foreach(Command command in tmpMap.Values)
			{
				t.Oneway(command);
			}
		}

		public bool UseExponentialBackOff
		{
			get { return _useExponentialBackOff; }
			set { _useExponentialBackOff = value; }
		}

		public override String ToString()
		{
			return ConnectedTransportURI == null ? "unconnected" : ConnectedTransportURI.ToString();
		}

		public Uri RemoteAddress
		{
			get
			{
				if(ConnectedTransport != null)
				{
					return ConnectedTransport.RemoteAddress;
				}
				return null;
			}
		}

		public Object Narrow(Type type)
		{
			if(this.GetType().Equals(type))
			{
				return this;
			}
			else if(ConnectedTransport != null)
			{
				return ConnectedTransport.Narrow(type);
			}

			return null;
		}

		public bool IsFaultTolerant
		{
			get { return true; }
		}

		private bool _asyncConnect = false;

		/// <summary>
		/// Gets or sets a value indicating whether to asynchronously connect to sockets
		/// </summary>
		/// <value><c>true</c> if [async connect]; otherwise, <c>false</c>.</value>
		public bool AsyncConnect
		{
			set
			{
				_asyncConnect = value;
			}
		}

		private int _asyncTimeout = 45000;

		/// <summary>
		/// If doing an asynchronous connect, the milliseconds before timing out if no connection can be made
		/// </summary>
		/// <value>The async timeout.</value>
		public int AsyncTimeout
		{
			set
			{
				_asyncTimeout = value;
			}
		}

		private bool doConnect()
		{
			try
			{
				reconnectMutex.WaitOne();

				if(ConnectedTransport != null || disposed || connectionFailure != null)
				{
					return false;
				}
				else
				{
					List<Uri> connectList = ConnectList;
					if(connectList.Count == 0)
					{
						Failure = new NMSConnectionException("No URIs available for connection.");
					}
					else
					{
						if(!UseExponentialBackOff)
						{
							ReconnectDelay = InitialReconnectDelay;
						}

						try
						{
							backupMutex.WaitOne();
							if(Backup && backups.Count != 0)
							{
								BackupTransport bt = backups[0];
								backups.RemoveAt(0);
								ITransport t = bt.Transport;
								Uri uri = bt.Uri;
								t.Command = new CommandHandler(onCommand);
								t.Exception = new ExceptionHandler(onException);
								try
								{
									if(started)
									{
										restoreTransport(t);
									}
									ReconnectDelay = InitialReconnectDelay;
									failedConnectTransportURI = null;
									ConnectedTransportURI = uri;
									ConnectedTransport = t;
									connectFailures = 0;
									connected = true;
									if(this.Resumed != null)
									{
										this.Resumed(t);
									}
									Tracer.InfoFormat("Successfully reconnected to backup {0}", uri.ToString());
									return false;
								}
								catch(Exception e)
								{
									e.GetType();
									Tracer.Debug("Backup transport failed");
								}
							}
						}
						finally
						{
							backupMutex.ReleaseMutex();
						}

						ManualResetEvent allDone = new ManualResetEvent(false);
						ITransport transport = null;
						Uri chosenUri = null;
						object syncLock = new object();

						try
						{
							foreach(Uri uri in connectList)
							{
								if(ConnectedTransport != null || disposed)
								{
									break;
								}

								Tracer.DebugFormat("Attempting connect to: {0}", uri);

								if(_asyncConnect)
								{
									// set connector up
									Connector connector = new Connector(
										delegate(ITransport transportToUse, Uri uriToUse) {
											if(transport == null)
											{
												lock(syncLock)
												{
													if(transport == null)
													{
														//the transport has not yet been set asynchronously so set it
														transport = transportToUse;
														chosenUri = uriToUse;
													}
													//notify issuing thread to move on
													allDone.Set();
												}
											}
										}, uri, this);

									// initiate a thread to try connecting to broker
									Thread thread = new Thread(connector.DoConnect);
									thread.Name = uri.ToString();
									thread.Start();
								}
								else
								{
									// synchronous connect
									try
									{
										Tracer.DebugFormat("Attempting connect to: {0}", uri.ToString());
										transport = TransportFactory.CompositeConnect(uri);
										chosenUri = uri;
										break;
									}
									catch(Exception e)
									{
										Failure = e;
										Tracer.DebugFormat("Connect fail to: {0}, reason: {1}", uri, e.Message);
									}
								}
							}

							if(_asyncConnect)
							{
								// now wait for transport to be populated, but timeout eventually
								allDone.WaitOne(_asyncTimeout, false);
							}

							if(transport != null)
							{
								transport.Command = new CommandHandler(onCommand);
								transport.Exception = new ExceptionHandler(onException);
								transport.Start();

								if(started)
								{
									restoreTransport(transport);
								}

								if(this.Resumed != null)
								{
									this.Resumed(transport);
								}

								Tracer.Debug("Connection established");
								ReconnectDelay = InitialReconnectDelay;
								ConnectedTransportURI = chosenUri;
								ConnectedTransport = transport;
								connectFailures = 0;
								connected = true;

								if(firstConnection)
								{
									firstConnection = false;
									Tracer.InfoFormat("Successfully connected to: {0}", chosenUri.ToString());
								}
								else
								{
									Tracer.InfoFormat("Successfully reconnected to: {0}", chosenUri.ToString());
								}

								return false;
							}

							if(_asyncConnect)
							{
								Tracer.DebugFormat("Connect failed after waiting for asynchronous callback.");
							}

						}
						catch(Exception e)
						{
							Failure = e;
							Tracer.DebugFormat("Connect attempt failed.  Reason: {1}", e.Message);
						}
					}

					if(MaxReconnectAttempts > 0 && ++connectFailures >= MaxReconnectAttempts)
					{
						Tracer.ErrorFormat("Failed to connect to transport after {0} attempt(s)", connectFailures);
						connectionFailure = Failure;
						this.Exception(this, connectionFailure);
						return false;
					}
				}
			}
			finally
			{
				reconnectMutex.ReleaseMutex();
			}

			if(!disposed)
			{
				Tracer.DebugFormat("Waiting {0}ms before attempting connection.", ReconnectDelay);
				try
				{
					sleepMutex.WaitOne();
					try
					{
						Thread.Sleep(ReconnectDelay);
					}
					catch(ThreadInterruptedException)
					{
					}
				}
				finally
				{
					sleepMutex.ReleaseMutex();
				}

				if(UseExponentialBackOff)
				{
					// Exponential increment of reconnect delay.
					ReconnectDelay *= ReconnectDelayExponent;
					if(ReconnectDelay > MaxReconnectDelay)
					{
						ReconnectDelay = MaxReconnectDelay;
					}
				}
			}
			return !disposed;
		}

		/// <summary>
		/// This class is a helper for the asynchronous connect option
		/// </summary>
		public class Connector
		{
			/// <summary>
			/// callback to properly set chosen transport
			/// </summary>
			SetTransport _setTransport;

			/// <summary>
			/// Uri to try connecting to
			/// </summary>
			Uri _uri;

			/// <summary>
			/// Failover transport issuing the connection attempt
			/// </summary>
			private FailoverTransport _transport;

			/// <summary>
			/// Initializes a new instance of the <see cref="Connector"/> class.
			/// </summary>
			/// <param name="setTransport">The set transport.</param>
			/// <param name="uri">The URI.</param>
			/// <param name="transport">The transport.</param>
			public Connector(SetTransport setTransport, Uri uri, FailoverTransport transport)
			{
				_uri = uri;
				_setTransport = setTransport;
				_transport = transport;
			}

			/// <summary>
			/// Does the connect.
			/// </summary>
			public void DoConnect()
			{
				try
				{
					TransportFactory.AsyncCompositeConnect(_uri, _setTransport);
				}
				catch(Exception e)
				{
					_transport.Failure = e;
					Tracer.DebugFormat("Connect fail to: {0}, reason: {1}", _uri, e.Message);
				}

			}
		}

		private bool buildBackups()
		{
			try
			{
				backupMutex.WaitOne();
				if(!disposed && Backup && backups.Count < BackupPoolSize)
				{
					List<Uri> connectList = ConnectList;
					foreach(BackupTransport bt in backups)
					{
						if(bt.Disposed)
						{
							backups.Remove(bt);
						}
					}

					foreach(Uri uri in connectList)
					{
						if(ConnectedTransportURI != null && !ConnectedTransportURI.Equals(uri))
						{
							try
							{
								BackupTransport bt = new BackupTransport(this);
								bt.Uri = uri;
								if(!backups.Contains(bt))
								{
									ITransport t = TransportFactory.CompositeConnect(uri);
									t.Command = new CommandHandler(bt.onCommand);
									t.Exception = new ExceptionHandler(bt.onException);
									t.Start();
									bt.Transport = t;
									backups.Add(bt);
								}
							}
							catch(Exception e)
							{
								Tracer.DebugFormat("Failed to build backup: {0}", e.Message);
							}
						}

						if(backups.Count < BackupPoolSize)
						{
							break;
						}
					}
				}
			}
			finally
			{
				backupMutex.ReleaseMutex();
			}

			return false;
		}


		public bool IsDisposed
		{
			get { return disposed; }
		}

		public bool IsConnected
		{
			get { return connected; }
		}

		public void Reconnect(Uri uri)
		{
			add(new Uri[] { uri });
		}

		public FutureResponse AsyncRequest(Command command)
		{
			throw new ApplicationException("FailoverTransport does not implement AsyncRequest(Command)");
		}

		public Response Request(Command command)
		{
			throw new ApplicationException("FailoverTransport does not implement Request(Command)");
		}

		public Response Request(Command command, TimeSpan ts)
		{
			throw new ApplicationException("FailoverTransport does not implement Request(Command, TimeSpan)");
		}

		public CommandHandler Command
		{
			get { return _commandHandler; }
			set { _commandHandler = value; }
		}

		public ExceptionHandler Exception
		{
			get { return _exceptionHandler; }
			set { _exceptionHandler = value; }
		}

		public InterruptedHandler Interrupted
		{
			get { return interruptedHandler; }
			set { this.interruptedHandler = value; }
		}

		public ResumedHandler Resumed
		{
			get { return resumedHandler; }
			set { this.resumedHandler = value; }
		}

		public bool IsStarted
		{
			get { return started; }
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void Dispose(bool disposing)
		{
			if(disposing)
			{
				// get rid of unmanaged stuff
			}

			disposed = true;
		}

		public int CompareTo(Object o)
		{
			if(o is FailoverTransport)
			{
				FailoverTransport oo = o as FailoverTransport;

				return this._id - oo._id;
			}
			else
			{
				throw new ArgumentException();
			}
		}
	}
}
